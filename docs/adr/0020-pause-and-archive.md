# 0020 — The exits: a pause, an ending, and a deletion

**Status:** Accepted
**Date:** 2026-09-08

## Context

[0016](0016-windows-instead-of-steps.md) gave a goal something it can fail, and
[0018](0018-proof-and-vote.md) put the verdict in other people's hands. Together
they left a product with no way out of anything.

Two gaps follow from that, and both are the kind that makes people leave rather
than complain:

- **Nothing can be excused.** Every elapsed deadline was punished the same,
  whether somebody could not be bothered or was in bed with the flu. An
  accountability app that cannot tell those apart is punishing people for things
  they did not choose.
- **Nothing can be stopped.** The only exit was deleting the goal for everybody,
  so anybody who wanted to stop after half a year had to destroy their own
  record to do it. The balance this product is built around was also the thing
  standing in the way of ever finishing with a goal.

`QDOS-UEBERNAHME.md`, stage 6, asks for `GoalPause` with an allowance and an
anonymous objection, a `completedAt`, an archive screen, and deletion from
there. Its completion criteria are the four assertions this record is really
about: a goal reported sick is set aside at once, the window counts as neither
kept nor missed, two objections put it back, and stopping does not worsen the
balance afterwards.

## Decision

### A pause covers whole local days, and costs three things

`GoalPause` runs from the day it was asked for to `EndsOn`, inclusive, ending at
the last instant of that day in the **owner's** zone through `LocalCalendar` —
the same rule as every other deadline here ([0016](0016-windows-instead-of-steps.md)).
A pause measured in hours would make "bis wann darf ich" a question about the
clock somebody happened to press the button at.

`PauseRules` is a pure module ported from the source project's `domain/pause.ts`,
for the same reason `ProofVoting` and `GoalRisk` are: the maintenance job
decides it while nobody is looking, and it has to be testable without a
database. It carries three costs, in this order of effectiveness:

1. **Scarcity — `MaxPerMonth` (2) per goal and calendar month.** This is the
   real brake, and it is the one that works silently: while pauses are scarce,
   nobody has to appoint themselves the invigilator. An overturned pause still
   counts against the allowance, or it could be worked around by asking again.
2. **A reason — `MinReasonLength` (10), read by everybody invited.** Having to
   write an excuse down costs more than thinking one.
3. **An objection.** Last on purpose. Doubting somebody's illness is socially
   expensive, and a mechanism nobody wants to use carries little weight.

The objection threshold is deliberately the same construction as doubting a
photograph — at least two, and more than a third — so one person can never
overturn a pause alone. With a single invited friend a pause therefore cannot be
challenged at all. That is the same gap the vote has there, accepted for the
same reason: a verdict between two people with no second opinion would be worse
than none. Unlike the vote, the share counts against everybody *invited* rather
than against the objections cast: there is no counter-objection, and silence
means consent.

Objections are **anonymous**, and in the same way doubt is: the names are not
withheld from `GoalPauseResponse`, they are never selected into it.

### A pause cannot interrupt a running vote

`Goal.RequestPause` refuses while a photograph is being voted on. Friends who
are halfway through deciding whether they believe something must not have the
question withdrawn from under them, and the outcome they were about to reach
would have nowhere to land. It is one rule instead of an interaction between
two subsystems.

### A set-aside window is `Paused`, which is not a result

`GoalInstanceStatus.Paused` is in neither half of the balance and does not break
the streak — `Goal.Streak` now counts back over `Done` and `Missed` only. It has
no `ResolvedAt`, because nothing about it was resolved.

While a pause runs, `GoalMaintenance.Advance` does nothing at all: no deadline
elapses and no window opens. When it is over, the windows whose deadlines fell
**inside** it are skipped rather than created and missed. Without that, the run
after a week off would manufacture seven failures — exactly what the pause was
granted to prevent. The history is left with a gap, which is what a pause
honestly is.

Ending a pause early stops it covering any *further* days; the days it has
already covered stay covered, today included. Coming back early is a good thing,
and handing somebody the deadline they were excused from would make it a
punishment. Only an objection puts the window back — with its original deadline,
including one that has since passed, which the next maintenance run turns into a
miss. That is the honest consequence of an objection that carried.

### Two exits, one timestamp

`Goal.Close(completed, now)` sets `Status` to `Completed` or `Archived` and
records `ClosedAt`. Two statuses rather than one, because "I carried this
through" and "I am stopping" are different things to have done and the app has
no way to tell them apart — it is the person's own claim. One timestamp, because
"when did this stop" is the same question either way and the archive sorts by
it.

Neither touches the balance, and the open window is **set aside rather than
failed**. An exit that made the record worse is an exit nobody would take, which
is the whole reason this stage exists.

The plan called the field `completedAt`. With two endings the honest name is
when it *closed*; `Status` says which of them it was.

### Deleting is a second decision, from the archive only

`DELETE /api/goals/{id}` refuses a goal that is still running. Stopping keeps
everything; deleting keeps nothing, and separating them is what stops half a
year of record going in one press.

It is also the only way photographs ever really go, so it takes the conversation
with them: the chat is *pointed at* a goal rather than owned by it, so the
database alone would null the reference and leave every proof message in the
thread aimed at a picture that no longer exists. Activity rows carrying the
goal's title go too. Image rows are removed inside the same save and their bytes
afterwards — a row removed with the file left behind is wasted disk, the other
way round is a reference to nothing.

### The archive is its own screen and its own read

`GET /api/goals/archive` returns both endings, most recently stopped first, and
the goals list asks for `?status=Active`. What is in the archive has no
deadline, no camera and no warning; mixing it into the list of things somebody
still owes would make that list longer without making it more useful.

## Consequences

- A window can now end in three ways, and every count in the product had to be
  taught the third. `Streak` and `Balance` were the two that mattered.
- `GoalResponse` gained `isMine`. The server refuses everything an owner-only
  action would do anyway, but a client could not previously tell whose goal it
  was looking at, and a button that always fails is worse than no button.
- The pinned goal in a chat says only *that* a goal is set aside and until when.
  The reason and the objection stay on the goal's own screen: a chat is not
  where somebody should be asked to judge a friend's illness.
- **A pause is invisible to the risk warning**, because a paused goal has no
  open window and [0019](0019-warning-and-balance.md) assesses windows. That
  falls out of the model rather than needing a rule.
- The objection threshold cannot be exercised end-to-end against the automated
  test seed, which has one invited friend — by design, that pause is
  unchallengeable. The threshold is covered by unit tests over `Goal` and
  `PauseRules`; the integration tests cover the single-friend case, which is the
  documented gap.
