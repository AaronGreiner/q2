using Q2.Api.Features.Goals;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// Which edits make a goal easier — the ones its checkers can object to.
/// </summary>
/// <remarks>
/// The rule leans one way on purpose: anything the app cannot show to be
/// stricter is treated as easier, so the cost of a wrong guess is a day's wait
/// rather than a quiet way out (docs/adr/0044-editing-a-running-goal.md).
/// </remarks>
public class GoalChangeRulesTests
{
    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    public static TheoryData<string, GoalSchedule, GoalSchedule, bool> DoSchedules => new()
    {
        { "fewer a week", GoalSchedule.TimesPer(3, QuotaPeriod.Week), GoalSchedule.TimesPer(2, QuotaPeriod.Week), true },
        { "more a week", GoalSchedule.TimesPer(2, QuotaPeriod.Week), GoalSchedule.TimesPer(3, QuotaPeriod.Week), false },
        { "every day to every other day", GoalSchedule.EveryNDays(1), GoalSchedule.EveryNDays(2), true },
        { "every other day to every day", GoalSchedule.EveryNDays(2), GoalSchedule.EveryNDays(1), false },
        { "the same number in another shape", GoalSchedule.TimesPer(3, QuotaPeriod.Week), GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Wednesday, Weekday.Friday]), true },
        { "three a week to twelve a month", GoalSchedule.TimesPer(3, QuotaPeriod.Week), GoalSchedule.TimesPer(12, QuotaPeriod.Month), true },
        { "three a week to fourteen a month", GoalSchedule.TimesPer(3, QuotaPeriod.Week), GoalSchedule.TimesPer(14, QuotaPeriod.Month), false },
        { "weekdays to every day", GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Tuesday]), GoalSchedule.EveryNDays(1), false },
    };

    [Theory]
    [MemberData(nameof(DoSchedules))]
    public void ADoIsEasierWithFewerProofsAWeekOrTheSameInAnotherShape(
        string because,
        GoalSchedule before,
        GoalSchedule after,
        bool easier)
    {
        var goal = Do(before);

        Assert.True(easier == GoalChangeRules.IsEasier(goal, Terms(schedule: after)), because);
    }

    [Theory]
    [InlineData(1, QuotaPeriod.Week, 2, QuotaPeriod.Week, true)]
    [InlineData(2, QuotaPeriod.Week, 1, QuotaPeriod.Week, false)]
    [InlineData(0, QuotaPeriod.Day, 1, QuotaPeriod.Month, true)]
    [InlineData(1, QuotaPeriod.Day, 1, QuotaPeriod.Week, false)]

    // Never a day and never a week are the same allowance, counted over a
    // different period — not something the app can rank.
    [InlineData(0, QuotaPeriod.Day, 0, QuotaPeriod.Week, true)]
    public void ADontIsEasierWhenItAllowsMore(int limitBefore, QuotaPeriod periodBefore, int limitAfter, QuotaPeriod periodAfter, bool easier)
    {
        var goal = Dont(limitBefore, periodBefore, DontProofMode.HonestOnly);

        var terms = Terms(
            schedule: periodAfter == periodBefore ? null : GoalSchedule.Per(periodAfter),
            limit: limitAfter == limitBefore ? null : limitAfter);

        Assert.Equal(easier, GoalChangeRules.IsEasier(goal, terms));
    }

    [Theory]
    [InlineData(DontProofMode.SpotCheck, DontProofMode.HonestOnly, true)]
    [InlineData(DontProofMode.SpotCheck, DontProofMode.ProofAtEnd, true)]
    [InlineData(DontProofMode.HonestOnly, DontProofMode.SpotCheck, false)]
    [InlineData(DontProofMode.ProofAtEnd, DontProofMode.SpotCheck, false)]
    public void ALooserProofModeIsEasier(DontProofMode before, DontProofMode after, bool easier)
    {
        var goal = Dont(1, QuotaPeriod.Week, before);

        Assert.Equal(easier, GoalChangeRules.IsEasier(goal, Terms(proofMode: after)));
    }

    /// <summary>
    /// A stricter limit does not buy a looser proof mode past the checkers: one
    /// easier part makes the whole waiting change one they can object to.
    /// </summary>
    [Fact]
    public void OneEasierPartIsEnough()
    {
        var goal = Dont(2, QuotaPeriod.Week, DontProofMode.SpotCheck);

        Assert.True(GoalChangeRules.IsEasier(goal, Terms(limit: 0, proofMode: DontProofMode.HonestOnly)));
    }

    [Fact]
    public void ADescriptionALaterDayOrAFewerCheckerAlwaysGoesInFrontOfTheCheckers()
    {
        var goal = Do(GoalSchedule.EveryNDays(1));

        Assert.True(GoalChangeRules.IsEasier(goal, Terms(descriptionChanged: true)));
        Assert.True(GoalChangeRules.IsEasier(goal, Terms(targetDate: new DateOnly(2026, 7, 1))));
        Assert.True(GoalChangeRules.IsEasier(goal, Terms(removed: [Guid.CreateVersion7()])));
    }

    [Fact]
    public void AMonthlyQuotaIsSpreadOverTheAverageMonth()
    {
        Assert.Equal(3, GoalChangeRules.ProofsPerWeek(GoalSchedule.TimesPer(3, QuotaPeriod.Week)));
        Assert.Equal(3.5, GoalChangeRules.ProofsPerWeek(GoalSchedule.EveryNDays(2)));
        Assert.InRange(GoalChangeRules.ProofsPerWeek(GoalSchedule.TimesPer(13, QuotaPeriod.Month)), 2.98, 3.0);
    }

    private static DeferredTerms Terms(
        GoalSchedule? schedule = null,
        int? limit = null,
        DontProofMode? proofMode = null,
        DateOnly? targetDate = null,
        bool descriptionChanged = false,
        IReadOnlyList<Guid>? removed = null) =>
        new(schedule, limit, proofMode, targetDate, descriptionChanged, descriptionChanged ? "Neu" : null, removed ?? []);

    private static Goal Do(GoalSchedule schedule) =>
        Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            "target",
            schedule,
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    private static Goal Dont(int limit, QuotaPeriod period, DontProofMode mode) =>
        Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            "target",
            GoalSchedule.Per(period),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            GoalKind.Dont,
            limit,
            mode);
}
