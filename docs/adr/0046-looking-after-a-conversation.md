# 0046 — Looking after a conversation: correcting, taking back, reporting, pinning, and its photographs

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#25](https://github.com/AaronGreiner/q2/issues/25),
[#104](https://github.com/AaronGreiner/q2/issues/104),
[#24](https://github.com/AaronGreiner/q2/issues/24)
**Amends:** [0029 — Photographs in conversations](0029-photographs-in-conversations.md)
and [0043 — A thread is read a page at a time](0043-threads-a-page-at-a-time.md).

## Context

A message, once sent, stayed as it was: a typo could not be fixed, and a
message sent to the wrong thread could not be taken back. Somebody else's
abusive message could only be answered by reporting the whole person from
their profile, which loses what was said and where. And a conversation's
photographs had no place of their own. Threads are read a page at a time
(0043), so finding last month's picture meant scrolling back through pages,
and an important chat sank under newer ones with nothing to keep it on top.

## Decision

### One menu per message, asked for by holding it

Pressing and holding a message opens its menu (`useLongPress`): a right click
on a desktop, Enter on a keyboard. On your own message it offers correcting and
taking it back; on somebody else's, reporting it. A message that was taken back
offers nothing. The bubble only asks; the screen owns the one menu, the one edit
sheet and the one report sheet, as it owns the one camera. The menu hands its
choice on only once it has closed, because two drawers open at once deadlock
(see `PhotoCapture`).

### Correcting: the words, for fifteen minutes, and it shows

`ChatMessage.Edit` takes new words for `EditWindow` (15 minutes) after sending.
That is long enough for a typo noticed on rereading, and short enough that an
answer cannot be rewritten under the replies it got. Only words can be
corrected: a message that is only a photograph has none. `EditedAt` puts
"bearbeitet" beside the time for everybody. The response carries `canEdit`, so
the app offers editing exactly while the server would accept it. A late attempt
is the field error `tooLateToEdit`.

### Taking back: any time, and the place stays

`ChatMessage.Delete` empties the words, deletes the photograph (its row in the
same save, its bytes after it, so it leaves the sender's allowance) and drops
the reactions. The row keeps its time and `DeletedAt`, and the thread says
"Nachricht gelöscht" in its place for everybody, so nobody's reply hangs in the
air. The list's preview says the same (`lastMessageIsDeleted`). There is no
time limit, because taking something back is always the sender's right.
Somebody else's message can be neither corrected nor taken back (403). Lines
derived from a goal (ADR 0027) are not messages and have no menu.

### A change reaches every open thread, even far back

An edit or a deletion touches the conversation (`LiveArea.Chats`), which reads
the newest page again, and the message itself (`LiveArea.ChatMessage`, new).
A thread that has that message further back than its newest page fetches it on
its own (`GET /api/chats/{id}/messages/{messageId}`) and puts it in place,
instead of reading every loaded page again. A correction never rings: it is not
news.

### Reporting a message

`ReportTargetKind.Message` covers a message with or without a photograph. Only
the others in its conversation can report it, never its sender, and not once it
has been taken back. The report carries ids, never the words (ADR 0022). A
group as a whole is not a target: its members can be reported, and what was
said can be reported message by message.

### Pinning, per person, three at most

`ConversationParticipant.PinnedAt`, set from the info sheet's switch
(`PUT /api/chats/{id}/pin`), is per person like a mute. The list is ordered
pinned first, then by last activity. The app splits it into its sections in
order, so a pinned chat stands at the top of its own section rather than above
all of them. At most three (`ChatService.MaxPinned`): a pin is only worth
something while it is rare. A fourth is refused with `tooMany`, which the sheet
words as its own sentence.

### The photographs, in the info sheet

`GET /api/chats/{id}/photos` reads a conversation's photographs on their own,
newest first, at most 120: those sent as messages and, in a goal's
conversation, the proofs delivered against the goal. A deleted image and a
taken-back message are left out. The info sheet reads them when it opens and
shows a grid of square tiles (`ChatPhotoTile`). A tap opens the photograph full
size with who sent it, when, and for a proof whether it was live (ADR 0036).

## Consequences

- Two migrations: `ChatMessageEditsAndDeletion` (`EditedAt`, `DeletedAt`) and
  `ConversationPins` (`PinnedAt`). The contract gains `editedAt`, `isDeleted`,
  `canEdit` on a message, `isPinned` on a summary and a thread,
  `lastMessageIsDeleted`, and `ChatPhotoResponse`.
- The field codes `tooLateToEdit` and (#40) `wrongPassword` and `lockedOut` are
  new, with their words in both languages.
- A push already delivered with a message's first words cannot be called back.
  Taking a message back removes it everywhere q2 keeps it, not from a lock
  screen that already showed it.
