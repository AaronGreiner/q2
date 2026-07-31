# 0011 — Accounts with ASP.NET Core Identity and a session cookie

**Status:** Accepted
**Date:** 2026-07-31
**Supersedes:** [0006 — Authentication deliberately deferred](0006-authentication-deferred.md)
and [0009 — One known person, flagged in the database](0009-single-known-person.md).

## Context

ADR 0006 deferred authentication and said what would have to be true before it
arrived. ADR 0009 filled the gap in the meantime: one `Person` row carried
`IsCurrentUser`, and the API answered as that person for anybody who could
reach it. Both records named the trigger for revisiting them — *a second person
needs to sign in* — and that is what this change is.

The deferral was worth what it cost. The model already had `Person` as a real
entity that goals, messages, kudos and friendships point at, so identity had
somewhere to attach to; `CurrentPerson` was already the single place that
answered "who is asking?"; the frontend's error taxonomy already had an
`unauthorized` kind; the Sentry scrubber already removed user identity. What was
missing was an account, and a rule about who may read what.

## Decision

**ASP.NET Core Identity, with a session cookie.** Nothing about authenticating
is written by hand: the password hash, the security stamp, lockout and the
cookie are all the framework's, through `UserManager` and `SignInManager`. That
is exactly what ADR 0006 asked for — "do not implement password hashing,
session management or token issuing by hand".

Six decisions inside that:

1. **An account is separate from a person.** `AppUser : IdentityUser<Guid>`
   holds credentials and one column: the `PersonId` it signs in as. The domain
   model never grows an Identity base class, and a person can exist *without* an
   account — which is what lets a seeded world contain people nobody needs to
   sign in as.

2. **A session, not a token.** The cookie is http-only, so no JavaScript ever
   holds it and an injected script cannot steal it. It also means server-side
   rendering still works: Nitro forwards the incoming cookie to the API, so the
   first paint of a page is already the signed-in one. A bearer-token variant
   would have moved every screen's data loading into the browser to solve a
   problem q2 does not have yet.

   The cost is stated rather than hidden: when q2 is packaged with Capacitor,
   a WebView on a `capacitor://` origin is a harder place to keep a cross-site
   cookie than a browser tab is. Identity can issue bearer tokens from the same
   setup, so that is an addition at that point, not a rewrite.

3. **Three fields to register: name, email address, password.** The handle, the
   initials and the avatar colour are derived from the name
   (`ProfileDefaults`), because they are three more things to get wrong on a
   phone keyboard and none of them is a decision somebody wants to make before
   they have seen the app.

4. **Length is the whole password rule.** Ten characters, no composition
   requirements. Identity's defaults would produce "Passw0rd!"; current NIST
   guidance (SP 800-63B) says the same thing this does.

5. **`IsCurrentUser` is gone, and ownership took its place.** `Goal` and
   `GoalTask` gained an `OwnerPersonId`, every read is scoped to the caller, and
   every feature endpoint group carries `RequireAuthorization`. ADR 0006 was
   explicit that a goal without an owner and a rule about who may read it is not
   finished; this is that half of the change, and it is why it could not be
   shipped separately.

6. **Friendships became two-sided.** One row per pair, with a requester and an
   addressee, because a friendship only one of the two can see is not one. The
   "suggested" status is gone: a suggestion is a statement about the friend
   graph as it stands now, so it is derived on every read rather than stored and
   left to go stale.

**Every seeded person gets an account**, all sharing one documented password
(`SeedAccounts`). That is what keeps the suites and the manual flows workable
with a sign-in in front of everything: a test can be *anybody* in the world it
seeded, which is the only way to check that a friend request or a group chat
looks right from both ends. The hash is a committed constant rather than
something computed while seeding, because Identity's hasher salts randomly and a
seed has to be a pure function of its context; `SeedAccountTests` asserts the
constant still verifies, so a framework change cannot quietly lock every seeded
account out.

## What was deliberately not built

- **Email confirmation, password reset, two-factor.** All three need to send
  mail, and "nothing that needs a running service to develop against" is a rule
  this repository already has ([AGENTS.md](../../AGENTS.md) section 6). The
  sign-in screen says so rather than offering a link that goes nowhere.
- **`MapIdentityApi`.** It brings that same set of routes along with a response
  shape that is not the Problem Details every other endpoint here answers with.
  The four endpoints q2 needs are written against `SignInManager` instead.
- **Roles.** The context is `IdentityUserContext`, not `IdentityDbContext`:
  there are no roles, and three more tables nothing queries is not a foundation,
  it is furniture.
- **An external identity provider.** OpenID Connect against Auth0, Entra or
  Keycloak was the other realistic option ADR 0006 named. It would mean a
  running service to develop against, and it answers a question q2 does not have
  — nobody is bringing a corporate identity to a self-care app.

## Consequences

**Good**

- Two people can sign in, and each sees their own data. That is the property
  every social feature in q2 was written against and none of them could
  actually demonstrate.
- Exactly one implementation changed to get there: `CurrentPerson` now reads the
  request's principal instead of a column. Every call site already asked it the
  right question, which is what ADR 0009 predicted.
- The unauthenticated, wrong-person and correct-person cases are covered by
  integration tests that run the real Identity stack, and by an E2E spec that
  starts signed out.

**Bad, and accepted**

- **There is no way back into a forgotten account.** No mail, no reset. This is
  the first thing to fix before anybody who is not a developer uses q2, and it
  is on [next-steps.md](../next-steps.md).
- **Registration discloses that an address is already taken.** The alternative —
  accepting the sign-up and saying nothing — trades a real usability problem for
  a small enumeration one. Sign-*in* does not disclose it: a wrong password and
  an unknown address get the same answer.
- **A Development database created before this change has no accounts.** The
  migration converts every goal, task and friendship honestly, using the
  `IsCurrentUser` flag before dropping it, but a password hash is not something
  a migration may invent. Starting over is one command
  ([README.md](../../README.md) section 6).
- **The demo credentials are in the frontend bundle** wherever
  `NUXT_PUBLIC_DEMO_*` is set. They are empty by default and set only where the
  database is a synthetic seed profile, but this is a switch that has to stay
  off in Staging and Production, and it is worth knowing it exists.

## When to revisit

- when q2 is packaged with Capacitor — see decision 2;
- when somebody has to be able to recover an account;
- when a second kind of actor appears (a coach, a support agent, a group
  moderator), which is the point at which "who may read this" stops having one
  answer per row and roles start earning their tables.
