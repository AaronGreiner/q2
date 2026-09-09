using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// What happens to a goal's windows while nobody is looking.
/// </summary>
/// <remarks>
/// The two properties the plan asks for are the two things asserted hardest
/// here: running twice must change nothing the second time, and coming back
/// after an outage must work through every day that passed rather than
/// forgiving them.
/// </remarks>
public class GoalMaintenanceTests
{
    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    /// <summary>A Monday, in a zone that has daylight saving.</summary>
    private static readonly DateOnly Monday = new(2026, 6, 1);

    private static readonly LocalCalendar Berlin = new(Zone("Europe/Berlin"));

    [Fact]
    public void ANewGoalGetsItsFirstWindow()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));

        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids));

        var window = Assert.Single(goal.Instances);
        Assert.Equal(Monday, window.StartsOn);
        Assert.Equal(Monday, window.DueOn);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
    }

    [Fact]
    public void NothingHappensWhileTheWindowIsStillRunning()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids));
        Assert.Single(goal.Instances);
    }

    [Fact]
    public void AnExpiredWindowIsMissedAndTheNextOneOpens()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(1)), Ids));

        Assert.Equal(2, goal.Instances.Count);
        Assert.Equal(GoalInstanceStatus.Missed, goal.Instances[0].Status);
        Assert.Equal(Monday.AddDays(1), goal.CurrentInstance!.DueOn);
        Assert.Equal(0, goal.Streak);
    }

    /// <summary>
    /// The plan's requirement in one test: after two days down, both days are
    /// worked through. An outage must not quietly forgive anybody.
    /// </summary>
    [Fact]
    public void ComingBackAfterAnOutageWorksThroughEveryDayThatPassed()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        // Three days later, in one pass.
        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(3)), Ids));

        Assert.Equal(4, goal.Instances.Count);
        Assert.Equal(
            [Monday, Monday.AddDays(1), Monday.AddDays(2), Monday.AddDays(3)],
            goal.Instances.Select(instance => instance.DueOn).Order());

        Assert.Equal(3, goal.Instances.Count(instance => instance.Status == GoalInstanceStatus.Missed));
        Assert.Equal(Monday.AddDays(3), goal.CurrentInstance!.DueOn);
    }

    [Fact]
    public void RunningItTwiceChangesNothingTheSecondTime()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        var now = Noon(Monday.AddDays(4));
        GoalMaintenance.Advance(goal, Berlin, now, Ids);
        var after = goal.Instances.Select(instance => (instance.Id, instance.DueOn, instance.Status)).ToList();

        Assert.False(GoalMaintenance.Advance(goal, Berlin, now, Ids));
        Assert.Equal(after, goal.Instances.Select(instance => (instance.Id, instance.DueOn, instance.Status)));
    }

    [Fact]
    public void AWeekdayScheduleSkipsTheDaysItIsNotDueOn()
    {
        // Monday and Wednesday only.
        var goal = Build(GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Wednesday]));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(2)), Ids);

        Assert.Equal(
            [Monday, Monday.AddDays(2)],
            goal.Instances.Select(instance => instance.DueOn).Order());
    }

    [Fact]
    public void AQuotaWindowSurvivesTheWholeWeekAndThenRollsOver()
    {
        var goal = Build(GoalSchedule.TimesPer(3, QuotaPeriod.Week));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        // Still the same window on the Saturday.
        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(5)), Ids));
        Assert.Single(goal.Instances);
        Assert.Equal(3, goal.CurrentInstance!.RemainingProofs);

        // And a new one on the Monday after.
        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(7)), Ids));
        Assert.Equal(Monday.AddDays(7), goal.CurrentInstance!.StartsOn);
        Assert.Equal(Monday.AddDays(13), goal.CurrentInstance.DueOn);
    }

    [Fact]
    public void AOneOffGetsNoSecondWindow()
    {
        var goal = Build(GoalSchedule.Once(), targetDate: Monday);
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(5)), Ids);

        var window = Assert.Single(goal.Instances);
        Assert.Equal(GoalInstanceStatus.Missed, window.Status);
        Assert.Null(goal.CurrentInstance);
    }

    [Fact]
    public void AnArchivedGoalIsLeftAlone()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        goal.Close(completed: false, Noon(Monday));

        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids));
        Assert.Empty(goal.Instances);
    }

    /// <summary>
    /// The deadline is local midnight, not UTC midnight. In Berlin in June that
    /// is 22:00 the previous day in UTC, and a window that ended at UTC midnight
    /// would take two hours of somebody's evening away.
    /// </summary>
    [Fact]
    public void AWindowEndsAtLocalMidnightRatherThanUtcMidnight()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        var window = Assert.Single(goal.Instances);

        // 1 June 2026 in Berlin is UTC+2.
        Assert.Equal(new DateTimeOffset(2026, 5, 31, 22, 0, 0, TimeSpan.Zero), window.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero), window.DueAt.AddTicks(1));
    }

    [Fact]
    public void NothingElapsesWhileAPauseRuns()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);
        goal.RequestPause(Guid.CreateVersion7(), "Grippe, seit Freitag im Bett.", 3, Monday, Berlin.EndOfDay, Noon(Monday));

        // Three days later, and still nothing: no new window, and the one that
        // was set aside is not a miss.
        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(2)), Ids));

        var window = Assert.Single(goal.Instances);
        Assert.Equal(GoalInstanceStatus.Paused, window.Status);
    }

    /// <summary>
    /// The day the pause ends, the goal picks up again — and the days it
    /// covered leave a gap rather than a row of failures. Creating and missing
    /// them is exactly what the pause was granted to prevent.
    /// </summary>
    [Fact]
    public void TheDaysAPauseCoveredNeverBecomeWindows()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);
        goal.RequestPause(Guid.CreateVersion7(), "Grippe, seit Freitag im Bett.", 3, Monday, Berlin.EndOfDay, Noon(Monday));

        var thursday = Monday.AddDays(3);
        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(thursday), Ids));

        Assert.Equal(2, goal.Instances.Count);
        Assert.Equal(GoalInstanceStatus.Paused, goal.Instances[0].Status);
        Assert.Equal(thursday, goal.CurrentInstance!.DueOn);
        Assert.Equal(0, goal.Balance.Missed);
    }

    [Fact]
    public void APauseIsClosedOnceItsLastDayHasGone()
    {
        var goal = Build(GoalSchedule.EveryNDays(1));
        goal.RequestPause(Guid.CreateVersion7(), "Grippe, seit Freitag im Bett.", 1, Monday, Berlin.EndOfDay, Noon(Monday));

        GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(1)), Ids);

        Assert.Equal(PauseStatus.Ended, Assert.Single(goal.Pauses).Status);
    }

    private static Goal Build(GoalSchedule schedule, DateOnly? targetDate = null) =>
        Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            "target",
            schedule,
            isGroup: false,
            reminderAt: null,
            targetDate,

            // Created at the start of the Monday, so "the first window" has an
            // unambiguous day to be derived from.
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    private static DateTimeOffset Noon(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    /// <summary>
    /// Falls back to UTC where the platform has no zone database — see
    /// <see cref="TimeZoneResolver"/>. The assertions that depend on a real
    /// offset name it, and there is one of those.
    /// </summary>
    private static TimeZoneInfo Zone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static IIdGenerator Ids { get; } = new SequentialIdGenerator();
}
