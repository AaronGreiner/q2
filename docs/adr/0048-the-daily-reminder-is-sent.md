# 0048 — The daily reminder is sent

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#82](https://github.com/AaronGreiner/q2/issues/82)
**Amends:** [0032 — The next step first](0032-the-next-step-first.md), whose
reminder was "stored, shown and changeable, but nothing sends it yet", and
[0024 — One notification pipeline](0024-one-notification-pipeline.md), which
gains a kind and a switch.

## Context

Creating a Do asks "Tägliche Erinnerung" with a time, the card shows a bell
with that time and the goal screen lets the owner move it. Nothing ever sent
it. Somebody who set "Meditation, 08:00" relied on a nudge that never came and
found out by missing the window.

## Decision

- **A kind of its own, `ReminderDue`, with a switch of its own,
  `Reminders`** ("Deine täglichen Erinnerungen", on by default like every
  switch). It interrupts — a push, or a banner in the open app — and the bell
  does not keep it: the start screen already shows what is still to do, and
  yesterday's reminder means nothing. Tapping it opens the start screen.
- **Sent by the maintenance pass** (`GoalMaintenanceWorker`), next to the
  evening warning, and never on a read: the owner opening the app is exactly
  when a reminder is not needed. The rule is pure (`GoalReminders.Due`):
  - only a Do, running, not paused, whose current window has started and
    takes a photograph now — not one whose photograph is being looked at,
    and not one already met;
  - from the time it was set for on the owner's clock, in their zone;
  - **once a day, every day the window still needs a photograph.** A quota
    ("3× pro Woche") is reminded on each day of its week until it is met: the
    reminder is called daily and the person chose its time. The window
    remembers the day it last reminded on (`GoalInstance.RemindedOn`), so a
    pass every ten minutes, or a restarted one, says it once.
- **Late rather than never.** The pass runs every ten minutes, so a reminder
  arrives up to ten minutes after its time; a server that was down at the time
  still reminds when it comes back, the same day.
- **Quiet hours drop it**, as they drop every interruption. A reminder set
  inside them does not arrive; it is not held until morning.

## Consequences

- One column on `UserSettings` (`NotifyReminders`, true for existing rows) and
  one on `GoalInstances` (`RemindedOn`), migration `DailyReminders`.
- Delivery to a locked phone still depends on push being configured (#8, #12);
  the banner in the open app works without it.
