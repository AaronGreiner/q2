# 0026 — Mail, and the way back into an account

**Status:** Accepted
**Date:** 2026-09-15
**Amends:** [0011 — Accounts with ASP.NET Core Identity and a session cookie](0011-authentication-with-identity.md),
which left password reset out because q2 sent no mail.

## Context

Until now q2 sent no mail at all, and ADR 0011 accepted what that cost: a
forgotten password meant a new account, and the goals, streaks and friends on
the old one were gone. It also meant somebody who had forgotten their password
could not delete their account either, because deleting asks for the password
again. That was the first thing to fix before anybody who is not a developer
signs up ([#3](https://github.com/AaronGreiner/q2/issues/3)).

Two existing rules shaped what could be built: nothing here needs a running
service to develop against (AGENTS.md section 6), and nothing about
authenticating is written by hand (section 8).

## Decision

**q2 sends mail over SMTP, to a transactional provider in the EU.** SMTP rather
than a provider's own HTTP API, so the provider is configuration — host, port,
user name, password — and changing it is not a code change. In the EU because a
provider is a processor (Art. 28), and one inside the EEA needs no transfer
mechanism on top ([#33](https://github.com/AaronGreiner/q2/issues/33)). The
client is MailKit; the framework's own `SmtpClient` is advised against for new
code by its own documentation. TLS is required and never negotiated away: port
465 speaks it from the first byte, and every other port has to upgrade with
STARTTLS or the send fails.

**One seam, three transports** — `IMailTransport`, chosen once at startup from
`Q2:Mail:Transport`:

| Transport | Where | What happens to a mail |
| --- | --- | --- |
| `Smtp` | Staging, Production | handed to the provider |
| `File` | Development, ManualTesting, E2E | written as a `.eml` under `mail/` beside the database; any mail client opens it, and the E2E suite reads the link out of it |
| `Off` | anywhere but Production | nothing — the reset screen says this environment sends no mail |

The integration tests replace the transport with a recorder, as they already do
the push sender. Three rules are enforced before the host takes its first
request (`MailSettings`):

- **Production refuses to start without SMTP.** A reset is the only way back
  into an account, and a host that cannot send one must not look as if it could.
- **Staging and Production refuse `File`.** A reset mail is a key to an account
  for an hour.
- **Every link starts from `Q2:PublicAppUrl`**, never from the request's `Host`
  header. A link built from the header would let whoever sends the request
  decide which domain the link in somebody else's inbox points at.

Staging runs with `Off` until its mail secrets exist; the release workflow warns
while they do not.

**A reset is a link, not a code.** The token is Identity's
(`AddDefaultTokenProviders`): data-protected, bound to the account's security
stamp, and valid for an hour. The link carries the account id and that token in
one opaque value, so no address appears in a URL. A six-digit code typed into
the app would have kept people inside the installed app, but a code can be
guessed, so it would have needed an attempt counter of our own around Identity —
exactly the hand-written authentication this repository does not do. The cost of
the link is stated rather than hidden: on an iPhone it opens in the browser, not
in the installed app, and the screen at the end says to sign in again there.

**The token travels in the fragment** — `/reset-password#token=…`. A browser
never sends a fragment anywhere, so the token reaches neither Caddy nor the
server rendering the page nor a server-side trace. Session Replay records the
address of the page it starts on, and it starts before any component does, so
an inline script in the reset page's `<head>` lifts the token out of the address
bar while the HTML is still being parsed — before the bundle, Sentry or the
router run (`app/app/utils/resetLink.ts`). As a second line, the Sentry
scrubbers now cut fragments from URLs as well as query strings.

**The answer never says whether an address has an account.** Asking for a link
answers 202 for every well-formed address. Looking the account up, issuing the
token and talking to the provider all happen after the response, from an
in-memory queue (inline in AutomatedTest, like notifications), so the two cases
also take the same time by construction rather than by padding. Registration
still discloses a taken address, as ADR 0011 accepted; this endpoint does not add
a second, quieter way.

**A reset ends every session of the account.** A new password means a new
security stamp, which spends the link and invalidates every cookie issued
before it. Identity checks a session against the stamp every thirty minutes by
default; q2 checks every minute (`AccountPolicy.SessionRecheckInterval`), so
whoever knew the old password is out within a minute rather than by lunch. The
device that did the reset is signed out as well and ends on the sign-in, with
the address filled in. A successful reset also lifts a lockout: a lockout exists
to stop somebody guessing, and this person did not guess.

**Two limits.** One client may ask for five links in fifteen minutes — ASP.NET
Core's own rate limiter, answering 429 with Problem Details — and one account is
mailed at most once in two minutes, whoever asks. The first needs the client's
real address, so the API now applies `X-Forwarded-For` from the proxy on its own
machine, and only that header: `X-Forwarded-Proto` would change how the session
cookie is issued, which is a decision of its own. The same address is what a
backend Sentry event carries (`SendDefaultPii`) — the client's, as
[privacy.md](../privacy.md) already described, rather than the proxy's.

**The server writes the mail's sentences.** This is the one exception to "the
API sends structure, not sentences"
([0010](0010-german-first-interface.md)): a mail is read in a mail client, long
after the request, with no q2 running to compose it. Its German and English text
lives beside the only feature that sends one (`PasswordResetMail`), in the
language the account chose in its settings, as plain text, naming nobody.

## What was deliberately not built

- **Email confirmation and two-factor authentication**
  ([#5](https://github.com/AaronGreiner/q2/issues/5),
  [#6](https://github.com/AaronGreiner/q2/issues/6)). Both will use the token
  providers and the transport this adds; neither is part of getting back in.
- **Changing the password while signed in.** A smaller change of its own, with
  the same rule about sessions
  ([#40](https://github.com/AaronGreiner/q2/issues/40)).
- **HTML mail, a reply address and a durable outbox.** A reset mail is a few
  sentences and a link; nobody is there to answer a reply yet; and a mail lost
  to a restart is one somebody asks for again with a tap.
- **Links that open the installed app.** They belong to the Capacitor build
  ([#7](https://github.com/AaronGreiner/q2/issues/7)).

## Consequences

**Good**

- Somebody who has forgotten their password gets back in without a developer
  touching the database — and can delete their account again, too.
- Development and every test suite still need no running service: a mail is a
  file or a recording, and the E2E suite follows a real link out of a real mail.
- The provider is four values in the environment.

**Bad, and accepted**

- **Identity's token provider reads the system clock**, not `TimeProvider` —
  the one place in q2 where time is not injected. The hour is therefore tested
  as configuration rather than by moving a clock.
- **A new processor.** The provider sees the address and the link of every
  reset mail. It needs an agreement and a line in the privacy notice
  ([#33](https://github.com/AaronGreiner/q2/issues/33),
  [#34](https://github.com/AaronGreiner/q2/issues/34)).
- **Staging has no password reset until its mail secrets exist**, and says so.
- **Both limits and the queue live in memory.** A restart forgets them, which
  costs at most a mail; q2 is one process on one host
  ([0008](0008-deployment-topology.md)), and a second instance would need them
  shared.

## When to revisit

- when there is a second API instance — the limits and the queue are per process;
- when q2 sends a second kind of mail, or one that needs more than a few
  sentences — the texts would outgrow one class;
- when the Capacitor build lands — a link could open the app itself.
