# 0019 — The warning before, and the record after

**Status:** Accepted
**Date:** 2026-09-08

## Context

[0018](0018-proof-and-vote.md) made a window something other people close. What
it did not do is give anybody a reason to act *before* the deadline, or a place
where a missed one shows afterwards.

`QDOS-UEBERNAHME.md` calls this "die Shame-Hälfte", and its own note about it is
the important part: the effective half of "do it or shame it" is not behind the
deadline but in front of it. Somebody who knows their friends are about to hear
that it is getting tight can still act. Somebody who finds out afterwards can
only regret it.

The stage has two completion criteria, and both are one bad decision away from
being harmful rather than merely useless:

- a friend has to find out **in the evening** that somebody is about to miss —
  at most once per window, never before 20:00;
- somebody else's profile has to show "erledigt · verpasst" for **shared to-dos
  only**.

## Decision

### `GoalRisk` — three rules that keep a warning from becoming noise

Ported from the source project's `domain/risk.ts`, pure for the same reason
`ProofVoting` is: the decision belongs to a job that runs whether or not
anybody has the app open.

1. **Late.** Nothing before `AlertHour` (20), which is where "later today"
   becomes "probably not today". Somebody who has delivered nothing by lunchtime
   is not failing, they are having an ordinary day.
2. **Once.** `GoalInstance.RiskNotifiedAt` is the key, so "at most one per
   window" is a property of the window rather than of a job that would otherwise
   have to remember between runs. The job ticks every ten minutes; without this
   an evening would carry sixty warnings.
3. **Only when something is actually missing.** A photograph under a running
   vote counts as delivered. Warning about the person who has just handed in is
   the fastest way to teach everybody to ignore this.

The hour is **the owner's**, read through their `LocalCalendar`. Using the
server's would warn half of Europe at nine in the morning, and the moment a
warning arrives at the wrong time it gets muted — after which it never works for
anything again.

Two reasons, not one, because they are different situations: `LastDay` is
running out of time, `Tight` is running out of room (only reachable on a quota,
where "3× diese Woche" can be arithmetically lost days before the deadline).

### The warning is published by the job, never by a read

`GoalMaintenanceWorker.WarnIfAtRisk` writes an `ActivityKind.WindowAtRisk` row.
It is not in `GoalMaintenance`, and that split is deliberate: advancing a goal is
a pure function over the goal, while a warning is a row written for other people
to read. Keeping the pure part pure is what lets the window arithmetic be tested
without a database.

It also never runs on a read path. A warning is for the person's *friends*, so
tying it to the owner opening the app would let the one person whose attention
is not in question decide whether anybody else hears about it.

### There is no "window missed" entry, and that is the rule

A finished failure is **counted, never announced**. The balance is where it
shows, and that is the only place it should. Publishing it would be the one
thing this product is not allowed to do to somebody.

The same principle shapes the row that *is* published: a warning gets no kudos
button. All three kudos are approving, so the control could never be an insult
on its own — but "stark gemacht" under "droht zu verpassen" is a sentence nobody
should be able to send, and the cheapest way to be sure is not to draw it. It is
a property of the row's kind (`isWarning`), never a setting.

### A balance is counted over the subject's own goals, seen by somebody let in

`GET /api/people/{id}` returns a balance over the goals **the subject owns and
the viewer is a participant of**. `SharedGoals` comes with it, because "0 · 0"
means two very different things — nothing in common, or a clean record — and
without the count a stranger's profile would read as spotless.

**The obvious rule here is wrong, and it took looking at a running screen to
see it.** "Goals either of us owns and the other is on" reads as the fairer,
symmetric version and passes the first test anybody writes. It puts the
*viewer's* windows under the *subject's* name, which only becomes visible from
the third angle: two people both invited to a third person's goal. There is now
a test for exactly that angle.

Everything else on that screen — name, handle, streak, kudos — stays visible to
anybody signed in, because a search result already shows it and a profile that
was emptier than the result behind it would be strange rather than private.

## Consequences

**The feed has two kinds of row now, and the overview separates them.** `/vote`
was stage 4's new screen; `/activity` is this one's, reachable from a bell on
the start screen. Warnings sit above everything else rather than interleaved by
time: a warning has a deadline tonight and a friend's good news does not, and
recency alone would bury the one row still worth acting on.

The bell carries **no badge count**, on purpose. A number on it turns "have my
friends done anything" into something to clear, which is the mechanic this
product is trying not to be.

**The clock is now a test fixture.** `FixedTimeProvider` gained `Set`/`Reset`,
because the evening rule and the voting deadline are the two things in q2 whose
subject *is* the time of day. `Q2ApiFactory.ResetAsync` puts it back, so a test
that fails halfway through does not leave every later one at half past eight.

**One thing is still missing and is stage 9's.** Nothing is delivered anywhere:
the warning is a row in a feed somebody has to open. The rule, the audience and
the wording are settled here, which is the point — push is a delivery mechanism,
not a redesign.
