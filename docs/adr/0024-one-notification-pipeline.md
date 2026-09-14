# 0024 — One notification pipeline: the bell, a live connection, and push

**Status:** Accepted — amended by [0025](0025-banners-in-the-open-app.md): the
live connection also carries the notification itself to somebody who is
looking, and the open app shows it as a banner instead of their phone ringing.
Everything else stands as written.
**Date:** 2026-09-14

Amends [0019](0019-warning-and-balance.md) — the bell now carries a count, and
the evening warning left the feed — and [0023](0023-web-push.md): the payload,
the switches and where a notification comes from. The reasoning in German, with
the alternatives that were weighed, is in `BENACHRICHTIGUNGEN.md` at the root.

## Context

After [0023](0023-web-push.md) two things in q2 arrived without the app being
open: the evening warning and the daily challenge. Everything else somebody
would want to know — a message, a friend request, a friend's photograph waiting
for their verdict, the verdict on their own, kudos, being put on a goal, a
friend setting one aside — could only be found by opening the right screen at
the right time. The chat and request badges rode along on the profile read, so
they changed when somebody navigated and not when something happened.

The settings screen had five switches. Three governed nothing, one of them for a
weekly review that did not exist.

Adding kinds one at a time to 0023's path would have meant each feature deciding
for itself who is told, in which words, and by which route — a push here, a
badge there, a feed row somewhere else. That mix is the thing to avoid.

## Decision

### One pipeline, called by the action that causes it

`Notifier` is the only way anybody is told anything. The service that performs
an action stages the event before its `SaveChanges`, so the bell's rows are
written in the same transaction as the thing they are about, and flushes after
the commit. Flushing hands a delivery to a bounded in-memory queue, and
`NotificationDeliveryWorker` sends it live and by push. Nothing is sent inside a
transaction and a failed delivery never fails the action — 0023's rule, applied
everywhere rather than in two workers.

Deadline verdicts go through `ProofVerdicts.AdvanceAsync` both on reads and in
maintenance. Opening or changing a goal before the worker runs therefore keeps
the verdict notification, and a later maintenance pass does not repeat it.

Who is never told is decided once, when an event is staged: not the person who
caused it, and nobody on either side of a block
([0022](0022-blocking-reporting-and-erasure.md)). The bell filters blocked
actors again when it is read, because a block can come after the line.

### Three routes for one event

- **The bell** — a row in `Notifications`, for what has no other home.
- **Live** — a SignalR connection at `/api/live` while a page is on screen. It
  carries fresh badge counts and "this part changed", never content.
- **Push** — Web Push exactly as in 0023, for somebody who is not looking.

### One place per event

A message lives in its chat, a friend request on the search screen, a
photograph to vote on behind the start screen's banner, the challenge in its
room — each with a count of its own. The bell keeps what has nowhere else to
be: a new friendship, a verdict, kudos and reactions, an invitation, a pause and
its end, a friend about to miss. Nothing shows up twice as something to clear.

`NotificationRules` holds that list with the switch and the live area of every
kind, and it throws for a kind nobody decided.

### The bell has a count

0019 left the bell without a number because it opened the friends' feed, and a
count on friends' good news turns their doing into something to clear. The bell
now opens what concerns *you* — a verdict, an acceptance, somebody's kudos — and
a number on that is the same honest count the chat tab has. The feed keeps no
count and is reached from "Alle anzeigen" under itself.

"Seen" is one timestamp per person, moved by reading the bell, the way opening a
chat marks it read. What was new when the bell was opened stays marked new until
somebody leaves it, although every later read — the live connection catching
up, another line arriving — answers "seen" for all of it. Several reactions to
one thing are one line ("Lena und 2 weitere"). A person is counted once however
often they change their mind between the three kinds, and taking a reaction back
takes back a line not yet seen. Lines are kept thirty days.

### The warning leaves the feed

`WindowAtRisk` is no longer a kind of activity, and the migration deletes the
rows that were, with their kudos. The warning is a line in each friend's bell
and a push to their devices.

The feed is a record of what friends did. A warning is something else: it is
addressed to particular people about tonight, and it is stale by morning. In
the feed it needed a special case — a row without a kudos button, so that "stark
gemacht" could never be said under "droht zu verpassen". Now there is simply no
row to hide the button on, and "kein Nachtreten" is a matter of structure rather
than of remembering.

### A switch decides what may interrupt, not what may be found

Eight switches, one per thing somebody would decide separately: messages,
friendships, votes due, verdicts on your proofs, kudos and reactions, invitations
and pauses, friends about to miss, the challenge. They govern **push**. The bell
keeps everything whatever is switched off, the same way the message switch never
took a message out of a chat, and the screen says so.

The weekly review is gone. Its column was reused as `NotifyVotesDue` and set to
on for everybody, because dropping a column in SQLite means rebuilding the table
with foreign keys off, and what somebody chose for a feature that never existed
says nothing about votes.

A conversation can be muted. It is the one switch about a single thing rather
than a kind, so it lives in the conversation rather than in the settings, and
muted still counts as unread.

Push is gated in one pure rule, `NotificationRules.ShouldPush`, in this order:
nobody watching, not muted, switched on, outside quiet hours in the recipient's
own zone.

### Watching means connected, and connected means looking

The app holds its connection only while a page is visible and somebody is
signed in, and lets go ten seconds after it is hidden. The server skips the push
for anybody connected. An open tab therefore does not ring on top of what it is
showing, and a phone put back in a pocket rings again.

### The live connection is thin, and it only listens

Server to client only: the hub has no method a client can call. It sends two
events, `counts` (the same `CountsResponse` that `GET /api/counts` returns) and
`changed` (an area and an id). The client answers `changed` by reading again
through the endpoints that already apply every rule about who may see what, so
no rule is written a second time for the socket, and the OpenAPI document stays
the only contract. The hub is excluded from it, and its two event names are
mirrored by hand in `useLiveConnection`.

After reconnecting, mounted reads are refreshed because missed events are not
replayed. Background reads keep the current screen mounted; skeletons only
appear before the first response, so an incoming message preserves a draft and
keyboard focus.

Who is connected is held in memory in the API process. There is one today
([0008](0008-deployment-topology.md)); a second would need a SignalR backplane.

### One shape for a line and a push

`NotificationResponse` is both a line in the bell and the push payload.
`notificationText` in `app/utils/display.ts` writes the sentence for both, and
the service worker imports it, so a lock screen and the bell cannot say one
event two ways. The server still sends parts rather than prose.

For a message the payload now carries its first 140 characters. They are the
sender's words on the way to the person they were written for, encrypted to
that browser (RFC 8291), and they are never stored a second time on the server.
Whether a lock screen shows them is the operating system's setting, which is
where a person already decides that for every other app.

A verdict and a lifted pause never carry a name, because doubt and objection
are anonymous ([0018](0018-proof-and-vote.md)). A pause never carries its
reason.

## Consequences

- **The counts left the profile.** `GET /api/counts` answers for every badge,
  and the live connection pushes the same shape.
- **New surface:** `GET /api/notifications` (reading it marks it seen),
  `GET /api/counts`, `PUT /api/chats/{id}/mute`, and the hub at `/api/live`.
- **One new dependency on each side of the socket.** SignalR ships with
  ASP.NET Core; the browser client is `@microsoft/signalr`, and the integration
  tests use `Microsoft.AspNetCore.SignalR.Client` to connect for real.
- **`AutomatedTest` delivers inline**, so a test can assert on what was pushed
  as soon as the request returns. Everywhere else the queue is in memory and
  drops the oldest delivery when full. A delivery lost to a restart is a push
  not sent; the bell's line is already committed, which is why the line is
  written in the transaction and the delivery is not.
- **A notification goes with either account.** The rows cascade from both the
  recipient and the actor, and a deleted goal takes its lines with it.
- **A block is not announced live either.** The side that blocked is refreshed;
  the side that was blocked is not, because a screen changing under somebody at
  the moment they were blocked would be the announcement.
- **Native push is later.** APNs and FCM arrive with a Capacitor build
  ([next-steps.md](../next-steps.md) item 15). Until then a phone is reached by
  Web Push, which on iOS means an installed home-screen app.
