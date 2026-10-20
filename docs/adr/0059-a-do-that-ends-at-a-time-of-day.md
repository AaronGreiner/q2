# 0059 — A Do that ends at a time of day

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#98](https://github.com/AaronGreiner/q2/issues/98)
**Amends:** [0016 — Windows instead of steps](0016-windows-instead-of-steps.md),
where "delivered by midnight was on time", and the evening warning of
[0019](0019-warning-and-balance.md).

## Context

Every window ended at midnight in its owner's zone. That fits "2 L Wasser am
Tag", not a goal whose point is the time: the template "Vor 7 Uhr aufstehen"
could be proved at 23:00, and friends could not tell. The template promised
something the model could not check.

## Decision

- **"Bis … Uhr", per Do** (`Goal.DueBy`), for a Do whose windows are single
  days — every day, every few days, set weekdays — decided in the session. A
  quota's week, a one-off's day and a Don't's period still end at midnight; an
  edit that turns a goal into one of those leaves the time stored but without
  effect (`EffectiveDueBy`).
- **It is the window's deadline**, not a new window shape: the window still
  begins at the day's start and ends at that time in the owner's zone
  (`Goal.DueAtOn`, `LocalCalendar.AtOrAfter` — a time the clock skips is the
  next one that exists). The maintenance pass misses it then; a photograph
  after it is refused, because a delivery brings the goal up to date first.
- **A reminder comes before it.** A reminder at or after the day's end would
  never be sent; it is refused (`reminderAt`), and the create sheet moves the
  template's 09:00 to half an hour before the time chosen.
- **Made after today's time, it starts tomorrow** — "bis 07:00" made at eleven
  would be missed before anybody could deliver. The server moves the start and
  the summary says so instead of offering "Ab heute".
- **The warning follows the deadline.** A window that ends at midnight is warned
  about from 20:00, as before; one that ends at a time from four hours before —
  the same lead — but never before 06:00, so a "bis 07:00" is warned about at
  six, not at three.
- **The words say the time**: "Jeden Tag · bis 07:00" for the rhythm, "Heute bis
  07:00" for the window, "um 07:00" in the first window's sentence. The
  template "Vor 7 Uhr aufstehen" comes with 07:00.

## Consequences

- One nullable column on `Goals`; every existing goal ends at midnight.
- The seeded worlds keep their goals ending at midnight: a seeded "bis 07:00"
  would show a missed day every time somebody opened the app after seven.
- Changing the time on a running goal is not offered yet; it would be an edit
  in front of the checkers like any other (0044).
