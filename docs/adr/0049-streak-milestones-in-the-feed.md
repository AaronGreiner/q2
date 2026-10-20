# 0049 — Streak milestones in the feed, and no percentage anywhere

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#90](https://github.com/AaronGreiner/q2/issues/90)
**Amends:** [0032 — The next step first](0032-the-next-step-first.md), which
listed "the `GoalProgress` feed kind that only the seeds produce" as open, and
[0016 — Windows instead of steps](0016-windows-instead-of-steps.md), whose
"there is no percentage" the feed still contradicted.

## Context

The seeded feed read "Lena Schulz hat einen 7-Tage-Streak erreicht" and
"Lena Schulz ist bei „Frühaufsteher-Challenge“ auf 1 %". Neither could happen
in a real deployment: nothing published `StreakReached`, and `GoalProgress`
was a leftover of the percentage model. A real feed had two kinds of line, and
missed the moment kudos are most for.

## Decision

- **A milestone is 7, 30, 100 or 365 days** of the person's streak
  (`StreakMilestones`). Few and far apart: a line every day would be noise.
- **Reached once per run.** A check-in records what it did to the run it
  landed in — the longest run it touched before, and the run it makes
  (`Streak.GrowthFrom`) — and a milestone is reached by the day that crosses
  it. Days are only ever added, so a run never crosses the same length twice;
  a run that breaks and starts again reaches them again, which is a new
  achievement. A day that joins two runs and carries the result past two
  milestones says only the higher.
- **Published by whoever saves the check-in.** The check-in is made inside
  `GoalMaintenance`, which is pure and publishes nothing, so the person keeps
  what its check-ins did until `ActivityRecorder.PublishStreakMilestones`
  takes it — from the vote that settles a photograph (`ProofService`) and from
  every advance that has the owner in hand (`ProofVerdicts.AdvanceAsync`).
- **`GoalProgress` is gone**, from the enum, the catalogue and the seeds, and
  its rows are deleted by migration `RetireGoalProgress` with their kudos and
  the bell lines pointing at them: a row the enum could no longer read, saying
  something untrue.

## Consequences

- The seeds show only lines the product can produce: their streak lines are
  milestones, and the old percentage lines became deliveries.
- A milestone reached while nobody has the owner loaded — there is no such
  path today — would be lost rather than published late. Anything new that
  calls `Person.CheckIn` has to hand the person to the recorder.
