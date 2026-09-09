# 0018 — A window is closed by other people

**Status:** Accepted
**Date:** 2026-09-07

## Context

[0016](0016-windows-instead-of-steps.md) gave q2 a window that can be missed.
What it did not give it was anybody to miss it *in front of*: the window was
still closed by its owner pressing a button, which is the self-report the whole
migration exists to replace.

`QDOS-UEBERNAHME.md` puts it plainly in its first table. The two products differ
on one question — **who decides whether something was done** — and every other
difference follows from it. In q2 it was you. In Qdos it is your friends, and
that is why Qdos can say "verpasst" and mean it.

This is the stage the plan marks *"Hier ist das Produkt zum ersten Mal Qdos."*

## Decision

**A window is delivered by a photograph that other people believe.**

### `ProofVoting` — the rules, as a pure function

Ported from the source project's `domain/voting.ts` and kept as a static class
over a list of votes, for the reason it was pure there: it has to be testable on
its own, and it has to be the *server's* answer. A client may run the same
arithmetic to show a result immediately; nothing it computes is binding
(section 7e of the plan).

Four numbers carry it, and each one answers a way the vote could be gamed or
stalled:

| constant | value | what it prevents |
| --- | --- | --- |
| `DoubtThreshold` | more than 1/3 | a tie reading as a rejection |
| `MinDoubtVotes` | 2 | one person sinking a friend's proof alone — in any pair, one doubt is already half |
| `MaxAttempts` | 2 | a bad photograph costing a streak that was actually earned |
| `VotingWindowHours` | 12 | one inattentive friend freezing a daily goal forever |

Two rules about *how* it decides are worth stating separately, because both are
counter-intuitive and both are deliberate:

- **Dispute is checked before agreement.** Once enough doubt has arrived the
  outcome cannot change, so waiting for the last vote would be a slower answer
  rather than a fairer one.
- **Silence is not mistrust.** After the deadline only the votes cast count, so
  a photograph nobody objected to is confirmed — including one nobody looked at.
  The alternative lets an inattentive friend break a streak by doing nothing.

### Nobody to ask means nobody to convince

A goal shared with no one confirms its own proof the moment it arrives. That is
the self-report q2 has always had, kept for the one case where a vote would be
ceremony: waiting twelve hours for a photograph no friend will ever see.

It is expressed as `expectedVoterCount <= 0` rather than "is this a group",
because the question is always *is there anybody to ask*.

### Doubt is anonymous, and it is enforced by not loading it

Confirmations come back with names; doubts come back as a number. If a doubter
could be named, most people would confirm out of politeness and the whole check
would be theatre.

The enforcement is not a filter in the mapping — it is that
`ProofService.LoadPeopleAsync` selects the ids of uploaders and *confirmers*
only. A doubter's name is never fetched, so no later change to the response
shape can leak one. `ProofsEndpointTests` asserts the id and the handle are
absent from the raw payload, not merely from the typed one.

### The consequences live on the window, not in the service

`GoalInstance.ApplyProofOutcome` is the one place a verdict becomes a fact.
A confirmed proof counts towards the quota and may close the window; a rejected
one ends the window immediately rather than leaving it to run out, because two
separate majorities have already said the thing did not happen.

Attempts are counted from the last *believed* proof rather than from the start
of the window. On "three times a week" that is what makes each delivery its own
chance: two friends doubting Monday's photograph must not use up Wednesday's
retries.

### The deadline is the maintenance job's

`GoalMaintenance` gained a step, and it runs *before* the miss check: a
photograph delivered at 23:00 has twelve hours to be believed, and its window's
own deadline may pass in the middle of that. Missing the window first would fail
somebody who had in fact delivered on time.

This is what makes the stage's completion criterion true — the outcome is
settled after the deadline whether or not anybody has the app open — and it is
why the worker now loads goals with their proofs and their owners: a vote
confirmed overnight still has to count a day towards the owner's streak.

### A proof photograph's audience is its goal's audience

`ImageService.CanRead` could not answer for a proof, because the question is
about a goal rather than about the picture. `CanReadAsync` does, with a query:
the owner and the participants, and nobody else. Not "everybody signed in" —
that is an avatar's reach, and a photograph of somebody's living room is not
public the way initials on a colour are. Not "friends" either: a friendship is
not an invitation to every goal.

A proof stays readable after its vote closes, because the history is unreadable
otherwise.

## Consequences

**The self-report is gone, including from the seeds — almost.** A seeded world
has to contain delivered windows and cannot contain the photographs that
delivered them, because a seed is a pure function and may not write files. So
`Goal.SeedDeliveredProof` exists, is `internal`, and is the only door of its
kind. It is also what a real window looks like once its proofs are deleted.

**Reactions are a second table, on purpose.** `ProofReaction` duplicates the
shape of `MessageReaction` rather than merging into the plan's generic
`Reaction(TargetId, …)`. Two near-identical types is normally a smell; one
shared column pointing at two tables is a foreign key to nothing and a branch in
every reader. The merge is the right shape when the daily challenge adds a third
target (stage 7), and not before.

**"Kein Nachtreten" is a property of the state.** All three reaction kinds are
approving, so a reaction can never be an insult on its own — and a *rejected*
photograph accepts none at all. That is enforced in the service, not left to the
client to hide a button.

**Everything downstream now waits.** "Today's progress", the streak, the balance
and whether a goal is still on today's list all move on a *confirmed* proof
rather than on a delivered one. Several tests had to learn the difference, and
that is the change working rather than the change breaking them.

**One thing this stage does not do:** it publishes nothing to the feed when a
photograph is doubted. A miss is somebody else's to notice from the balance, not
an announcement. The considered version of telling friends is stage 5's evening
warning, and it belongs there rather than as a side effect here.
