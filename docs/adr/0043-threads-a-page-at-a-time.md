# 0043 — A thread is read a page at a time

**Status:** Accepted — amended by [0046](0046-looking-after-a-conversation.md): a message can be corrected and taken back, and a changed line further back is fetched by its id
**Date:** 2026-10-18
**Issues:** [#44](https://github.com/AaronGreiner/q2/issues/44)
**Amends:** [0027 — Every goal is checked in its own conversation](0027-goal-conversations.md),
whose consequences left threads and the chat list unpaged.

## Context

- **Opening a thread read all of it.** `GET /api/chats/{id}` loaded every
  message with every reaction, and in a goal's conversation every line of its
  timeline with every photograph described. A daily goal adds a photograph and
  a line a day; a few months of one, or one lively group, and the thread is
  slow to open on a phone.
- **The chat list read every message of every conversation** to find the last
  one of each, and counted what was unread over them in memory.
- **A goal's conversation is two lists.** What people wrote is stored; what
  happened to the goal is derived from the goal every time it is read
  (`GoalTimeline`, [0027](0027-goal-conversations.md)). A page of one without
  the matching part of the other would put a kept window next to the wrong
  day's messages.

## Decision

### One cursor for both lists

A line's place in a thread is `(instant, line about the goal before message,
kind, id)` — `ThreadPosition`. Messages are ordered by `(SentAt, Id)`, as they
always were; lines about the goal by the timeline's own order; at the same
instant a line about the goal comes first, which is how the client already
laid the two side by side.

A page is the newest 30 lines before a position. The database is asked for
one message more than fits; the goal's lines are derived from the goal as
before and only the ones that fall on the page are described and sent
(`ThreadPage.Cut`). The page carries the position of its oldest line as an
opaque cursor, exact to the tick the database stores, so two lines with the
same timestamp land on one page each and never on both or neither.

### Reads and writes

- `GET /api/chats/{id}` answers with the newest page and `olderCursor`, and
  still marks the thread read.
- `GET /api/chats/{id}/messages?before=<cursor>` answers with the page before
  it. It does not move the read marker: reading last week says nothing about
  today.
- Sending and muting answer with the newest page, as before with the whole
  thread.
- **A reaction answers with the message it was on**, not with a page. It may be
  pages back from the newest, where an answer made of the newest page would not
  have it. A vote already answered with the photograph; the client puts both
  where the line is loaded.

### The chat list

The last message of each conversation is picked in the query (`ROW_NUMBER()`
per conversation), and what is unread is counted by the database against each
reader's marker. No other message is read.

### The client keeps what it has read

The newest page lives under the thread's data key and is replaced by every
read and write, as before. Everything read further back is the thread's
history, kept in front of it (`utils/chatPages.ts`). When new lines push the
oldest off the newest page, those are moved into the history rather than lost;
when two newest pages have nothing in common — more arrived at once than a page
holds — the history starts again rather than drawing a thread with a hole in
it.

Scrolling within a screen of the top reads the page before. The screen keeps
its place by its distance from the bottom, with the browser's own scroll
anchoring off on the region so the correction is not made twice. A new line is
followed to the bottom only when the reader was at the bottom already or the
line is their own.

## Consequences

- Opening a thread reads at most 31 messages, their reactions and their
  photographs, however long it is. The chat list reads one message per
  conversation.
- **The goal is still read whole** for a goal's conversation — for the banner,
  the streak and the timeline it derives. That is the goal's own read, shared
  with its screen, and paging it is a change to how a goal is loaded rather
  than to the conversation
  ([#127](https://github.com/AaronGreiner/q2/issues/127)).
- A line that changes pages back from the newest — another person's reaction
  on an old message, a vote cast elsewhere on an old photograph — shows when
  the thread is opened again; the live connection reads the newest page only.
  A reaction or a vote made from this screen is shown at once wherever it is.
- A line about the goal that is settled into the past — a pause found to have
  ended at its last day — appears in a page already loaded only on the next
  opening, if more than a page has happened since that day.
- Editing and deleting messages ([#25](https://github.com/AaronGreiner/q2/issues/25))
  will address a message that may not be on the newest page, the way a reaction
  does now.
