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

    /// <summary>
    /// "Ab morgen" (#96): nothing to deliver into today, and tomorrow's window
    /// is the first — not today's, missed on the way.
    /// </summary>
    [Fact]
    public void AGoalStartingTomorrowOpensNothingTodayAndTomorrowsWindowFirst()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(), Owner, "Anything", null, GoalCategory.Other, GoalSchedule.EveryNDays(1), false, null, null,
            new DateTimeOffset(Monday.ToDateTime(new TimeOnly(20, 50)), TimeSpan.Zero),
            startsOn: Monday.AddDays(1));

        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday).AddHours(9), Ids));
        Assert.Empty(goal.Instances);

        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(1)), Ids));
        var window = Assert.Single(goal.Instances);
        Assert.Equal(Monday.AddDays(1), window.StartsOn);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
    }

    [Fact]
    public void AOneOffIgnoresAStartDay()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(), Owner, "Anything", null, GoalCategory.Other, GoalSchedule.Once(), false, null, Monday.AddDays(3),
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            startsOn: Monday.AddDays(1));

        Assert.Null(goal.StartsOn);
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

    /// <summary>
    /// A one-off has no next window to pick up with after a pause, so it gets
    /// the one the pause set aside back — later by exactly the days the pause
    /// covered, because those days do not count (#129).
    /// </summary>
    [Fact]
    public void AOneOffGetsItsWindowBackLaterByThePausedDays()
    {
        var saturday = Monday.AddDays(5);
        var goal = Build(GoalSchedule.Once(), targetDate: saturday);
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        goal.RequestPause(Guid.CreateVersion7(), PauseReason, 3, Monday, Berlin.EndOfDay, Noon(Monday));

        // Monday to Wednesday were paused; on Thursday the window is back.
        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(3)), Ids));

        var window = Assert.Single(goal.Instances);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
        Assert.Equal(saturday.AddDays(3), window.DueOn);
        Assert.Equal(Berlin.EndOfDay(saturday.AddDays(3)), window.DueAt);
        Assert.Same(window, goal.CurrentInstance);

        // Once is enough: the next pass has nothing left to hand back.
        Assert.False(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(3)), Ids));
        Assert.Equal(saturday.AddDays(3), window.DueOn);
    }

    /// <summary>
    /// Ending a pause early moves the deadline by the days it actually took,
    /// today included — not by the days it was asked for.
    /// </summary>
    [Fact]
    public void AOneOffWhosePauseEndedEarlyIsMovedByTheDaysItTook()
    {
        var saturday = Monday.AddDays(5);
        var goal = Build(GoalSchedule.Once(), targetDate: saturday);
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        goal.RequestPause(Guid.CreateVersion7(), PauseReason, 5, Monday, Berlin.EndOfDay, Noon(Monday));
        var tuesday = Monday.AddDays(1);
        goal.EndPause(tuesday, Noon(tuesday));

        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(tuesday), Ids));

        var window = Assert.Single(goal.Instances);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
        Assert.Equal(saturday.AddDays(2), window.DueOn);
    }

    /// <summary>
    /// A host that was down past even the moved deadline catches up honestly:
    /// the window comes back and is missed, rather than staying set aside.
    /// </summary>
    [Fact]
    public void AOneOffWhoseMovedDeadlineHasPassedIsMissed()
    {
        var goal = Build(GoalSchedule.Once(), targetDate: Monday);
        GoalMaintenance.Advance(goal, Berlin, Noon(Monday), Ids);

        goal.RequestPause(Guid.CreateVersion7(), PauseReason, 1, Monday, Berlin.EndOfDay, Noon(Monday));

        Assert.True(GoalMaintenance.Advance(goal, Berlin, Noon(Monday.AddDays(10)), Ids));

        var window = Assert.Single(goal.Instances);
        Assert.Equal(Monday.AddDays(1), window.DueOn);
        Assert.Equal(GoalInstanceStatus.Missed, window.Status);
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

    /// <summary>
    /// Made after midnight in Berlin, which is still the day before in UTC: the
    /// first window is the owner's day, open, and nothing is settled behind it.
    /// Taking the UTC day opened yesterday's window and settled it at once — a
    /// miss for a Do, a kept day for a Don't (#132).
    /// </summary>
    [Theory]
    [InlineData(GoalKind.Do)]
    [InlineData(GoalKind.Dont)]
    public void AGoalMadeAfterLocalMidnightStartsOnThatDay(GoalKind kind)
    {
        var tuesday = Monday.AddDays(1);

        // 00:30 on Tuesday in Berlin (summer time), 22:30 on Monday in UTC.
        var madeAt = new DateTimeOffset(tuesday.ToDateTime(new TimeOnly(0, 30)), TimeSpan.FromHours(2));
        Assert.Equal(Monday, DateOnly.FromDateTime(madeAt.UtcDateTime));

        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            GoalCategory.Other,
            kind == GoalKind.Dont ? GoalSchedule.Per(QuotaPeriod.Day) : GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            madeAt,
            kind,
            limit: kind == GoalKind.Dont ? 1 : null,
            proofMode: kind == GoalKind.Dont ? DontProofMode.HonestOnly : null);

        GoalMaintenance.Advance(goal, Berlin, madeAt, Ids);

        var window = Assert.Single(goal.Instances);
        Assert.Equal(tuesday, window.StartsOn);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
        Assert.Equal(0, goal.Balance.Done);
        Assert.Equal(0, goal.Balance.Missed);
    }

    /// <summary>
    /// A Do that ends at a time of day (#98): its window's deadline is that
    /// time in the owner's zone, and it is missed then rather than at midnight.
    /// </summary>
    [Fact]
    public void ADayThatEndsAtSevenEndsAtSevenInTheOwnersZone()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Vor 7 Uhr aufstehen",
            null,
            GoalCategory.Other,
            GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: new TimeOnly(6, 30),
            targetDate: null,
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            dueBy: new TimeOnly(7, 0));

        GoalMaintenance.Advance(goal, Berlin, Berlin.StartOfDay(Monday).AddHours(6), Ids);

        var window = Assert.Single(goal.Instances);
        Assert.Equal(Berlin.AtOrAfter(Monday, new TimeOnly(7, 0)), window.DueAt);
        Assert.Equal(GoalInstanceStatus.Open, window.Status);

        // A minute past seven, local: missed, and tomorrow's has not begun.
        GoalMaintenance.Advance(goal, Berlin, Berlin.StartOfDay(Monday).AddHours(7).AddMinutes(1), Ids);

        Assert.Equal(GoalInstanceStatus.Missed, window.Status);
        Assert.Single(goal.Instances);
    }

    private const string PauseReason = "Grippe, seit Freitag im Bett.";

    private static Goal Build(GoalSchedule schedule, DateOnly? targetDate = null) =>
        Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            GoalCategory.Other,
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
    /// offset name it, and there are two of those.
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
