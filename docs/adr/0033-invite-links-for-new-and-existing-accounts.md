# 0033 — Invite links that work, for new and existing accounts

**Status:** Accepted
**Date:** 2026-09-24
**Amends:** [0022 — Getting away, asking for help, and leaving](0022-blocking-reporting-and-erasure.md),
section "An invite link is a secret, and redeeming one makes a friendship".
**Issue:** [#46](https://github.com/AaronGreiner/q2/issues/46)

## Context

Somebody with no friends is offered their invite link, with the promise that
whoever arrives through it is their friend straight away. In a real browser
that never happened:

- `/join/<code>` put the code into `useState` and called `navigateTo('/register')`.
  The page is server-rendered, so the redirect was an HTTP 302 — a fresh request
  in which the state no longer existed. Registration sent no code. The backend
  half worked and was tested; the hand-over between two requests was not, and
  only a run through a browser could have noticed.
- Somebody who already had an account got nothing from the link. The server
  redeemed codes at registration only, and signed-in visitors were sent home.
- The visitor could not see whose link it was before creating an account.

The author considered two kinds of link — one to join q2, one to become
friends — and decided on one.

## Decision

**One personal, reusable friendship link per person**, replaceable as before.
Registration stays open to everybody; the link adds the friendship. An
invitation into q2 that made no friendship would leave the newcomer exactly
where the link was meant to stop them: an account with nobody.

**The link opens a page that says whose it is**, and offers the one step that
fits the visitor:

| Visitor | Offered |
| --- | --- |
| no session | "Konto erstellen" — the form carries the code as `?invite=` — or "Ich habe schon ein Konto", which signs in and comes back via `?next=` |
| signed in, not friends yet | "Freundschaft annehmen" |
| signed in, already friends, or their own link | a sentence saying so, and the way home |
| a code that means nothing | "Dieser Link gilt nicht mehr", and still the way in |

Nothing has to survive a redirect any more: the code is in the address at
every step. Sentry drops query strings on both runtimes, and the code was in
the address of the landing page anyway.

**There is now a way to look a code up** — the rule 0022 relied on ("an
endpoint that answered 'whose code is this?' would turn an unguessable string
into something worth guessing at") is reversed. It is safe for the same reason
the link is: 96 random bits cannot be guessed, so whoever asks has been sent the
code, and the sender wanted them to see the name. The answer is minimal — a
name, initials and a colour; no id, no handle, no photograph — because the
asker may never sign up. There is deliberately no rate limit: at 96 bits one
would protect nothing, and the repository does not build ahead of a need.

**The code travels in a body, never in a path.** `POST /api/invite/preview`
and `POST /api/invite/accept` take `{ code }`. Paths reach request logs and
Sentry's fetch breadcrumbs intact; bodies and query strings do not.

**Accepting respects what already exists.** A block in either direction makes
the code answer 404, exactly like a code that means nothing, so the page cannot
announce a block. Already friends is not an error. A request already waiting
between the two, either way round, is accepted rather than joined by a second
row: if it was the visitor's request to the sender, the link is the sender's
answer to it. Registration and acceptance share this one code path.

## Consequences

- `POST /api/invite/preview` is the one route in a `RequireAuthorization`
  group marked `AllowAnonymous`, and it says so where it is mapped.
- **The link is reachable after the first friend** ([#48](https://github.com/AaronGreiner/q2/issues/48)).
  `InviteCard` stays the empty state; `InviteSheet` is the same link opened from
  the friends tab's header, the profile and a search that found nobody.
  Replacing it moved to the settings: a safety action, reached for once. A
  pasted link previews in a messenger with a generic title and the app icon,
  never the sender's name, and is marked `noindex`.
- The E2E suite registers fresh accounts for both ends of a link, rather than
  touching the seed, because every spec shares one database.
- The link is built from `window.location.origin`. Inside a Capacitor WebView
  that is not an address anybody else can open, so the iOS app builds it from
  the configured `siteUrl` instead ([0034](0034-bearer-tokens-for-the-native-app.md)).
  Opening the link in the installed app is [#50](https://github.com/AaronGreiner/q2/issues/50).
