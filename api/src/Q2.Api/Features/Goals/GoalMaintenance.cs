using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// What has to happen to a goal's windows as time passes, whether or not
/// anybody is looking.
/// </summary>
/// <remarks>
/// Three things cannot wait for somebody to open the app: a vote whose deadline
/// has passed has to be decided on the votes cast, a window whose deadline has
/// passed has to become a miss, and the next window has to exist so there is
/// something to deliver into. Without them the app would only ever be correct
/// while it was on screen — and a photograph delivered last night would sit
/// unresolved until its owner happened to look, which is precisely the person
/// whose opinion the vote does not want.
///
/// The whole thing is one pure function over one goal, deliberately:
///
/// - **Idempotent.** Running it twice does nothing the second time.
///   <see cref="GoalInstance.Miss"/> refuses a resolved window and
///   <see cref="Goal.OpenWindow"/> refuses a duplicate, so this is a property
///   of the model rather than of the caller remembering.
/// - **Catching up.** After two days down, both days are worked through — each
///   elapsed window is created and then missed, in order. That is why the loop
///   exists rather than a single step: it is the difference between an honest
///   history and one where an outage quietly forgave everybody.
/// - **Never ahead of itself.** A window that has not started is not opened, so
///   finishing today's does not put tomorrow's on the screen for somebody to
///   deliver into tonight.
/// - **No watermark table.** "How far have we processed" is already stored, in
///   the deadline of the newest window. A second copy of that fact is a second
///   thing that can be wrong.
///
/// It is called from two places, and both are needed. The read path runs it for
/// the goals it just loaded, so a screen is never a day out of date; the
/// background worker runs it for everybody, so a person's friends see a missed
/// window even when that person never opens the app — which is what stage 5's
/// warning will hang on.
/// </remarks>
public static class GoalMaintenance
{
    /// <summary>
    /// How many windows one run will work through for a single goal.
    /// </summary>
    /// <remarks>
    /// A year of daily windows is 365, so this covers any outage worth catching
    /// up in one pass. It is a guard rather than a limit: the next run picks up
    /// where this one stopped, and without it a schedule that somehow produced a
    /// non-advancing window would spin forever.
    /// </remarks>
    public const int MaxWindowsPerRun = 400;

    /// <summary>
    /// Brings one goal up to date. Returns true when anything changed, so the
    /// caller knows whether it has to save.
    /// </summary>
    /// <param name="owner">
    /// The goal's owner, when the caller has them loaded. Supplied so a vote
    /// that is confirmed while nobody is looking still counts its day towards
    /// their streak; without it the outcome is still applied, only the check-in
    /// is skipped. The background worker passes it, and so does every read path
    /// that already had the person in hand.
    /// </param>
    public static bool Advance(
        Goal goal,
        LocalCalendar calendar,
        DateTimeOffset now,
        IIdGenerator ids,
        Person? owner = null)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(ids);

        // A pause whose last day has gone is over. First, because everything
        // below asks whether one is still running.
        var changed = goal.ExpirePauses(now);

        // Spot checks on windows that are no longer open — relapsed, paused,
        // set aside — are settled too, so none stays "open" for ever. They
        // cannot move such a window any more.
        foreach (var settled in goal.Instances.Where(instance => instance.Status != GoalInstanceStatus.Open))
        {
            changed |= settled.SettleSpotChecks(now);
        }

        for (var step = 0; step < MaxWindowsPerRun; step++)
        {
            // An archived or completed goal has no future: no window is opened
            // for it, and the one it had was set aside when it stopped.
            if (goal.Status != GoalStatus.Active)
            {
                break;
            }

            // While a pause runs, nothing at all happens to this goal: no
            // deadline elapses and no window opens. That is the entire point of
            // it, and it is one line rather than a condition threaded through
            // each step below.
            if (goal.ActivePauseAt(now) is not null)
            {
                break;
            }

            // A one-off has no next window for a change to wait for. Its change
            // applies once the objection period is over — before the deadline
            // below is looked at, since a later day is what it may have moved.
            if (goal.ApplyDueOneOffChange(calendar.Today(now), calendar.EndOfDay, now))
            {
                changed = true;
                continue;
            }

            if (goal.CurrentInstance is { } open)
            {
                // A spot check whose three minutes are up is settled first: an
                // unanswered one misses the window before anything else is
                // asked of it.
                if (open.SettleSpotChecks(now))
                {
                    changed = true;
                    continue;
                }

                /*
                 * A vote first, and the window afterwards.
                 *
                 * The order is the whole point: a photograph delivered at
                 * 23:00 has twelve hours to be believed, and its window's own
                 * deadline may pass in the middle of that. Missing the window
                 * before deciding the vote would fail somebody who had in fact
                 * delivered on time.
                 */
                if (open.PendingProof is { } pending && pending.IsExpiredAt(now))
                {
                    var outcome = ProofVoting.ResolveAfterDeadline(pending.CastValues, pending.Attempt);

                    if (goal.ApplyProofOutcome(pending, outcome.Result, now))
                    {
                        changed = true;

                        if (outcome.Result == VotingResult.Confirmed)
                        {
                            owner?.CheckIn(ids.NewId(), calendar.DayOf(now));
                        }
                    }

                    continue;
                }

                // A window whose vote is still running is not overdue yet: its
                // proof arrived in time and is simply not decided. Expire
                // misses a Do's window and keeps a Don't's whose limit held.
                if (open.PendingProof is not null || !open.Expire(now))
                {
                    // Still running. Nothing more to do until its deadline.
                    break;
                }

                // A Don't kept for its period counts for the owner's daily
                // flame on its last day, the way a believed photograph counts
                // on the day it was believed — even when the run that notices
                // is a day late.
                if (open.IsDont && open.Status == GoalInstanceStatus.Done)
                {
                    owner?.CheckIn(ids.NewId(), open.DueOn);
                }

                changed = true;
                continue;
            }

            var next = NextWindowFor(goal, calendar);

            /*
             * An edit that waited for the next window takes effect between
             * windows: from the first day the old window no longer covers, or,
             * when it can be objected to, the first day that starts once the
             * objection period is over — as long as that comes before the next
             * window the old terms would open, which otherwise opens first.
             * Then round again, so the window is cut from the new terms
             * (docs/adr/0044-editing-a-running-goal.md).
             */
            if (goal.PendingChange is { } waiting
                && goal.LatestInstance is { } latest
                && FirstDayFor(waiting.Objectable, waiting.CreatedAt, latest.DueOn, calendar) is var from
                && from <= calendar.Today(now)
                && (next is not { } upcoming || from <= upcoming.Start))
            {
                goal.ApplyChange(waiting, from, calendar.EndOfDay, now);
                changed = true;
                continue;
            }

            if (next is not { } window)
            {
                break;
            }

            // A window that has not started yet stays closed. Opening tomorrow's
            // the moment today's is delivered would let somebody hand in
            // tomorrow's proof tonight — and on a weekly quota it would open
            // next week's on the Monday somebody finished this one.
            if (window.Start > calendar.Today(now))
            {
                break;
            }

            var instance = goal.OpenWindow(
                ids.NewId(),
                window,
                calendar.StartOfDay(window.Start),
                calendar.EndOfDay(window.End));

            if (instance is null)
            {
                break;
            }

            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// The first local day the goal's waiting change will count for, if
    /// nothing else happens first — what the goal screen says it applies "ab".
    /// Null when nothing is waiting.
    /// </summary>
    public static DateOnly? WhenPendingChangeApplies(Goal goal, LocalCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return goal.PendingChange is { } change
            ? WhenChangeApplies(goal, calendar, change.Objectable, change.CreatedAt)
            : null;
    }

    /// <summary>
    /// The first local day a change made at <paramref name="madeAt"/> would
    /// count for — whether it has been made yet or not, which is what lets the
    /// sheet say so before it is saved.
    /// </summary>
    /// <remarks>
    /// Walked through the same windows <see cref="Advance"/> opens, so the day
    /// on the screen is the day it happens. A pause still to come can only
    /// move it later.
    /// </remarks>
    public static DateOnly? WhenChangeApplies(Goal goal, LocalCalendar calendar, bool objectable, DateTimeOffset madeAt)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(calendar);

        // A one-off's change goes into the window it has, the moment it may.
        if (!goal.Schedule.Repeats)
        {
            return calendar.DayOf(objectable ? madeAt + GoalChangeRules.ObjectionPeriod : madeAt);
        }

        if (goal.LatestInstance is not { } latest)
        {
            return null;
        }

        var lastEnd = latest.DueOn;
        var next = NextWindowFor(goal, calendar);

        for (var step = 0; step < MaxWindowsPerRun; step++)
        {
            var from = FirstDayFor(objectable, madeAt, lastEnd, calendar);

            if (next is not { } window || from <= window.Start)
            {
                return from;
            }

            lastEnd = window.End;
            next = SkipPaused(goal, goal.Schedule.NextWindow(window.End));
        }

        return null;
    }

    /// <summary>
    /// The first day a waiting change can count for once the window ending on
    /// <paramref name="lastEnd"/> is over: the day after it, or the first day
    /// that starts once the objection period has run, whichever is later.
    /// </summary>
    private static DateOnly FirstDayFor(bool objectable, DateTimeOffset madeAt, DateOnly lastEnd, LocalCalendar calendar)
    {
        var earliest = objectable ? madeAt + GoalChangeRules.ObjectionPeriod : madeAt;
        var day = calendar.DayOf(earliest);

        if (calendar.StartOfDay(day) < earliest)
        {
            day = day.AddDays(1);
        }

        var after = lastEnd.AddDays(1);
        return day > after ? day : after;
    }

    /// <summary>
    /// The window that should come next: the first one if the goal has never
    /// had one, otherwise the one after its newest.
    /// </summary>
    private static GoalWindow? NextWindowFor(Goal goal, LocalCalendar calendar)
    {
        var window = goal.LatestInstance is { } latest

            // After a change, from the day it applied: the new terms do not
            // reach back into the days before it.
            ? goal.Schedule.NextWindow(LaterOf(latest.DueOn, goal.LastChangeAppliedOn?.AddDays(-1)))

            // A goal with no window at all. Normal at creation, and also what a
            // goal migrated from the old step-counting model looks like. From
            // the owner's day it was made: the UTC day is the day before for
            // anything made after local midnight, which opened a window for
            // yesterday and settled it at once (#132).
            : goal.Schedule.FirstWindow(
                calendar.DayOf(goal.CreatedAt),
                goal.TargetDate);

        return SkipPaused(goal, window);
    }

    private static DateOnly LaterOf(DateOnly day, DateOnly? other) => other is { } candidate && candidate > day ? candidate : day;

    /// <summary>
    /// The window itself, or the first after it whose deadline does not fall
    /// inside a pause.
    /// </summary>
    /// <remarks>
    /// A window whose deadline fell inside a pause never happens. Without
    /// this, the run that follows a week off would create those seven days one
    /// after another and miss every one of them — which is precisely what the
    /// pause was granted to prevent. Skipping them leaves a gap in the history
    /// rather than a row of failures, and a gap is what a pause honestly is: no
    /// result at all.
    /// </remarks>
    private static GoalWindow? SkipPaused(Goal goal, GoalWindow? window)
    {
        for (var guard = 0; window is { } candidate && goal.IsPausedOn(candidate.End); guard++)
        {
            if (guard >= MaxWindowsPerRun)
            {
                return null;
            }

            window = goal.Schedule.NextWindow(candidate.End);
        }

        return window;
    }
}
