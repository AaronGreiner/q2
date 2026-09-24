# 0032 — The next step first: urgency, folded misses, and a goal made in steps

**Status:** Accepted
**Date:** 2026-09-24
**Amends:** [0027 — Every goal is checked in its own conversation](0027-goal-conversations.md),
where every missed window was its own line and the list showed the newest
event whatever it was, and
[0031 — A To-Dos tab, a shorter start screen](0031-todos-tab-and-a-bell-you-can-empty.md),
whose start screen opened on two cards of numbers.

## Context

A walk through the app at 390 × 844 found the same problem in several places:
q2 said what had happened, but not what to do next or by when.

- **The start screen said the streak twice** — "0 Tage in Folge" and directly
  below it "0-Tage-Streak" — and the week under it only spelled out the days.
  What to actually do was a list further down. The top-right corner, the most
  valuable place on the screen, switched between light and dark.
- **"Heute fällig · 06:00" at four in the afternoon** read as "too late". The
  clock was the reminder, not the deadline; nothing said how long was left.
- **A goal's conversation showed fifteen lines of "Verpasst"** without a date,
  and the chat list's preview for your own goal was "Verpasst" too. The header
  of a conversation could not be tapped, so its members were unreachable.
- **The goal screen** had no dates on its history, offered "Aussetzen" and
  "Beenden" as large as the camera, kept who checks the goal below the
  history, and showed "Erinnerung: Keine" like a setting nobody could change.
- **Smaller:** the tab called "Suche" was mostly friends; ending a friendship
  sat beside "Nachricht schreiben"; the profile said the streak twice and
  "Ziele 37" without saying which; badges were shown that nothing awards;
  creating a goal was one long form with an unfiltered list of friends.

## Decision

### The start screen opens on what to do

- **"Als Nächstes"** is the first card: the open window that closes first and
  takes a photograph now, how long it has left, and a full-width camera. In
  the evening the risk cards take its place, since they already are the next
  thing to do. The "Heute" list below leaves that goal out and is sorted by
  deadline — what still wants a photograph, then what is being voted on, then
  what is done.
- **One card for the streak.** The number once, today's share as a ring beside
  it, and a week whose pills say something: kept (a tick), today (outlined),
  missed (grey) or still to come (faint). Screen readers get one sentence
  ("Diese Woche an 2 Tagen dabei") instead of seven letters.
- **Your picture replaces the theme switch** in the header and goes to the
  profile. Light and dark stay in the settings.

### Every window says how long it has left

`deadlineLeft` counts down to `dueAt` — "noch 7 Std.", "noch 25 Min." — once
the end is less than a day away, and replaces "Heute fällig", which says
nothing more. The reminder carries a bell instead of a clock and is read out
as "Erinnerung um 06:00".

### A goal's conversation folds what went wrong, and dates what happened

- Two or more missed windows in a row, with nothing said in between, are one
  line: "7.–23.9. · 17 Fenster verpasst". A window's own line carries its day
  ("Geschafft · Streak 3 · 3.9."). The day is the window's (`GoalEventResponse.day`,
  new), not when it was settled, because a run of misses is settled in one go
  the next time anybody looks.
- A day separator ("Heute", "Gestern", "Mo, 22.9.") goes before messages and
  photographs when the day changes — not between lines about the goal, which
  on a daily goal would double the thread.
- **The list never previews a miss.** The server skips `WindowMissed` when it
  picks a row's newest event, so a miss neither shows as "Verpasst" nor lifts
  a conversation to the top — the same "kein Nachtreten" as the feed. The
  owner's row carries the open window (`ChatSummaryResponse.goalCurrent`, new)
  and says what is due next instead: "Heute fällig · noch 9 Std.".
- **The header opens an info sheet**: the members (`ChatDetailResponse.members`,
  new; the reader first, as "Du"), each linked to their profile, the way to
  the goal, and — for a group — leaving it. Leaving moved out of the header,
  where it sat beside the mute switch.

Listing the members puts a doubter's id into the thread response. That is
not the anonymity 0018 protects: a doubter is a member either way and the
goal screen already lists them. What stays true, and is still asserted, is
that no photograph or vote in the thread carries their id.

### The goal screen: who checks it, and what can be changed

- **"Wer dich prüft"** comes straight after the window, each person a link to
  their profile; for a friend's goal it is "Dabei".
- **"Aussetzen" and "Beenden" moved behind "…"** in the header.
- **The history is grouped by month** with the day on every square.
- **The reminder can be moved or removed** by the goal's owner while it runs:
  `PUT /api/goals/{id}/reminder` with `reminderAt` (`HH:MM:SS`, or null). It
  nudges only the owner, so it is the one thing about a running goal that is
  theirs alone to change; everything else stays with #4. The create sheet now
  asks for the time too, instead of fixing nine o'clock.

  The reminder is stored, shown and changeable, but **nothing sends it yet**:
  there is no notification kind and no job for it. That is a separate change.

### Smaller things

- **The second tab is "Freunde"** with a people icon. The route stays
  `/search`, so links and notifications that point at it keep working.
- **Ending a friendship moved to the person's profile**, behind "…" and the
  existing question, next to "Melden oder blockieren".
- **The profile says the streak once** (the flame pill under the name is gone)
  and labels the third number "Abgeschlossen".
- **Badges are hidden** until something awards them. The API, the data and
  the seeds are untouched, so bringing them back is a frontend change.

### A goal is made in four steps

What, how often (with the reminder's time), who checks it (searchable once
there are more than six friends), and a summary that says when the first
window ends — "Dein erstes Fenster endet am So, 27.9. um Mitternacht" — from a
client-side mirror of `GoalSchedule.FirstWindow` (`utils/firstWindow.ts`). Six
ideas ("3× pro Woche laufen", …) fill the first step in one tap. A refusal
from the server takes the sheet back to the step that holds the field.

## Consequences

- `TodayProgressCard` and `BadgeGrid` are deleted; `StreakHero` draws both the
  streak and today. `NextUpCard`, `ChatInfoSheet`, `ChatMissedRun` and
  `GoalReminderSheet` are new.
- Three contract additions (`GoalEventResponse.day`, `ChatSummaryResponse.goalCurrent`,
  `ChatDetailResponse.members`) and one endpoint (`SetGoalReminder`).
- `useFriends` no longer removes friends; `usePersonProfile.removeFriend` does.
- Still open, each in its own issue: sending the reminder, the `GoalProgress`
  feed kind that only the seeds produce, and awarding badges.
