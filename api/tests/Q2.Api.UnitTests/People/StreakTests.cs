using Q2.Api.Features.Streaks;

namespace Q2.Api.UnitTests.People;

/// <summary>
/// The number q2 puts in front of people most often.
/// </summary>
public class StreakTests
{
    // A Wednesday, so the Monday-first week has days on both sides of it.
    private static readonly DateOnly Today = new(2026, 6, 17);

    [Fact]
    public void NoDaysIsNoStreak()
    {
        Assert.Equal(0, Streak.Count([], Today));
    }

    [Fact]
    public void ConsecutiveDaysEndingTodayAreCounted()
    {
        var days = new[] { Today, Today.AddDays(-1), Today.AddDays(-2) };

        Assert.Equal(3, Streak.Count(days, Today));
    }

    [Fact]
    public void AStreakThatEndedYesterdayStillCounts()
    {
        // Opening the app before breakfast must not announce that a run is over
        // before the day has had any chance to happen.
        var days = new[] { Today.AddDays(-1), Today.AddDays(-2) };

        Assert.Equal(2, Streak.Count(days, Today));
    }

    [Fact]
    public void AStreakThatEndedTheDayBeforeYesterdayIsOver()
    {
        var days = new[] { Today.AddDays(-2), Today.AddDays(-3) };

        Assert.Equal(0, Streak.Count(days, Today));
    }

    [Fact]
    public void AGapEndsTheRunEvenWhenOlderDaysExist()
    {
        var days = new[] { Today, Today.AddDays(-1), Today.AddDays(-5), Today.AddDays(-6) };

        Assert.Equal(2, Streak.Count(days, Today));
    }

    [Fact]
    public void ADuplicateDayDoesNotCountTwice()
    {
        var days = new[] { Today, Today, Today.AddDays(-1) };

        Assert.Equal(2, Streak.Count(days, Today));
    }

    [Fact]
    public void TheWeekAlwaysHasSevenEntriesStartingOnMonday()
    {
        var week = Streak.Week([Today], Today);

        Assert.Equal(7, week.Count);

        // Wednesday is the third entry when the week starts on Monday. Getting
        // this wrong is silent: the squares still render, on the wrong days.
        Assert.Equal([false, false, true, false, false, false, false], week);
    }

    [Fact]
    public void TheWeekOfASundayIsTheWeekThatEndsOnIt()
    {
        var sunday = new DateOnly(2026, 6, 21);

        var week = Streak.Week([sunday], sunday);

        Assert.Equal([false, false, false, false, false, false, true], week);
    }

    [Fact]
    public void DaysOutsideTheCurrentWeekAreNotShown()
    {
        var week = Streak.Week([Today.AddDays(-14)], Today);

        Assert.DoesNotContain(true, week);
    }
}
