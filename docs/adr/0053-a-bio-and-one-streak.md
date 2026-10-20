# 0053 — A bio, and one streak

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#20](https://github.com/AaronGreiner/q2/issues/20)

## Context

The takeover plan extends a person with a bio and with Qdos's second streak:
Qdos counts an activity streak and a community streak, q2 counts one. Neither
had been carried over, and the second streak had never been defined — only
named. ADR 0021 already ruled out a streak fed by the daily challenge.

## Decision

- **A bio: a few words of a person's own under their name.** Optional, at
  most 160 characters (`Person.MaxBioLength`), set, changed and cleared in the
  profile edit sheet beside the name — a blank one takes it away — and shown
  under the handle on your own profile and on anybody's who may open it. It
  is user content: never logged, masked in a replay (`data-q2-private`), gone
  with the account.
- **q2 keeps one streak.** The streak is days in a row with a check-in — a
  believed photograph, or a Don't kept for its period — and it is the number
  beside the name, on the profile, and the one friends hear about at its
  milestones ([0049](0049-streak-milestones-in-the-feed.md)). A "community
  streak" would have to count something friends do *for* you — votes, kudos —
  and turn their attention into a score you can lose; or count the challenge,
  which is deliberately for nothing ([0021](0021-daily-challenge.md)). A
  second flame beside the first would also make "the streak" ambiguous on
  every screen that says it. Kudos given already earn a badge
  ([0050](0050-categories-and-badges-that-are-earned.md)); that is where
  being there for others is recognised.

## Consequences

- One nullable column, `People.Bio` (migration `PersonBio`); the contract
  carries `bio` on `ProfileResponse`, `PersonProfileResponse` and
  `UpdateProfileRequest`.
- A proposal for a second streak starts by superseding this record.
