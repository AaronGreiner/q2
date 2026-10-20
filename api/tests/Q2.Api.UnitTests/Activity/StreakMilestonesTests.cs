using Q2.Api.Features.Activity;
using Q2.Api.Features.People;
using Q2.Api.Features.Streaks;

namespace Q2.Api.UnitTests.Activity;

/// <summary>
/// Which streaks friends hear about (#90): few, and each once per run.
/// </summary>
public sealed class StreakMilestonesTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    [Theory]
    [InlineData(6, 7, 7)]
    [InlineData(29, 30, 30)]
    [InlineData(99, 100, 100)]
    [InlineData(364, 365, 365)]
    public void TheDayThatReachesAMilestoneSaysSo(int before, int after, int milestone)
    {
        Assert.Equal(milestone, StreakMilestones.ReachedBy(new StreakGrowth(before, after)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 8)]
    [InlineData(13, 14)]
    [InlineData(30, 31)]
    public void AnyOtherDaySaysNothing(int before, int after)
    {
        Assert.Null(StreakMilestones.ReachedBy(new StreakGrowth(before, after)));
    }

    [Fact]
    public void ADayThatCarriesARunPastTwoSaysOnlyTheHigher()
    {
        // Two runs of five and twenty-eight days joined by one late day.
        Assert.Equal(30, StreakMilestones.ReachedBy(new StreakGrowth(28, 34)));
    }

    [Fact]
    public void ACheckInRemembersWhatItDidUntilItIsTaken()
    {
        var person = Person.Create(Guid.CreateVersion7(), "Lena Schulz", "@lena", "LS", AvatarColors.Teal);

        for (var day = 6; day >= 1; day--)
        {
            person.CheckIn(Guid.CreateVersion7(), Today.AddDays(-day));
        }

        Assert.Equal(6, person.TakeStreakGrowth().Count);

        person.CheckIn(Guid.CreateVersion7(), Today);
        person.CheckIn(Guid.CreateVersion7(), Today);

        // The second check-in on a day grew nothing, and taking forgets.
        var growth = Assert.Single(person.TakeStreakGrowth());
        Assert.Equal(7, StreakMilestones.ReachedBy(growth));
        Assert.Empty(person.TakeStreakGrowth());
    }
}
