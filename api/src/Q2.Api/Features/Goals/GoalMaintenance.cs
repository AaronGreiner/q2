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

            if (goal.CurrentInstance is { } open)
            {
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
                // proof arrived in time and is simply not decided.
                if (open.PendingProof is not null || !open.Miss(now))
                {
                    // Still running. Nothing more to do until its deadline.
                    break;
                }

                changed = true;
                continue;
            }

            if (NextWindowFor(goal) is not { } window)
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
    /// The window that should come next: the first one if the goal has never
    /// had one, otherwise the one after its newest.
    /// </summary>
    private static GoalWindow? NextWindowFor(Goal goal)
    {
        var window = goal.LatestInstance is { } latest

            ? goal.Schedule.NextWindow(latest.DueOn)

            // A goal with no window at all. Normal at creation, and also what a
            // goal migrated from the old step-counting model looks like.
            : goal.Schedule.FirstWindow(
                DateOnly.FromDateTime(goal.CreatedAt.UtcDateTime),
                goal.TargetDate);

        /*
         * A window whose deadline fell inside a pause never happens.
         *
         * Without this, the run that follows a week off would create those
         * seven days one after another and miss every one of them — which is
         * precisely what the pause was granted to prevent. Skipping them leaves
         * a gap in the history rather than a row of failures, and a gap is what
         * a pause honestly is: no result at all.
         */
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
