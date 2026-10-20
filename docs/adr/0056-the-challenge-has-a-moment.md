# 0056 — The daily challenge has a moment

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#136](https://github.com/AaronGreiner/q2/issues/136)
**Amends:** [0021 — The daily challenge](0021-daily-challenge.md), whose
challenge ran from midnight to midnight, and
[0023 — Notifications](0023-web-push.md), whose quiet hours swallowed its
announcement for everybody on the defaults.

## Context

The challenge had no moment. It went live at local midnight, so all morning
the start screen said "Noch 23 Stunden" and there was no reason to answer it
now rather than later — "the task of the day, for all of us at once" with
nothing making it happen at once. Its announcement went out by the first hourly
pass after midnight, inside the default quiet hours (22:00–07:00), which drop
rather than hold: on the defaults nobody was ever told. It was found by opening
the app.

## Decision

1. **It appears at a moment between 10:00 and 20:00** in `Q2:TimeZone`, the
   same instant for everybody, BeReal-style. The moment is drawn from the date
   alone (`ChallengeQueue.MomentFor`, a fixed integer hash — never a random
   number generator), so the queue stays a pure, stable function and a day
   planned twice gets the same moment. It is the row's `PublishedAt`: before
   it there is no challenge on any screen, and no card saying one is coming.
2. **The first two hours are "the moment"** (`MomentEndsAt`). The card counts
   them down ("Jetzt · noch 1 Std. 45 Min."); after them a contribution is
   still taken until midnight and is marked "zu spät", on the tile and in the
   room. Nothing else about it changes: there is still nothing at stake.
3. **One push, or a banner, at the moment**, through the pipeline as before
   — the `Challenge` switch, quiet hours, a banner for somebody looking. The
   10:00–20:00 window lies outside the default quiet hours, so the defaults
   let it through. The bell still keeps no line: the card is its home. The
   queue pass runs every minute so the announcement goes out at the moment,
   not up to an hour later; it stays idempotent through `AnnouncedAt`.
4. **No reminders.** Whoever sits it out loses nothing and is not told about
   it, as 0021 said.
5. **One instant for everybody.** Somebody far from `Q2:TimeZone` may get it
   at night and lose the push to their quiet hours — acceptable for something
   with nothing at stake.

## Consequences

- One nullable column, `Challenges.MomentEndsAt` (migration
  `ChallengeMoments`). Rows that were running or past keep midnight and no
  moment, so nothing is "late" retroactively; rows still queued, which nobody
  had seen, are taken out and written again with their moment by the next
  pass. Seeded challenges have no moment and simply run, so no test depends
  on the hour.
- The contract gains `momentEndsAt` on a challenge and `isLate` on an entry.
