# 0016 — Windows instead of steps: a goal you can miss

**Status:** Accepted
**Date:** 2026-09-07

## Context

A goal in q2 was a counter. It had `CompletedSteps` and `TotalSteps`, a
percentage derived from them, and a `Contribute` button that added one. Its
rhythm was an enum of four words — Daily, Weekdays, Weekly, Once — that decided
nothing: no code read it, and nothing was due at any particular moment.

Three things follow from that, and all three are fatal to the product described
in `QDOS-UEBERNAHME.md`:

- **Nothing can be late.** "14 of 21" says how far somebody has turned a number
  up, not whether they did the thing they said they would do this week.
- **Nothing can fail.** A streak derived from the days somebody happened to tap
  can only stall; it cannot break, because there is no moment at which it was
  supposed to move and did not.
- **The commitment people actually make cannot be expressed.** "Dreimal die
  Woche" is not one of four enum members, and it is the shape most habits have.

Everything the remaining stages need hangs off the missing piece: the streak
that breaks, the balance that says "47 geschafft · 5 verpasst", the evening
warning to friends, the history grid, the pause that stops the clock.

## Decision

**A goal is a schedule and a chain of windows.**

### `GoalSchedule` — four shapes, not four words

An owned entity on the goal with four kinds, each carrying what it needs:

| kind | payload | means |
| --- | --- | --- |
| `Once` | the goal's `TargetDate` | once, by that day |
| `Interval` | `EveryDays` | every day, every third day, every week |
| `Weekdays` | `Weekdays` (ISO, Monday is 1) | on the chosen days |
| `Times` | `Times` + `Period` | three times per week, twice per month |

Two rules from the source project hold the type together, and both are load-bearing:

1. **A deadline is a whole day or a whole period, never a clock time.** Whoever
   delivers by midnight was on time. `ReminderAt` is a nudge and nothing else.
2. **Every commitment has exactly one representation.** "Every 7 days" and "once
   a week" would otherwise be two ways to say one thing, and two ways to say one
   thing is two code paths that will eventually disagree. `Weekly` therefore
   migrates to `Times(1, Week)` — a calendar week — rather than to
   `Interval(7)`.

It is an owned entity rather than four nullable columns because the sensible
combinations are not "any of them": an interval has no weekdays and a quota has
no interval, and this is the only place that has to know it. The columns still
land in the `Goals` row, so the whole goal is readable in one line of SQL.

### `GoalInstance` — the window

`StartsOn`/`DueOn` (local days), `StartsAt`/`DueAt` (instants), `RequiredProofs`,
`ConfirmedProofs`, `Status` (Open / Done / Missed), `ResolvedAt`.

`ConfirmedProofs` is a count rather than a flag because "3× pro Woche" is a real
commitment: a window can be two-thirds delivered, and both the person and their
friends need to see that while it still matters.

The instants are **stored, not recomputed**, so somebody moving to another
country does not retroactively change whether last Tuesday was late.

Two invariants live on `Goal`, not in a service:

- **at most one open window**, because `OpenWindow` is the only door and it
  refuses when one is already open;
- **no window twice**, because it also refuses a start/end pair the goal already
  has. That is what makes the maintenance job safe to run twice.

### Derived, never stored

`Streak` counts consecutive `Done` windows back from the newest resolved one and
stops at the first `Missed`. `Balance` counts both outcomes. Neither has a
column. An open window does not break a streak and does not add to it: it has
not failed yet, and saying otherwise before the deadline is a claim about the
future.

### `GoalMaintenance` — one pure function, two callers

Everything that has to happen while nobody is looking is one function over one
goal: miss an expired window, open the next one, repeat.

- **Idempotent.** Running it twice does nothing the second time, because the
  model refuses the duplicate rather than the caller remembering to check.
- **Catching up.** After two days down, both days are worked through — each
  elapsed window is created and then missed, in order. The source project
  skipped elapsed windows silently, which quietly forgave everybody for an
  outage; q2 does not.
- **Never ahead of itself.** A window that has not started is not opened, so
  finishing today's does not put tomorrow's on screen to be delivered tonight.
- **No watermark table.** "How far have we processed" is already stored, in the
  newest window's deadline. A second copy of that fact is a second thing that
  can be wrong.

It is called from two places and both are needed. **Every read runs it for the
goals it just loaded**, so a screen is never a day out of date — the same
decision the source project made, for the same reason. **A background worker
runs it for everybody** every ten minutes, so a person's friends see a missed
window even when that person never opens the app; stage 5's evening warning
depends on that. The worker is not registered in `AutomatedTest`, where a job
writing between the arrange and the assert would make tests flaky for a reason
unrelated to what they check.

### Time zones stop being ignorable

A deadline that says "midnight" decides whether somebody keeps a streak, so
"whatever the server thinks a day is" is no longer good enough.

`Person.TimeZoneId` holds an IANA identifier, set at registration from
`Q2:TimeZone` (default `Europe/Berlin`). `LocalCalendar` is the only place
instants and local days are converted into each other; nothing else calls
`TimeZoneInfo`, and nothing else takes `DateOnly.FromDateTime(now.UtcDateTime)`
as "today" — that expression is correct only for somebody living in UTC.

A goal's windows are always computed in **its owner's** zone. A friend in
another country looking at the same goal must not see a different deadline.

One platform note: `InvariantGlobalization` is on for the whole backend, which
on **Windows** leaves only UTC available. On Linux and macOS the zone database
is read from the operating system and this works. The deployment target is Linux
(docs/deployment.md); a developer on Windows gets UTC and a warning rather than a
crash.

### What went

**Goals with steps**, `Contribute`, `ProgressPercent`, `GoalContribution` and
`GoalRhythm` are gone. So is `GoalTask` — the tasks under a goal — and with it
the `/api/tasks` endpoints. That is the most painful item on the list and it is
recorded as a deliberate bet in `QDOS-UEBERNAHME.md` section 3b: a task was the
only interaction in q2 with no social hurdle at all, and if the product turns
out to be too demanding without one, this is the piece to bring back.

"Was ist heute dran" is now `GET /api/today`: the goals whose current window
**covers** today, not the ones due today. Three runs by Sunday is something you
can do on Tuesday, and a list that only showed it on Sunday would be a list of
things it is already too late to start.

### The interim proof

`POST /api/goals/{id}/proof` records one delivered proof, and only the owner may
call it. Until stage 4 that is the owner saying "done" — exactly the self-report
this product exists to replace. It is here because a stage that removed every
way to succeed would ship an app in which goals can only ever fail. What changes
in stage 4 is **who calls it**: a photograph their friends confirm. What the
window does with it — count it, close on the last one — is already final.

## Consequences

- The migration translates rather than defaults, and keeps the history: every
  `GoalContribution` becomes a delivered one-day window with its own id, so
  nobody's streak resets to zero on deploy. `CompletedSteps` has no honest
  equivalent and is dropped.
- That migration is hand-written, including the `Goals` table rebuild, because
  EF's SQLite `DropColumn` turns off foreign keys around it — which
  `MigrationTests.MigrationSqlNeverDisablesForeignKeys` forbids.
- A read can now write. `GoalService` loads goals tracked and saves when
  maintenance moved something. It is one save on the first read after a
  deadline, and it is what makes a screen truthful.
- Seeds describe a goal's past as a string of outcomes (`"dddmdd"`) and build it
  backwards from today, so a seeded streak is the same however long ago the
  database was last rebuilt.
- `Weekly` goals became weekly quotas, which is close but not identical: a goal
  that used to mean "every seven days from when I started" now means "once in
  each calendar week".
- Two things are still missing that this model exists for, and both are stages of
  their own: the proof photograph with its vote (stage 4), and the pause that
  stops a window's clock (stage 6).
