# 0006 — Authentication deliberately deferred

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

q2 is about shared goals, which implies people, friendships and communities.
The obvious instinct is to start with a `User` table.

But identity is the part of an application that is hardest to change later and
most damaging to get wrong. Building a homegrown user table with a password
column "for now" produces exactly the thing that never gets replaced.

## Decision

**No authentication, no accounts, no permissions in this version — and no
placeholder for them either.**

Participants are `GoalParticipant` rows carrying a display name. That is
enough to render a shared goal, and it commits to nothing.

Specifically:

- No `User` entity, no password storage, no session handling, no token issuing.
- No `[Authorize]`, no scopes, no roles, no ownership column.
- `GoalParticipant` is a separate entity rather than a string list, so gaining
  a user reference later is a migration rather than a rewrite.
- The frontend's error taxonomy already includes an `unauthorized` kind and a
  message for it, so adding 401/403 handling is a mapping change.
- The Sentry scrubber already strips user identity, so nothing has to be
  retrofitted when there is identity to strip.

**Until authentication exists, the API must not be publicly exposed with real
data.** It has no access control; it is a reference implementation.

## Consequences

- Nothing in this version can distinguish "my goals" from "everyone's goals".
  That is a visible, honest gap rather than a half-built one.
- No insecure authentication exists to be mistaken for a real one.
- Introducing identity will touch the goal endpoints, the domain model and the
  frontend — all of them small, because there are three endpoints.

## When authentication is introduced

1. Use an established identity provider or library. **Do not implement
   password hashing, session management or token issuing by hand.** Realistic
   options: ASP.NET Core Identity, or an external provider via OpenID Connect.
2. Decide first what identity *is* for this product — an account, or a device,
   or a pseudonymous profile — before writing a table.
3. Add ownership to `Goal` and authorisation to the endpoints in the same
   change; a goal without an owner and a rule about who may read it is not
   finished.
4. Store the minimum: an opaque id and whatever the provider requires. No
   password, no profile data that is not used.
5. Extend the Sentry scrubber's user handling: an opaque user id may be kept;
   email address, display name and IP must not.
6. Revisit [../privacy.md](../privacy.md) — identity changes the record of
   processing activities, the legal basis and the erasure path.
7. Add integration tests for the unauthenticated, wrong-user and correct-user
   cases before shipping.

## Alternatives considered

- **A minimal user table now.** Every field would be a guess, and the guesses
  would harden into a migration path.
- **A fake "current user" for development.** Code that only exists to be
  removed later, and reliably outlives the intention to remove it.
- **API keys as a stand-in.** Solves service-to-service access, not user
  identity, and would invite being used for the latter.
