using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// The four shapes a commitment can have, and the windows they produce.
/// </summary>
/// <remarks>
/// Pure: no clock, no database. Every date is passed in, which is what makes
/// "what happens on the Sunday of a month boundary" a test rather than a
/// conversation.
/// </remarks>
public class GoalScheduleTests
{
    /// <summary>A Monday, so weekday arithmetic is readable in the assertions.</summary>
    private static readonly DateOnly Monday = new(2026, 6, 1);

    public static TheoryData<GoalSchedule> RepeatingSchedules =>
    [
        GoalSchedule.EveryNDays(1),
        GoalSchedule.EveryNDays(4),
        GoalSchedule.OnWeekdays([Weekday.Tuesday, Weekday.Saturday]),
        GoalSchedule.TimesPer(3, QuotaPeriod.Week),
        GoalSchedule.TimesPer(2, QuotaPeriod.Month),
    ];

    [Fact]
    public void AnIntervalMustBeAtLeastOneDay()
    {
        Assert.Throws<DomainValidationException>(() => GoalSchedule.EveryNDays(0));
        Assert.Throws<DomainValidationException>(() => GoalSchedule.EveryNDays(GoalSchedule.MaxEveryDays + 1));
    }

    [Fact]
    public void WeekdaysAreSortedDeduplicatedAndNeverEmpty()
    {
        var schedule = GoalSchedule.OnWeekdays([Weekday.Friday, Weekday.Monday, Weekday.Friday]);

        Assert.Equal([Weekday.Monday, Weekday.Friday], schedule.Weekdays);
        Assert.Equal("1,5", schedule.WeekdayList);
        Assert.Throws<DomainValidationException>(() => GoalSchedule.OnWeekdays([]));
    }

    [Fact]
    public void AQuotaIsTheOnlyKindThatNeedsMoreThanOneProof()
    {
        Assert.Equal(1, GoalSchedule.EveryNDays(1).RequiredProofs);
        Assert.Equal(1, GoalSchedule.Once().RequiredProofs);
        Assert.Equal(1, GoalSchedule.OnWeekdays([Weekday.Monday]).RequiredProofs);
        Assert.Equal(3, GoalSchedule.TimesPer(3, QuotaPeriod.Week).RequiredProofs);
    }

    [Fact]
    public void OnlyAOneOffStopsAfterItsWindow()
    {
        Assert.False(GoalSchedule.Once().Repeats);
        Assert.True(GoalSchedule.EveryNDays(2).Repeats);
    }

    /// <summary>
    /// An interval starts today, because somebody who commits to something
    /// today means today.
    /// </summary>
    [Fact]
    public void AnIntervalWindowIsTheSingleDayItStartsOn()
    {
        var window = GoalSchedule.EveryNDays(3).FirstWindow(Monday, dueOn: null);

        Assert.Equal(Monday, window.Start);
        Assert.Equal(Monday, window.End);
        Assert.Equal(1, window.RequiredProofs);
    }

    [Fact]
    public void AnIntervalCountsFromTheDayThatJustClosed()
    {
        var next = GoalSchedule.EveryNDays(3).NextWindow(Monday);

        Assert.Equal(Monday.AddDays(3), next!.Value.Start);
    }

    [Fact]
    public void AWeekdayScheduleWaitsForTheNextChosenDay()
    {
        // Wednesday and Friday, asked on the Monday.
        var schedule = GoalSchedule.OnWeekdays([Weekday.Wednesday, Weekday.Friday]);

        var first = schedule.FirstWindow(Monday, dueOn: null);
        Assert.Equal(Monday.AddDays(2), first.Start);

        var next = schedule.NextWindow(first.End);
        Assert.Equal(Monday.AddDays(4), next!.Value.Start);

        // And round the corner of the week, back to Wednesday.
        var afterFriday = schedule.NextWindow(next.Value.End);
        Assert.Equal(Monday.AddDays(9), afterFriday!.Value.Start);
    }

    /// <summary>
    /// The window is the calendar week, not seven days from now: "three times
    /// this week" has to mean the week everybody else means.
    /// </summary>
    [Fact]
    public void AWeeklyQuotaCoversMondayToSunday()
    {
        var schedule = GoalSchedule.TimesPer(3, QuotaPeriod.Week);

        // Asked on the Thursday of that week.
        var window = schedule.WindowAround(Monday.AddDays(3), dueOn: null);

        Assert.Equal(Monday, window.Start);
        Assert.Equal(Monday.AddDays(6), window.End);
        Assert.Equal(3, window.RequiredProofs);

        var next = schedule.NextWindow(window.End);
        Assert.Equal(Monday.AddDays(7), next!.Value.Start);
        Assert.Equal(Monday.AddDays(13), next.Value.End);
    }

    [Fact]
    public void AMonthlyQuotaCoversTheWholeMonthIncludingAShortOne()
    {
        var schedule = GoalSchedule.TimesPer(2, QuotaPeriod.Month);

        var february = schedule.WindowAround(new DateOnly(2028, 2, 14), dueOn: null);

        Assert.Equal(new DateOnly(2028, 2, 1), february.Start);

        // 2028 is a leap year, which is the case a hard-coded 28 would fail.
        Assert.Equal(new DateOnly(2028, 2, 29), february.End);

        var march = schedule.NextWindow(february.End);
        Assert.Equal(new DateOnly(2028, 3, 1), march!.Value.Start);
        Assert.Equal(new DateOnly(2028, 3, 31), march.Value.End);
    }

    /// <summary>
    /// A new goal's first window never asks for what the days left cannot hold
    /// (#96): a quota started late in its period is cut to begin that day and
    /// asks for its share, rounded up, at least one.
    /// </summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 2)]
    [InlineData(6, 1)]
    public void AFirstQuotaWindowStartsWithTheGoalAndAsksForItsShare(int daysIntoWeek, int required)
    {
        var start = Monday.AddDays(daysIntoWeek);
        var window = GoalSchedule.TimesPer(3, QuotaPeriod.Week).FirstWindow(start, dueOn: null);

        Assert.Equal(start, window.Start);
        Assert.Equal(Monday.AddDays(6), window.End);
        Assert.Equal(required, window.RequiredProofs);
    }

    [Fact]
    public void AFirstMonthlyWindowOnTheLastDaysAsksForOne()
    {
        var window = GoalSchedule.TimesPer(2, QuotaPeriod.Month).FirstWindow(new DateOnly(2028, 2, 28), dueOn: null);

        Assert.Equal(new DateOnly(2028, 2, 28), window.Start);
        Assert.Equal(new DateOnly(2028, 2, 29), window.End);
        Assert.Equal(1, window.RequiredProofs);
    }

    [Fact]
    public void AFirstDontPeriodStartsWithTheGoalToo()
    {
        // Its limit is scaled to the days by the goal that opens it (DontTests).
        var sunday = Monday.AddDays(6);
        var window = GoalSchedule.Per(QuotaPeriod.Week).FirstWindow(sunday, dueOn: null);

        Assert.Equal(sunday, window.Start);
        Assert.Equal(sunday, window.End);
    }

    [Fact]
    public void AGoalStartedTomorrowHasItsWholeFirstDayTomorrow()
    {
        var tomorrow = Monday.AddDays(1);

        Assert.Equal(tomorrow, GoalSchedule.EveryNDays(1).FirstWindow(tomorrow, dueOn: null).Start);

        // Sunday evening, "ab morgen": the whole next week, all three.
        var week = GoalSchedule.TimesPer(3, QuotaPeriod.Week).FirstWindow(Monday.AddDays(7), dueOn: null);
        Assert.Equal(3, week.RequiredProofs);
    }

    /// <summary>
    /// "Once, by that day" (ADR 0016): open from the day it is made to the day
    /// it is due, so it can be done before then (#94). A window on the due day
    /// alone took no proof until that day came.
    /// </summary>
    [Fact]
    public void AOneOffRunsFromTodayToTheChosenDayAndHasNothingAfterIt()
    {
        var due = Monday.AddDays(10);
        var window = GoalSchedule.Once().FirstWindow(Monday, due);

        Assert.Equal(Monday, window.Start);
        Assert.Equal(due, window.End);
        Assert.Equal(1, window.RequiredProofs);
        Assert.Null(GoalSchedule.Once().NextWindow(due));
    }

    [Fact]
    public void AOneOffDueTodayIsTodayAlone()
    {
        var window = GoalSchedule.Once().FirstWindow(Monday, Monday);

        Assert.Equal(Monday, window.Start);
        Assert.Equal(Monday, window.End);
    }

    [Fact]
    public void AOneOffWithoutAChosenDayIsDueToday()
    {
        Assert.Equal(Monday, GoalSchedule.Once().FirstWindow(Monday, dueOn: null).End);
    }

    /// <summary>
    /// Stepping back and forward again has to land where it started, or a
    /// seeded history would drift away from the windows the job produces.
    /// </summary>
    [Theory]
    [MemberData(nameof(RepeatingSchedules))]
    public void SteppingBackAndForwardIsARoundTrip(GoalSchedule schedule)
    {
        var current = schedule.WindowAround(Monday.AddDays(3), dueOn: null);

        var previous = schedule.PreviousWindow(current.Start);
        Assert.NotNull(previous);

        var forwardAgain = schedule.NextWindow(previous.Value.End);
        Assert.Equal(current, forwardAgain);
    }

    [Fact]
    public void AOneOffHasNothingBeforeItEither()
    {
        Assert.Null(GoalSchedule.Once().PreviousWindow(Monday));
    }

    [Fact]
    public void WeekdaysAreNumberedFromMonday()
    {
        Assert.Equal(Weekday.Monday, GoalSchedule.WeekdayOf(Monday));
        Assert.Equal(Weekday.Sunday, GoalSchedule.WeekdayOf(Monday.AddDays(6)));
        Assert.Equal(Monday, GoalSchedule.MondayOf(Monday.AddDays(6)));
    }
}
