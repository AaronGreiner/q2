using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.UnitTests.Persistence;

/// <summary>
/// Seeds are pure functions of a <see cref="SeedContext"/>, so their content
/// can be asserted without a database.
/// </summary>
[Trait("Category", "Seed")]
public class SeedDataTests
{
    private static readonly SeedContext Context =
        new(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    private static IReadOnlyList<ISeedDataSource> Seeds =>
    [
        new DevelopmentSeed(),
        new ManualTestingSeed(),
        new AutomatedTestSeed(),
        new E2ESeed(),
    ];

    public static TheoryData<ISeedDataSource> AllSeeds
    {
        get
        {
            var data = new TheoryData<ISeedDataSource>();
            foreach (var seed in Seeds)
            {
                data.Add(seed);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllSeeds))]
    public void ASeedProducesTheSameDataEveryTime(ISeedDataSource seed)
    {
        var first = seed.CreateGoals(Context);
        var second = seed.CreateGoals(Context);

        Assert.Equal(
            first.Select(g => (g.Id, g.Title, g.Status, g.ProgressPercent, g.TargetDate, g.CreatedAt)),
            second.Select(g => (g.Id, g.Title, g.Status, g.ProgressPercent, g.TargetDate, g.CreatedAt)));

        Assert.Equal(
            first.SelectMany(g => g.Participants.Select(p => p.Id)),
            second.SelectMany(g => g.Participants.Select(p => p.Id)));
    }

    [Theory]
    [MemberData(nameof(AllSeeds))]
    public void SeedIdsAreStableAndDoNotOverlapBetweenProfiles(ISeedDataSource seed)
    {
        var ids = seed.CreateGoals(Context).Select(g => g.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public void ProfilesDoNotShareIds()
    {
        var all = Seeds
            .SelectMany(seed => seed.CreateGoals(Context).Select(g => g.Id))
            .ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllSeeds))]
    public void EverySeedCoversTheStatesTheUiHasToRender(ISeedDataSource seed)
    {
        var goals = seed.CreateGoals(Context);

        Assert.Contains(goals, g => g.Status == GoalStatus.Active);
        Assert.Contains(goals, g => g.Status == GoalStatus.Completed);
        Assert.Contains(goals, g => g.ProgressPercent == 0);
        Assert.Contains(goals, g => g.ProgressPercent is > 0 and < 100);
        Assert.Contains(goals, g => g.ProgressPercent == 100);
        Assert.Contains(goals, g => g.TargetDate is not null);
        Assert.Contains(goals, g => g.TargetDate is null);
        Assert.Contains(goals, g => g.Participants.Count > 1);
    }

    [Theory]
    [MemberData(nameof(AllSeeds))]
    public void SeedDataCarriesNothingThatLooksLikeARealPersonOrASecret(ISeedDataSource seed)
    {
        var text = string.Join(
            ' ',
            seed.CreateGoals(Context).SelectMany(g =>
                new[] { g.Title, g.Description ?? string.Empty }
                    .Concat(g.Participants.Select(p => p.DisplayName))));

        Assert.DoesNotContain('@', text);
        foreach (var forbidden in new[] { "password", "secret", "token", "api-key" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheManualTestingSeedCoversTheBoundaryAndAwkwardCases()
    {
        var goals = new ManualTestingSeed().CreateGoals(Context);

        // The comment in ManualTestingSeed claims "exactly the maximum length".
        Assert.Contains(goals, g => g.Title.Length == Goal.MaxTitleLength);
        Assert.Contains(goals, g => g.Status == GoalStatus.Archived);
        Assert.Contains(goals, g => g.IsOverdue(Context.Today));
        Assert.Contains(goals, g => g.Participants.Count >= 5);
        Assert.Contains(goals, g => g.Description is { Length: > 300 });
        Assert.Contains(goals, g => g.Title.Any(c => c > 127));
    }

    [Fact]
    public void TheAutomatedTestSeedLeavesTheArchivedFilterEmpty()
    {
        var goals = new AutomatedTestSeed().CreateGoals(Context);

        // Keeps "a status with no results" available as an empty-state check.
        Assert.DoesNotContain(goals, g => g.Status == GoalStatus.Archived);
    }

    [Fact]
    public void TheE2eSeedExposesTheIdsAndTitlesPlaywrightRelieson()
    {
        var goals = new E2ESeed().CreateGoals(Context);

        Assert.Contains(goals, g => g.Id == E2ESeed.SharedGoalId && g.Title == E2ESeed.SharedGoalTitle);
        Assert.Contains(goals, g => g.Title == E2ESeed.CompletedGoalTitle);
        Assert.Contains(goals, g => g.Title == E2ESeed.ZeroProgressGoalTitle);
        Assert.Contains(goals, g => g.Title == E2ESeed.OverdueGoalTitle);

        // Locators match on text, so no title may be a substring of another.
        var titles = goals.Select(g => g.Title).ToList();
        Assert.All(titles, title =>
            Assert.DoesNotContain(titles, other => other != title && other.Contains(title, StringComparison.Ordinal)));
    }

    [Fact]
    public void DatesAreRelativeToTheSeedContextRatherThanTheWallClock()
    {
        var april = new SeedContext(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        var december = new SeedContext(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var overdueInApril = new E2ESeed().CreateGoals(april).Single(g => g.Title == E2ESeed.OverdueGoalTitle);
        var overdueInDecember = new E2ESeed().CreateGoals(december).Single(g => g.Title == E2ESeed.OverdueGoalTitle);

        // The absolute date moves with the reference instant, but the business
        // state ("this goal is overdue") is identical on every rebuild.
        Assert.NotEqual(overdueInApril.TargetDate, overdueInDecember.TargetDate);
        Assert.True(overdueInApril.IsOverdue(april.Today));
        Assert.True(overdueInDecember.IsOverdue(december.Today));
    }

    [Fact]
    public void EachProfileHasExactlyOneSource()
    {
        var profiles = Seeds.Select(seed => seed.Profile).ToList();

        Assert.Equal(profiles.Count, profiles.Distinct().Count());
        Assert.DoesNotContain(SeedProfile.None, profiles);
    }
}
