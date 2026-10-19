# 0037 — Don'ts: a limit per period, logged uses and an honest relapse

**Status:** Accepted, amended by [0038](0038-spot-checks.md) — how a spot check is drawn, asked and decided
**Date:** 2026-10-09
**Amends:** [0016 — Windows instead of steps](0016-windows-instead-of-steps.md),
where every window was closed by proofs;
[0018 — A window is closed by other people](0018-proof-and-vote.md),
where a believed photograph always finished its window; and
[0019 — The warning before, and the record after](0019-warning-and-balance.md),
whose balance had two numbers.

## Context

Many of the commitments people most need witnesses for are about stopping
something: no alcohol, no social media after ten, no smoking. q2 could only
express doing something. Every window was closed by a photograph of it, and an
abstinence cannot be photographed. Bolting a Don't onto a Do with an awkward
picture would have made both worse, so a Don't gets its own rules for how a
window is decided. Everything else stays shared: schedule, windows, friends,
conversation, pause, archive.

## Decision

### One shape: at most N per period

A Don't is a `Goal` with `Kind = Dont`. Existing goals became `Do` in the
migration. A Don't says "at most **N** times per day, week or month", and
**N = 0 means "never"**. It is one form, not two: "never" and "at most" would be
two ways to say one thing. The schedule kind is `Limit` with a `Period`, and
`QuotaPeriod` gained `Day`, which a Do's quota may not use. The window is the
period. The limit is copied onto each window when it opens, like its deadline,
so history keeps what each window was held to. A running Don't's limit is
changed by an edit and from the next window on; a higher one waits for its
checkers' objections ([0044](0044-editing-a-running-goal.md)).

### Uses are logged, and the one over the limit is a relapse at once

The owner logs a use in one tap, from the row, the goal or its conversation
(`POST /api/goals/{id}/uses`). The photo is optional, nobody votes on it and
nobody is notified. Each use is a `DontUse` row, shown as a neutral line in the
goal's conversation. `GoalTimeline` derives that line; it is never written as a
message ([0027](0027-goal-conversations.md)). Its photograph is uploaded as a
proof image and read by the goal's audience.

The use that goes over the limit sets the window to **`Relapse`** immediately,
the way a refused proof sets `Missed` immediately. The rest of the period cannot
change that outcome, so the period draws no more checks and needs no proof at
its end. The owner can still log further uses, which appear as lines and change
nothing. Until the period ends, the response's `Current` is that decided window,
so "this week" still reads "Rückfall".

The app asks before the tap that would be the relapse. One question with the
honest cost stated, because a relapse cannot be taken back and a slipped thumb
should not cost a week. It does not try to talk anybody out of it.

A use within the limit is not asked about, so it can be taken back instead: the
newest use of a period still open, by its owner, within 30 seconds of logging
it (`DontUse.UndoPeriod`, `DELETE /api/goals/{id}/uses/{useId}`). The app
offers that as "Rückgängig" on the toast that confirms the use. The relapse is
never offered back — it was the one tap that was asked about (#110).

### What a relapse costs, and what it does not

- **The streak holds.** It neither grows nor ends: owning up costs less than
  being caught. **The third relapse in a row ends it like a miss**, or honesty
  would be a way never to lose a streak. A kept period in between starts the
  count again. `Goal.StreakOf` is the one place this is counted; the
  conversation counts forwards with the same function.
- **The balance counts it as its own number** (`Done · Missed · Relapsed`),
  never as a miss, on the goal, the profile and a friend's profile.
- **Being caught is not a relapse.** A refused or missed check (#63), or a
  proof at the end that was refused, is `Missed`.
- **The chat list never previews a relapse or a use**, for the same reason it
  never previews a miss (0032).

### A window is kept when its period ends within the limit

`GoalMaintenance` settles every expired window through `GoalInstance.Expire`:
- A Do's window that ran out is missed.
- A Don't's is kept (`Done`) unless its proof mode asked for a photograph that
  was never believed.

This is idempotent and catches up: after an outage, each period that passed is
opened and settled in order. A believed photograph no longer closes a Don't's
window early, because a use logged afterwards is still a relapse. A kept Don't
window checks the owner in for its last day, so it counts towards the daily
flame. A relapse adds no check-in: the goal's streak holds, but the daily flame
may break.

### How friends check it: the proof mode

Chosen when the Don't is made:
- **Spot check**: random live checks during the window
  ([#63](https://github.com/AaronGreiner/q2/issues/63)).
- **Proof at the end**: one photograph before the window ends, live or
  screenshot and labelled as such ([0036](0036-live-or-screenshot.md)), voted on
  like any proof. A screen-time screenshot is the typical case. The window takes
  it at any time during the period, and it only settles the window when the
  period ends.
- **Honest only**: the limit and the relapse rule, with nothing to vote on.

### On screen

- The create sheet opens with "Ich will …" / "Ich will nicht mehr …". For a
  Don't, the second step is the limit (a stepper, 0 shown as "Nie"), the
  period and the proof mode. A Don't has no daily reminder. Five Don't ideas
  join the six Do ideas.
- A Don't is told apart without a new colour (0015/0028): a shield instead of
  the camera, "Höchstens 3× pro Woche" instead of a rhythm, "1 von 3 genutzt"
  or "Bisher durchgehalten" instead of "Heute fällig", "Durchgehalten" instead
  of "Geschafft". Where a Do has a ring and a streak, a Don't has a run:
  "Seit 12 Tagen", counted from `RunStartedOn` (the day after the last relapse
  or miss), with "Ab morgen" on the day of a relapse.
- On the start screen, Don'ts are listed below the Dos, all of them. They are
  not part of the "heute geliefert" ring, because they are not delivered during
  the day, and they are never the "Als Nächstes" card.

## Consequences

- `GoalInstanceStatus.Relapse`, `GoalKind`, `DontProofMode`, `DontUse` and the
  `DontsAndRelapses` migration. `BalanceResponse`, `GoalResponse`,
  `GoalInstanceResponse` and `GoalEventResponse` carry the new numbers, and the
  contract is regenerated.
- Seeds have Don'ts in every profile: a proof-at-the-end Don't and a weekly
  limit in the demo world, and a weekly Don't with a relapse in AutomatedTest
  and E2E. They are weekly so the suites do not depend on the weekday.
