# 0052 — Pauses planned ahead, and for everything at once

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#99](https://github.com/AaronGreiner/q2/issues/99)
**Amends:** [0020 — The exits](0020-pause-and-archive.md), whose pause began
the moment it was asked for and belonged to one goal.

## Context

Somebody with five Dos who falls ill or goes on holiday had to open each goal,
write a reason five times and pick a length five times — and only on the day
itself. A holiday known two weeks ahead could not be entered ahead, and from
the beach it is easy to forget.

## Decision

- **A pause can start on a later day**, up to `PauseRules.MaxDaysAhead` (30)
  days ahead: "Ab wann? Heute · Morgen · Später". Until its first day it holds
  nothing — the window running now runs as it would have — and **its friends
  are not told**: they hear of it when it begins, which is also when they can
  first object. Then the maintenance pass sets aside the window open at that
  moment (`Goal.BeginPause`), exactly as asking that morning would have, and
  announces it once (`GoalPause.AnnouncedAt`, by `ProofVerdicts.AdvanceAsync`).
  A window whose deadline fell before the pause began is not excused by it.
- **It counts against the allowance of the month it begins in**, not the one
  it was entered in: a pause for November uses November's two. One taken back
  before it began covered nothing and counts nowhere; its owner sees it on the
  goal ("Ausgesetzt ab …", "Zurücknehmen") and nobody else does.
- **"Alle aussetzen"** sets every running goal of the person's — Dos and
  Don'ts — aside with one reason, one length and one first day
  (`POST /api/goals/pauses`). Underneath it is one pause per goal, so each
  goal's friends are told and may object as they always could. A goal that
  cannot be set aside on those days is left out rather than failing the rest
  — a vote in flight, no pause left that month, already paused then — and the
  answer names it and why (`PauseRefusal`). It is offered from the Dos tab's
  header once there is more than one running goal of your own, and as a switch
  in a goal's own pause sheet.
- "Never two at once" now means no two pauses on the same day, running or
  planned.

## Consequences

- One column, `GoalPauses.AnnouncedAt` (migration `PlannedPauses`); every
  earlier pause was announced when it was made, so that is what it gets.
- `GoalResponse.plannedPause` exists for the owner only.
- A reason entered for every goal at once is read by the friends of every one
  of them — the sheet says so ("die Freunde jedes Ziels sehen sie wie immer").
