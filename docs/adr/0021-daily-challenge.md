# 0021 — The daily challenge: one prompt, and nothing at stake

**Status:** Accepted
**Date:** 2026-09-08

## Context

Everything from [0016](0016-windows-instead-of-steps.md) to
[0020](0020-pause-and-archive.md) has built one thing: a promise you can fail.
A window has a deadline, a photograph is judged by other people, a miss shows in
the balance, and the exits had to be added because there were none.

That product works, and it has a gap that is easy to miss. Nothing in it is
worth doing unless you already have a goal and already have friends on it. A new
account sees an empty list, and somebody having a bad month sees only what they
owe.

`QDOS-UEBERNAHME.md`, stage 7, asks for the counterweight the source project
built for exactly that: a daily challenge — a prompt everybody gets at the same
time, a room made of your own friends, covered until you take part, and an
archive of your own contributions. Its completion criteria are four assertions:
a week of prompts can be entered in advance, the room shows only friends, it is
covered before your own contribution, and your own contributions are in the
archive afterwards while other people's are not.

## Decision

### It pays into nothing, and therefore nobody votes

A challenge counts towards no streak, appears in no balance and knows no
"missed". Whoever joins in gains something; whoever sits it out loses nothing,
and is not told about it.

From that follows the decision that shapes the whole feature: **there is no
vote.** Doubting exists in this product for one purpose — to stop a streak being
won with a borrowed photograph ([0018](0018-proof-and-vote.md)) — and with no
streak there is nothing to protect. A contribution is seen and applauded, never
examined.

`ChallengeEntry` is therefore not a `ProofPhoto`. That type carries a status, a
second attempt and a voting deadline, all three of which would be meaningless
here; a challenge is not a `Goal` either, because a goal has one owner who
delivers and invited friends who judge, and a challenge has any number of people
who deliver and nobody who judges. Modelling it as either would put a "not
here" branch through every rule the vote has.

### The room is covered until you are in it, and that is enforced by what is sent

`Challenge.RevealsTo` is the rule: your friends' pictures arrive once you have
contributed one yourself. Without the hurdle the room fills with spectators — a
few people showing themselves to a silent majority, which is the gradient the
rest of the product avoids.

The source project drew this as a CSS blur over the real photographs. Here the
image ids are **left out of the response** until the viewer has contributed, and
`ChallengeEntryCard` draws a placeholder rather than blurring something it was
given anyway. A blur over bytes that were sent regardless is a curtain with a
gap in it.

The picture endpoint applies the same test (`ImageService.CanReadAsync`, purpose
`ChallengeEntry`), so having an id is never the same thing as being allowed to
look. That half is what makes it a rule rather than a habit.

What is *not* held back is who is already in: the author, their avatar and the
time travel with a covered entry. Seeing who has taken part is the reason to
join, and it costs nobody anything.

### Everybody sees a different room

`ChallengeService` composes the room from the viewer's own accepted friendships
and nothing else. There is never a public surface, so there is nothing to
moderate — the same reasoning the source project put in `getChallengeRoom`, and
the third of the six scopings the plan puts on the server rather than in a
screen.

The archive is the same rule from the other end, and stronger: it starts from
the viewer's own contributions rather than from the challenges, so other
people's pictures are not in the result to be filtered out and there is no
filter to forget. The room is deliberately transient. Nobody gets to build a
lasting collection of other people's pictures — whoever could keep them could
also pass them on — while their own memory is untouched.

### Configuration is the editorial desk, and the queue is rows

The plan's phrase is "**kein täglicher Redaktionsdienst**", and the queue is how
it is met. `ChallengeOptions.Prompts` is an ordered list in `appsettings.json`;
`ChallengeQueueWorker` writes one `Challenge` row per day for the next week, and
a day becomes the current challenge simply because the clock reached it.
Tomorrow's prompt exists tonight.

The two alternatives were both worse. An admin screen needs a role, and q2 has
none ([0011](0011-authentication-with-identity.md)). A job that invents a prompt
every night is a daily editorial shift by another name.

`ChallengeQueue.Plan` is pure, for the same reason `ProofVoting`, `GoalRisk` and
`PauseRules` are: it decides something while nobody is looking. It is
idempotent — it is told which days already have a row — and **stable**: which
prompt a day gets is a function of the date
(`prompts[day.DayNumber % prompts.Count]`), so re-planning never rewrites a day
and adding a prompt shifts only days that are not queued yet. It never fills in
the past; a prompt nobody was offered has no business in anybody's archive. An
empty list turns the feature off cleanly.

### A challenge belongs to a day in the deployment's zone

Every deadline in this product is counted in the *owner's* zone, because a
deadline that decides somebody's streak has to be theirs
([0016](0016-windows-instead-of-steps.md)). A challenge decides nothing, and
"the task of the day, for all of us at once" is worth more than a prompt that
turns over at a different moment for each reader. It is one row for everybody,
running from local midnight to local midnight in `Q2:TimeZone`, and `Day` is
stored beside the two instants with a unique index on it — one prompt per day is
an invariant rather than a convention.

The one seam this leaves is that seeds compute their days in UTC, so a seeded
challenge and a queued one can overlap by a few hours near a zone boundary.
`ChallengeService` takes the most recently published match rather than insisting
on exactly one, which makes that a harmless off-by-an-evening instead of an
exception on somebody's start screen.

### A third reaction table, against the plan's advice

The plan proposed generalising reactions to one row with a bare `TargetId` once
a third kind of target appeared, and `ProofReaction` says here is where that
would happen. It was not done.

A shared `TargetId` is a foreign key to nothing. Today each of the three tables
has a real one and cascades with what it hangs off, so a deleted message, a
deleted photograph and a withdrawn contribution take their reactions with them
without anybody remembering to. One shared table would trade that for three
manual clean-up paths and a column the database cannot check — a poor trade for
one table fewer, in a codebase whose rule is that invariants live in the model.

All three kinds stay approving, which is what lets reactions be offered here at
all: with no verdict in the room, a reaction that could be negative would be the
only way to be unpleasant in it.

### One contribution per person, replaceable, withdrawable

A second picture replaces the first rather than adding to a gallery — the
challenge is a moment, not a collection — and the replaced picture is deleted
with it, so a second contribution is not a way to spend a storage allowance
twice. Replacing drops the reactions: applause belongs to the picture it was
given to, and carrying it over would leave somebody's "stark" standing under a
photograph they never saw.

Withdrawing takes the picture *and* the view: the room is covered again. The
reciprocity rule has to read the same in both directions, or an exit would be
the spectator's way back in.

## Consequences

- **The start screen has something on it for somebody with no goals.** That was
  the gap this stage exists to close, and it is also the answer stage 8's
  onboarding will build on.
- `ImagePurpose` gained a third member, and it is the narrowest of the three:
  the only one whose audience depends on what the *viewer* has done. Adding it
  forced the decision in `CanReadAsync` rather than letting it default to
  visible, which is what that enum is for.
- **A second background service.** `ChallengeQueueWorker` runs hourly and, like
  `GoalMaintenanceWorker`, is not registered in `AutomatedTest` — a job writing
  between an arrange and an assert makes tests flaky for reasons unrelated to
  what they check. The seeds carry a challenge so the room exists there; the
  worker's own tests drive `RunOnceAsync` directly.
- **Seeded worlds have a prompt but no contributions.** A seed writes no image
  bytes ([0017](0017-image-storage.md)), and a row pointing at a picture that
  does not exist is a broken image on every screen that shows it. What a seed
  can honestly contain is the prompt — which is also the state a real morning
  starts in.
- The challenge is not in the tab bar. It is reached from a banner on the start
  screen, and the banner stays after contributing rather than disappearing: the
  friends' counter is the reason to look in again in the evening.
- Nothing about the challenge is in the weekly balance, and that is deliberate
  even though the source project counted `challengeDays` there. A number that
  goes up for taking part is a scoreboard by another name, and this is the one
  part of q2 with nothing at stake.
