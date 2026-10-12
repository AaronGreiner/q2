# 0038 — Spot checks: a live photograph at a moment nobody can plan for

**Status:** Accepted
**Date:** 2026-10-09
**Amends:** [0037 — Don'ts](0037-donts.md), whose "spot check" proof mode
this fills in, and [0024 — One notification pipeline](0024-one-notification-pipeline.md),
which gains a kind and a switch.

## Context

A Don't has nothing to photograph, but its friends should still have
something to judge. A photograph taken at leisure says little about an evening
without a bar. A photograph taken at a moment the owner could not plan for,
"show me what you are doing right now", does. This is how BeReal works, and it
keeps the photograph at the centre of q2.

## Decision

### Drawn by the server, never announced

- Only a running, unpaused Don't whose proof mode is **spot check** is checked.
  It gets **one check per day of an open window**: exactly one for a daily
  window, one each day for a weekly or monthly one. A period that already
  relapsed gets no more checks, because nothing is left to decide.
- The moment is a whole minute between **08:00 and 21:57 in the owner's zone**
  (21:57 so the three minutes end by 22:00). It is outside the owner's quiet
  hours and still ahead. If no minute is left, there is no check that day. The
  pick is seeded by the check's own id, which is random wherever ids are and
  reproducible in tests and seeds. No random source is read in the domain.
- `ScheduledAt` never leaves the server. `GoalResponse.spotCheck` carries a
  check only once it is open, only to its owner, and only with its id and when
  its three minutes end.

### Three minutes, live only

- When the moment comes, the check is **asked**: the owner gets
  `SpotCheckDue`. It has its own switch ("Stichproben bei deinen Don'ts", on by
  default), interrupts like any other kind (a banner in the open app, a push
  where available), and is not kept in the bell, because three minutes later it
  means nothing. It opens the start screen, where the open check is the first
  card with a countdown in seconds. Push is off by default, so this card is
  often how it is found. The card also appears on the Don't's own screen.
- The three minutes run from the moment it was asked, not from the moment
  drawn, so a late runner never takes time off the owner.
- The camera is **live only**: `PhotoCapture` has no picker and no file input
  for a check. The photograph is stored as `ProofSource.Live` whatever the
  client says ([0036](0036-live-or-screenshot.md)).
- A pause cannot be requested while a check is open. Otherwise it would be the
  way out of the very check it was meant to answer.

### Decided like a proof, but once

- Friends vote on it exactly as on any proof ([0018](0018-proof-and-vote.md)).
  It is always the **last attempt**: the moment is over once it has passed, so
  a doubted check is refused, not retried.
- A **refused** check, or **no photograph within three minutes**, makes the
  window `Missed`. That is being caught, never a `Relapse`.
- A **believed** check leaves the window to its limit. It does not decide the
  window, and a use logged afterwards is still a relapse. It checks the owner
  in for that day, like any believed photograph, and announces nothing in the
  feed.

### When nothing ran

`GoalMaintenance` settles checks idempotently, and so does `SpotCheckWorker`,
which looks every thirty seconds because a ten-minute pass cannot keep a
three-minute promise. A check whose three minutes passed before anything could
ask it, because the server was down, is **voided**: it is not asked late, and it
counts for nothing. The owner was never asked, so they cannot have failed to
answer. And a check announced an hour after its moment is one the owner could
have prepared for.

### Seeing one without waiting

`POST /api/diagnostics/spot-check/{goalId}` asks one on the caller's own
spot-check Don't now. Like every diagnostics route it does not exist in Staging
or Production. E2E and manual testing use it. The demo world seeds a spot-check
Don't with an open check (asked at the moment of seeding) and one missed in
its past.

## Consequences

- `SpotCheck` (Scheduled, Open, Answered, Missed, Voided) per window and day,
  `SpotCheckWorker`, `NotificationKind.SpotCheckDue`,
  `NotificationSwitch.SpotChecks`, and the `SpotChecks` migration, which turns
  the switch on for every existing account.
- `POST /api/goals/{id}/spot-checks/{checkId}/proof` answers one. The contract
  is regenerated.
