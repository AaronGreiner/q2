# 0051 — A first window that can be met

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#96](https://github.com/AaronGreiner/q2/issues/96)
**Amends:** [0016 — Windows instead of steps](0016-windows-instead-of-steps.md),
whose first window was the whole period around the day a goal was made.

## Context

A new goal's first window was its period at full size. "3× pro Woche" made on
a Sunday needed three believed photographs by midnight; "2× pro Monat" on the
30th had a day or two; "Jeden Tag" made at 23:50 had ten minutes. The first
thing such a goal did was record a miss — which friends read in the balance,
and which ends the streak — and it punished starting late in the week, which
is when people start things.

## Decision

- **A quota's first window starts with the goal and asks for its share.**
  `GoalSchedule.FirstWindow` cuts the period to begin on the goal's first day
  and scales the quota to the days left, rounded up and at least one
  photograph: three a week from a Thursday is two, from a Sunday one. The
  rule is the one a window cut by an edit already follows (`Prorate`,
  [0044](0044-editing-a-running-goal.md)), and a Don't's limit is scaled the
  same way when its window opens. From the second window on, every period is
  whole.
- **The owner can start tomorrow.** The summary step asks "Wann geht es
  los?" — "Ab heute" or "Ab morgen" — for anything that repeats; a one-off
  has its own day. Tomorrow is stored on the goal (`Goal.StartsOn`), because
  nothing else would say it: such a goal has no window today, and the
  maintenance pass opens tomorrow's as its first. Its card says "Ab …"
  instead of a bare status. The server takes today or tomorrow, and nothing
  later: a goal planned for next month is a different feature.
- **The summary says what the first window asks**, not only when it ends:
  "Dein erstes Fenster: 2 Nachweise bis So, 25.10. um Mitternacht", with one
  sentence of why when it is a share of a period that has begun. For a Don't:
  "höchstens 1× bis …". The client mirrors the cut and the share
  (`utils/firstWindow.ts`); the server stays the authority.

## Consequences

- One nullable column, `Goals.StartsOn` (migration `GoalStartsOn`); goals made
  before it start on the day they were made, as they always did.
- The seeds lay their histories out from the uncut window around today
  (`GoalSchedule.WindowAround`): a seeded goal stands for one that has been
  running for a while, not one made this morning.
- A daily goal made at 23:50 "ab heute" still has ten minutes. The summary now
  says so, and "Ab morgen" is one tap away.
