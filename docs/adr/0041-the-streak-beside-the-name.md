# 0041 — The streak beside the name, and a start screen of your own goals only

**Status:** Accepted
**Date:** 2026-10-16
**Issues:** [#121](https://github.com/AaronGreiner/q2/issues/121),
[#78](https://github.com/AaronGreiner/q2/issues/78),
[#79](https://github.com/AaronGreiner/q2/issues/79)
**Amends:** [0032 — The next step first](0032-the-next-step-first.md), whose
start screen had one card for the streak, with today as a ring and a week of
pills.

## Context

- **The streak had a card of its own.** `StreakHero` sat under the next step:
  the number, today's share as a ring, a week of seven pills and a line of
  encouragement. The number is what people look for on the start screen; the
  card put it, and two figures about it, between them and what there was to
  do.
- **The start screen took friends' goals for yours.** `/api/today` returns
  every goal the reader owns *or checks* — the Dos tab needs both, and lists
  them apart since #77. The start screen did not ask whose a goal was: a
  friend's Do sat under "Heute" with an hourglass as if a photograph were
  being looked at, a friend's window could be "Als Nächstes" with a camera
  whose upload the server refuses, and the ring counted a friend's
  undelivered window against your day. The goal's own screen offered the
  same friend "Beweis liefern" (`acceptsProof` describes the window, not the
  reader) and never said whose goal it was.

## Decision

### A flame beside the name

The streak moves into the header, directly after the name (`StreakFlame`, in
`AppScreenHeader`'s `title-trailing` slot) — left of the bell and the avatar,
because it belongs to the person, not to the screen's actions.

- **The badge the card carried**, unchanged: a circle in the flame gradient
  with a dark flame while there is a streak, the number beside it in the
  flame's text colour; a grey circle with a dimmed flame without one
  ([0028](0028-ruhe-design-system.md): the gradient only for a streak).
- **No number on a grey flame.** For a new person it would always read 0, and
  the grey already says it.
- **One sentence for a screen reader** — "12 Tage in Folge", "Noch keine
  Serie" — rather than an icon and a digit.
- **Not tappable.** The avatar beside it opens the profile, where the streak
  is the first of the numbers; a second way to the same place is clutter.
- **A long name gives way first.** The title truncates; the flame does not.

### The ring and the week go, and leave the contract

Nothing else draws today's ring or the week of pills, so
`ProfileResponse.today` (`DaySummaryResponse`, `GoalService.SummariseTodayAsync`)
and `ProfileResponse.weekActivity` (`Person.WeekActivity`, `Streak.Week`) are
removed rather than computed on every profile read for nobody. The numbers
#27 wants for the week are counted from windows, not check-in days, and get a
field of their own once that issue has a screen to put them on.

### The start screen shows only your own goals

`useHome` keeps the goals the reader owns (`goal.isMine`) and drops the rest;
`nextUp` refuses a friend's goal on its own as well, since "the one thing to
do next" is never somebody else's to hand in. `/api/today` is unchanged —
the Dos tab lists friends' goals under "Von Freunden". A friend's goal
reaches the start screen only as what it asks of you: its photograph in the
vote stack.

A separate read-only section for friends' due goals was considered and left
out: it would repeat the Dos tab's "Von Freunden" on a screen that is about
what you have to do.

### A friend's goal says whose it is

The goal's screen offers the camera only to its owner. A friend sees "Von
<Name>" with the owner's avatar under the title, linked to their profile,
and where the camera would be: "Noch kein Beweis von <Name>" while the window
is owed, "Wird geprüft" while a photograph is being looked at.

## Consequences

- `StreakHero` is deleted, and with it `weekStates`, `weekdayIndex` and the
  `DaySummary` type; `StreakFlame` is new.
- Two fields leave `ProfileResponse`; the contract and the generated types
  are regenerated.
- The start screen opens on whatever is first that day — a spot check, the
  next step, the challenge, the vote stack or "Heute" — so each of them sits
  flush under the panel's padding (`first:mt-0`).
