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
    public void ADayAfterARunGrowsIt()
    {
        var days = new[] { Today.AddDays(-1), Today.AddDays(-2) };

        Assert.Equal(new StreakGrowth(2, 3), Streak.GrowthFrom(days, Today));
    }

    [Fact]
    public void ADayThatJoinsTwoRunsCountsTheLongerOneAsBefore()
    {
        // A late day between last week and today: the longer half had already
        // reached its length, so only what is new beyond it counts as reached.
        var days = new[] { Today.AddDays(-2), Today.AddDays(-3), Today, Today.AddDays(1), Today.AddDays(2) };

        Assert.Equal(new StreakGrowth(3, 6), Streak.GrowthFrom(days, Today.AddDays(-1)));
    }

    [Fact]
    public void ADayThatIsAlreadyThereGrowsNothing()
    {
        Assert.Null(Streak.GrowthFrom([Today], Today));
    }

    [Fact]
    public void TheLongestRunIsFoundWhereverItIs()
    {
        var days = new[] { Today, Today.AddDays(-10), Today.AddDays(-11), Today.AddDays(-12), Today.AddDays(-20) };

        Assert.Equal(3, Streak.Longest(days));
        Assert.Equal(0, Streak.Longest([]));
    }

    [Theory]
    [InlineData(6, 7, true)]
    [InlineData(7, 8, false)]
    [InlineData(0, 7, true)]
    [InlineData(7, 7, false)]
    public void ALengthIsReachedOnlyByTheDayThatCrossesIt(int before, int after, bool reached)
    {
        Assert.Equal(reached, new StreakGrowth(before, after).Reached(7));
    }
}
