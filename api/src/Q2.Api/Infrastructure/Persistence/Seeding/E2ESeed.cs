using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fixture for the Playwright suite.
/// </summary>
/// <remarks>
/// Titles are unique, prefixed with <c>E2E</c> and never a substring of one
/// another, so a locator can match on text without becoming ambiguous once a
/// test creates its own goals. Ids are stable across runs
/// (<c>e2e00000-0000-4000-8000-000000000001</c> and up).
///
/// Like the automated-test seed it contains no archived goal, which keeps
/// "filter to a status with no results" available as an empty-state check.
/// </remarks>
public sealed class E2ESeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.E2E;

    public SeedProfile Profile => Owner;

    public string Description => "4 goals with stable ids and unique titles for Playwright.";

    /// <summary>Shared active goal — the row E2E tests open and assert on.</summary>
    public static Guid SharedGoalId => SeedIds.Goal(Owner, 1);

    public const string SharedGoalTitle = "E2E shared goal with participants";

    public const string CompletedGoalTitle = "E2E completed goal";

    public const string ZeroProgressGoalTitle = "E2E goal without any progress";

    public const string OverdueGoalTitle = "E2E overdue goal";

    public IReadOnlyList<Goal> CreateGoals(SeedContext context) =>
    [
        SeedGoals.Build(
            Owner,
            1,
            SharedGoalTitle,
            "Synthetic E2E fixture.",
            progressPercent: 50,
            targetDate: context.DaysFromToday(30),
            createdAt: context.DaysAgo(10),
            "E2E Participant One",
            "E2E Participant Two"),

        SeedGoals.Build(
            Owner,
            2,
            CompletedGoalTitle,
            null,
            progressPercent: 100,
            targetDate: context.DaysFromToday(5),
            createdAt: context.DaysAgo(20)),

        SeedGoals.Build(
            Owner,
            3,
            ZeroProgressGoalTitle,
            null,
            progressPercent: 0,
            targetDate: null,
            createdAt: context.DaysAgo(2)),

        SeedGoals.Build(
            Owner,
            4,
            OverdueGoalTitle,
            null,
            progressPercent: 25,
            targetDate: context.DaysFromToday(-7),
            createdAt: context.DaysAgo(40)),
    ];
}
