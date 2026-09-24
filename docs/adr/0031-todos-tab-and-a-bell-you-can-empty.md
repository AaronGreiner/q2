# 0031 — A To-Dos tab, a shorter start screen, and a bell you can empty

**Status:** Accepted
**Date:** 2026-09-24
**Amends:** [0015 — The Qdos design language](0015-qdos-design-language.md),
section "The tab bar creates", and
[0024 — One notification pipeline](0024-one-notification-pipeline.md), where
the bell could be read but not emptied.

## Context

- **Your own goals had no place in the tab bar.** The middle slot was a plus
  labelled "Neu" that opened the goals screen with the create sheet already up.
  It was the only visible way to reach your goals and what is due today, so
  people read it as "this is where my goals are" and were surprised to land in
  a form.
- **The start screen showed everything.** Every entry in the friends' feed and
  a strip of all your goals sat below what is due today, so the screen people
  open most kept growing, with nothing new in it.
- **The bell only grew.** A line could be read but never removed. After a month
  of kudos the list was mostly things somebody had already dealt with.

## Decision

### The middle of the tab bar is a place

Start · Suche · **To-Dos** · Chats · Profil — five destinations of equal
weight. To-Dos is the goals screen (`/goals`), with today's windows under one
tab and the goals under the other.

Creating a goal is the plus in that screen's header, and only there. The
empty state below does not repeat it, so the screen still has one loud
control. The sheet is still `?create=1`, so it survives a reload and closes
with the back gesture.

### The start screen is a summary

Three of what is due today and the three newest things friends did, each with
"Alle anzeigen" to the whole list. The strip of your own goals is gone from
the start screen: those goals now have their own tab, and repeating them there
made the start screen longer without saying anything new.

### The bell can be emptied

- **Swipe a line to the left** and it is deleted. The row reveals red while
  it moves, because deleting is final. For a keyboard or a screen reader, each
  row has a delete button that stays hidden until it has focus.
- **"Alle löschen"** in the header, behind a confirmation, deletes everything
  that was on the screen. The bound is the newest line shown
  (`DELETE /api/notifications?until=…`), not the server's clock, so a line
  that arrives while the question is up is not deleted before anybody has
  seen it.

Deleting removes the rows. The retention worker already does that after thirty
days, so there is no second "hidden" state to maintain. Three rules follow from
how the bell is built:

- A line of reactions is every reaction to the same thing, so dismissing it
  deletes all of them up to that line. Leaving the older rows would bring the
  line straight back with one name fewer.
- Lines hidden by a block were not on the screen, so "Alle löschen" does not
  delete them. Lifting the block still brings them back, as 0022 promises.
- Dismissing a line that is already gone, or that belongs to somebody else,
  answers 204 and changes nothing. The answer is the same either way, so it
  reveals nothing about anybody else's bell.

## Consequences

- `GoalTile` has no remaining use and is deleted. The start screen reads three
  endpoints instead of four.
- The next time a create action is proposed for the tab bar, the problem it
  would bring back is recorded here.
- A bell open on a second device is not told about a deletion made on the
  first. It shows the line until it is next read, and opening it there
  reads it fresh.
