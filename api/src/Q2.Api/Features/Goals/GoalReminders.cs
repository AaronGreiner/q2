using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// When a Do's daily reminder is due: the nudge its owner set up when they
/// made it.
/// </summary>
/// <remarks>
/// Pure, like <see cref="GoalRisk"/>, because it is decided by a pass that
/// runs whether or not anybody has the app open
/// (<see cref="GoalMaintenanceWorker"/>), and has to be testable without one.
///
/// The rules, each of which keeps the reminder from becoming something to mute
/// (#82):
///
/// 1. **Only for something still to do.** An open window that takes a
///    photograph now. Not one already delivered, not one whose photograph is
///    being looked at, not a paused goal, not a stopped one, and not a Don't —
///    a Don't is kept by not doing something, and is never asked to deliver.
/// 2. **Every day it still matters, once.** A quota ("3× pro Woche") is
///    reminded on each day of its window until it is met: the reminder is
///    called "täglich", and the person chose the time. The window records the
///    day (<see cref="GoalInstance.RemindedOn"/>), so the pass every ten
///    minutes says it once.
/// 3. **From the time it was set for, in the owner's own zone.** Late rather
///    than never: a pass that comes round after the time — or a server that was
///    down at it — still reminds that day. Quiet hours are not decided here;
///    the notification pipeline drops a reminder that falls inside them, as it
///    drops everything else (docs/adr/0024-one-notification-pipeline.md).
/// </remarks>
public static class GoalReminders
{
    /// <summary>The window to remind the owner of now, or null.</summary>
    public static GoalInstance? Due(Goal goal, LocalCalendar calendar, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(calendar);

        if (goal.ReminderAt is not { } at
            || goal.IsDont
            || goal.IsClosed
            || goal.ActivePauseAt(now) is not null)
        {
            return null;
        }

        if (goal.CurrentInstance is not { } window
            || now < window.StartsAt
            || !window.IsCurrentAt(now)
            || !window.AcceptsProof)
        {
            return null;
        }

        if (window.RemindedOn == calendar.Today(now) || calendar.TimeOf(now) < at)
        {
            return null;
        }

        return window;
    }
}
