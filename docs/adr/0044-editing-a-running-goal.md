# 0044 — A running goal is edited in front of the people who check it

**Status:** Accepted
**Date:** 2026-10-18
**Issues:** [#4](https://github.com/AaronGreiner/q2/issues/4)
**Amends:** [0027 — Every goal is checked in its own conversation](0027-goal-conversations.md),
whose membership could not change, and
[0037 — Don'ts](0037-donts.md), whose limit was fixed once it ran.

## Context

- **A running Do or Don't could not be changed**, apart from its reminder
  ([0032](0032-the-next-step-first.md)). Somebody who wanted a second friend to
  check them, found three runs a week too many, wanted to lower a Don't's limit
  once it worked, or made a typo in the title had to stop the goal and start
  again — and lost its streak and its conversation with it.
- **An edit must not become the quiet way out.** q2 is an accountability app:
  making a goal easier is exactly what the people checking it agreed to watch
  for. A pause already has an answer to that — announced, open to anonymous
  objection, scarce ([0020](0020-pause-and-archive.md), `PauseRules`).
- **A window keeps the terms it opened with.** Its deadline, its required
  proofs and a Don't's limit are copied onto it when it opens
  ([0016](0016-windows-instead-of-steps.md), [0037](0037-donts.md)), so the
  history says what each window was held to.

## Decision

### Only the owner edits, and every edit leaves a row

`PATCH /api/goals/{id}`, for the owner only; anybody else gets the 404 every
owner-only endpoint gives. The request names what the goal should become; a
field left out stays as it stands — or as the change already waiting would
leave it, so editing the title tomorrow does not take back today's schedule
change. The kind, the owner, and everything that already happened (windows,
photographs, votes, uses, spot checks) are not part of an edit. A one-off stays
a one-off and a repeating goal keeps repeating (`kindFixed`). A stopped goal is
not edited; restarting one is [#102](https://github.com/AaronGreiner/q2/issues/102).

Every edit is a `GoalChange` row with what changed **from what to what**: the
goal only remembers the latest answer, and the conversation has to say what it
was before. `GoalTimeline` derives the conversation's lines from these rows —
the edit, and later that it applied, was lifted or was taken back — so nothing
is written as a message ([0027](0027-goal-conversations.md)).

### Two halves: now, and with the next window

| Applies at once | Waits for the next window |
| --- | --- |
| title, icon | schedule, a Don't's limit and proof mode |
| a checker added | a checker removed |
| a one-off's earlier day | a one-off's later day |
| | the description |

Nothing in the first column lets the owner off anything. Everything in the
second changes what a window is held to, who judges it, or — the description
([#95](https://github.com/AaronGreiner/q2/issues/95)) — the agreement
photographs are judged against, so it waits for the old window to end.

### Easier waits a day and can be objected to; stricter just waits

`GoalChangeRules` decides whether the waiting half makes the goal easier. It
leans one way on purpose: **anything the app cannot show to be stricter counts
as easier.**

- A Do's schedule is compared by required proofs per week (a monthly quota over
  the average month). Fewer is easier, and so is the same number in another
  shape — Monday, Wednesday and Friday is not provably harder than three times
  a week.
- A Don't is compared by uses allowed per day; more is easier, and so is the
  same allowance over another period. A proof mode towards `HonestOnly` is
  easier, towards `SpotCheck` stricter.
- A later day, a removed checker and any changed description are always easier.

An easier change is **objectable**: the checkers are told (`GoalChangeProposed`
— that it exists, never what it is), see it on the goal and in its
conversation, and can object anonymously, with a toggle, exactly as to a pause:
**at least two objections and more than a third of the checkers**
(`PauseRules.IsOverturned`). Silence is consent. Lifted, it never applies and
the owner hears so from nobody in particular (`GoalChangeLifted`).

**With a single checker the objection can never carry.** The gap is accepted,
as it is for a pause and for the vote: one person's objection could not be
anonymous, and a verdict between two people with nobody else asked is worse
than none. The change is still announced and still in the conversation.

### From when

A waiting change applies **between windows**, inside `GoalMaintenance.Advance`
— idempotent and catching up like everything else there:

- from the first day the old window no longer covers, or, for an easier change,
  the first day that starts once **24 hours** have passed since the edit —
  whichever is later;
- but only if that comes before the next window the old terms would open; if
  not, that window opens on the old terms and the change waits behind it.

So a daily goal edited at 23:55 still leaves its checkers a day, a weekly one
edited on Sunday applies the Monday after next, and a stricter change from
"every other day" to "every day" applies tomorrow. The open window keeps its
deadline, its proofs and its limit; the streak is counted from the windows as
always and is neither reset nor rewritten. After a change the new terms count
from the day it applied and never reach back before it.

**A one-off has no next window**, so its change applies into the window it
has once the 24 hours are over — never while a photograph in it is being voted
on. An earlier day moves its deadline at once. A later day can only be asked
for while the deadline is at least a day away (`tooLateToMove`), or it would be
the way past the deadline that is about to pass.

**One change waits at a time.** A new edit with a waiting half replaces it; an
edit that asks for the goal as it is takes it back; the owner can also take it
back on its own (`DELETE /api/goals/{id}/change`). Stopping the goal drops it.
The day it will apply is worked out with the same windows and shown on the
goal (`appliesOn`).

### A changed period opens a pro-rated window

When a change moves the period — three a week becoming ten a month, a daily
goal becoming a weekly quota, a Don't counted per week becoming per month — the
new schedule's first period began before the old window ended. That window
starts the day after instead and asks for **its share, rounded up**: proofs at
least one, a Don't's limit at least zero (`GoalSchedule.NextWindow`,
`GoalSchedule.Prorate`). With an unchanged schedule periods line up and nothing
is cut. The same rule could settle the first window of a new goal
([#96](https://github.com/AaronGreiner/q2/issues/96)), which is still open.

### Checkers and the conversation

- **Adding** is immediate and needs a friend, like at creation: the newcomer is
  put into the goal's conversation (read up to now, so the history is not a
  badge of a hundred lines), receives `GoalInvitation`, and votes from that
  moment — on a photograph already being voted on too.
- **Removing** waits like every easier change, and never removes the last
  checker of a goal that has one. It applies only between windows, and a window
  opens only once the previous one is resolved, so the number of voters never
  changes under a running vote.
- **A removed checker loses access entirely**: they leave the conversation and
  no longer see the goal, as if never invited; their messages and votes stay
  for the others. The goal's participants change inside `Advance`, which
  cannot reach a conversation, so `GoalConversations.RemoveFormerCheckersAsync`
  asks the database afterwards what no longer matches — after reads that moved
  a goal, after an edit or an objection, and on every maintenance pass.

### The interface

"Bearbeiten" in the goal's "…" menu opens `GoalEditSheet`: the create sheet's
four steps without the kind, starting from the goal as the waiting change will
leave it. Its last step asks `POST /api/goals/{id}/change/preview` — the same
`Goal.PlanEdit` that saving goes through — and says what applies now, what
waits and from which day, and before an easier change is saved, that the
checkers can object and until when. A waiting change shows on the goal as
`GoalChangeBanner`, grey like the pause banner, with "Zurückziehen" for the
owner and the objection for a checker; the conversation's line lists what
changed and carries the same objection.

## Consequences

- Making a goal easier costs a day and happens in front of everybody on it;
  making it harder costs nothing. Rewriting the description always goes
  through the checkers, even a typo fix — the price of not guessing.
- A goal with one checker can be made easier without anybody being able to stop
  it, only to see it. That is the accepted gap above.
- Every place that loads a goal for `GoalMaintenance` loads its changes too; a
  query that forgot them would open the next window on the old terms. The
  integration tests drive the nightly job to catch that.
- A removed checker can still see the conversation for the moments between a
  change applying somewhere other than a goal read and the next maintenance
  pass; the goal's own lines and the chat list row are hidden from them at
  once, because both are read through goal membership.
