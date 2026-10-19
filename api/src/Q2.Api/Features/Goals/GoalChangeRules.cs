namespace Q2.Api.Features.Goals;

/// <summary>
/// Which edits of a running goal make it easier, and how long the people who
/// check it have to object.
/// </summary>
/// <remarks>
/// q2 is an accountability app, so an edit must not become the quiet way out.
/// A change that asks more of the owner applies with the next window; one that
/// asks less is announced, can be lifted by the checkers' objections — the same
/// rule as a pause (<see cref="PauseRules.IsOverturned"/>) — and waits until it
/// has been open to objection for <see cref="ObjectionPeriod"/>
/// (docs/adr/0044-editing-a-running-goal.md).
///
/// **When in doubt, it is easier.** The app cannot tell whether a rewritten
/// description asks for more, or whether Monday, Wednesday and Friday is
/// stricter than three times a week; it only knows they are not the same. So
/// anything it cannot show to be stricter goes in front of the checkers, and
/// the cost of being wrong is a day's wait rather than a loophole.
///
/// Pure, like <see cref="PauseRules"/>: no database and no clock.
/// </remarks>
public static class GoalChangeRules
{
    /// <summary>
    /// How long an easier change is open to objection before it may apply. A
    /// day, so a daily goal edited at 23:55 still gives its checkers one.
    /// </summary>
    public static readonly TimeSpan ObjectionPeriod = TimeSpan.FromHours(24);

    /// <summary>The average month, for comparing a monthly quota with a weekly one.</summary>
    private const double DaysPerMonth = 365.25 / 12;

    /// <summary>Two rates closer than this are the same rate; they are ratios of small integers.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Whether the part of an edit that waits for the next window makes the
    /// goal easier, judged against what the goal is now.
    /// </summary>
    public static bool IsEasier(Goal goal, DeferredTerms terms)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(terms);

        // The description is what checkers judge photographs against, and a
        // rewrite cannot be ranked; a later day is more time; somebody removed
        // is one fewer person looking.
        if (terms.DescriptionChanged || terms.TargetDate is not null || terms.RemovedCheckers.Count > 0)
        {
            return true;
        }

        return goal.IsDont ? IsEasierDont(goal, terms) : IsEasierDo(goal, terms);
    }

    /// <summary>
    /// How many proofs a week a Do's schedule asks for, on average. A monthly
    /// quota is spread over the average month.
    /// </summary>
    public static double ProofsPerWeek(GoalSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Kind switch
        {
            ScheduleKind.Interval => 7.0 / Math.Max(1, schedule.EveryDays ?? 1),
            ScheduleKind.Weekdays => schedule.Weekdays.Count,
            ScheduleKind.Times when schedule.Period == QuotaPeriod.Month => (schedule.Times ?? 1) * 7 / DaysPerMonth,
            ScheduleKind.Times => schedule.Times ?? 1,
            _ => 0,
        };
    }

    /// <summary>How many uses a day a Don't's limit allows, on average.</summary>
    public static double UsesPerDay(int limit, QuotaPeriod period) => limit / (period switch
    {
        QuotaPeriod.Day => 1,
        QuotaPeriod.Week => 7,
        _ => DaysPerMonth,
    });

    /// <summary>
    /// How much a proof mode asks of the owner. A check at a moment they cannot
    /// plan for is the most; their word alone is the least.
    /// </summary>
    public static int Strictness(DontProofMode mode) => mode switch
    {
        DontProofMode.SpotCheck => 2,
        DontProofMode.ProofAtEnd => 1,
        _ => 0,
    };

    private static bool IsEasierDo(Goal goal, DeferredTerms terms)
    {
        if (terms.Schedule is not { } schedule)
        {
            return false;
        }

        // Fewer is easier; the same number in another shape cannot be shown
        // to be stricter. Only more is.
        return ProofsPerWeek(schedule) <= ProofsPerWeek(goal.Schedule) + Tolerance;
    }

    private static bool IsEasierDont(Goal goal, DeferredTerms terms)
    {
        var periodBefore = goal.Schedule.Period ?? QuotaPeriod.Day;
        var periodAfter = terms.Schedule?.Period ?? periodBefore;
        var before = UsesPerDay(goal.Limit ?? 0, periodBefore);
        var after = UsesPerDay(terms.Limit ?? goal.Limit ?? 0, periodAfter);

        // More allowed is easier, and so is the same allowance counted over a
        // different period — "never in a week" and "never in a day" are not
        // the same promise, and the app cannot say which is the harder one.
        if (after > before + Tolerance || (Math.Abs(after - before) <= Tolerance && periodAfter != periodBefore))
        {
            return true;
        }

        return terms.ProofMode is { } mode
            && Strictness(mode) < Strictness(goal.ProofMode ?? DontProofMode.HonestOnly);
    }
}
