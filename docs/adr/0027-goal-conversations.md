# 0027 — Every goal is checked in its own conversation

**Status:** Accepted
**Date:** 2026-09-21
**Amends:** [0018 — A window is closed by other people](0018-proof-and-vote.md),
where a goal nobody shares confirmed its own photographs, and
[0020 — The exits](0020-pause-and-archive.md), where a chat was *pointed at* a
goal rather than belonging to it.

## Context

In Qdos, the product q2 took over, a to-do **was** its chat: making one meant
inviting friends, which opened a group conversation, and the proof photo, the
vote and every "missed" arrived there. q2 kept the goal and the conversation
apart. A conversation could be pinned to a goal, but only the API and the seeds
ever did that — the create sheet did not even ask who the goal was for. Every
goal made in the app therefore had nobody on it, and 0018's rule for that case
applied to all of them: the photograph was believed the moment it arrived.
The part of q2 that makes it q2 — friends checking — could not be reached from
the interface at all.

Two constraints came with the fix. q2's free conversations stay: the takeover
paper called removing them the one change it would consider a real mistake,
because they are what a new account has on its first day
([#26](https://github.com/AaronGreiner/q2/issues/26)). And what happens to a
window while nobody is looking already runs through `GoalMaintenance`, a pure
function over one goal that must stay pure.

## Decision

**A goal needs at least one friend.** `POST /api/goals` refuses an empty
`participantIds`, and one that lists only the owner. The create sheet asks
"Wer prüft dich?" and, for somebody with no friends yet, shows the invite link
instead of a form that could only be refused. Goals from before this rule keep
working exactly as they did — with nobody on them, no conversation, and a
photograph that confirms itself — because databases hold them; nothing new can
be made that way.

**Every goal somebody else is on has exactly one conversation of its own.** A
new `ConversationKind.Goal`, opened in the same save as the goal
(`GoalConversations.Open`), whose members are the owner and everybody invited —
one rule, used by goal creation, the seeds, and the maintenance pass that opens
the missing conversation of an older goal. A unique index on `GoalId` makes
"one" a property of the database.

- **Its own thread even with one friend.** Two people have one direct
  conversation; two goals with Lena would otherwise share one history.
- **Named after the goal, stored nowhere.** Like a direct chat's name, the title
  and the icon are read from the goal, so they cannot go stale.
- **It cannot be left, only muted.** Membership is the goal's. Leaving would
  quietly take a vote off it — and the last friend leaving would turn it back
  into a goal that confirms itself. Changing who is on a goal is
  [#4](https://github.com/AaronGreiner/q2/issues/4).
- **It goes with the goal.** Deleting a goal from the archive, and erasing its
  owner's account, remove it explicitly. The foreign key stays `SetNull`:
  moving it to `Cascade` would make SQLite rebuild the table with foreign keys
  switched off, which `MigrationTests` forbids — and a goal's conversation
  without its goal is shown to nobody.
- **A free conversation is never about a goal.** `CreateGroupChatRequest` lost
  `goalId`; the migration clears the reference on every conversation that had
  one, because each of those was a free chat pinned to a goal. Their messages
  stay where they were.

**What happened to the goal is read off the goal, never written into the
thread.** `GoalTimeline` derives, from the windows, photographs and pauses that
already exist: the goal being created, each photograph (drawn with its vote),
each window kept (with the streak it reached) or missed (with how far it got), a
pause starting (with its last day — never its reason, as 0020 already said of
the chat) and ending, and the goal being completed or stopped. `ChatDetail`
carries these as `events` beside `messages`; the client puts the two side by
side by time.

Derived rather than stored, like every other number in q2:

- a line in the thread cannot disagree with the goal it is about;
- a goal from before this change shows its whole history in the conversation
  the maintenance pass opens for it;
- `GoalMaintenance` stays a pure function over the goal. Writing messages from
  it would have made every read path that advances a goal a place that also has
  to post into a chat, idempotently.

**Voting happens in the thread, with the same rules and the same endpoint** as
on the vote screen: the card is `ProofResponse`, described through
`ProofService.Describe` from the same narrow set of people — uploader and
confirmers, never a doubter. The owner gets the camera in the composer while
the open window takes a photograph (`acceptsProof`).

**The chat list is three sections**: your goals (you deliver), friends' goals
(you check), and conversations (about no goal). A row says in the accent when a
photograph is waiting for the reader's verdict — something they can do now,
which "unread" is not — and shows the newest event when that is newer than any
message. Unread still counts only what people wrote; a missed window is not a
message and does not ring.

## Consequences

- The product's core loop is reachable from the interface: make a goal in
  front of somebody, deliver the photograph where they are looking, have them
  believe it there.
- A new account cannot make a goal until somebody has followed its invite link.
  That is the price of the rule, and it is the same one Qdos paid; the daily
  challenge remains the thing to do on the first day
  ([0021](0021-daily-challenge.md)).
- The thread of a long-running daily goal grows by a photograph and a line a
  day. Nothing is paginated yet — neither are messages — and the day that
  matters it will be both at once
  ([#44](https://github.com/AaronGreiner/q2/issues/44)).
- A goal's thread follows its goal over the live connection: `keysFor` names
  `goal:<id>` for a goal's changes and for a photograph arriving, and the open
  thread reads itself again on that key. A vote that does not settle a
  photograph tells nobody but the voter, so another friend's open thread shows
  it on its next read.
- The seeds say the same thing the app does: every goal in the demonstration
  world has somebody on it and a conversation, and two of them belong to
  friends, so both halves of the list have something in them. The test seeds
  keep goals nobody is on, standing for the ones databases still hold.
