using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The smallest data set that still covers every branch the API tests care
/// about: one active goal with participants, one completed goal, one goal
/// without a target date.
/// </summary>
/// <remarks>
/// Deliberately contains <em>no</em> archived goal, so
/// <c>GET /api/goals?status=Archived</c> returns an empty list and the empty
/// state stays testable. Tests that need more may arrange extra rows
/// themselves — this seed is a floor, not a ceiling.
/// </remarks>
public sealed class AutomatedTestSeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.AutomatedTest;

    public SeedProfile Profile => Owner;

    public string Description => "3 goals: active with participants, completed, active without target date.";

    /// <summary>Stable id of the active seeded goal, used by GET-by-id tests.</summary>
    public static Guid ActiveGoalId => SeedIds.Goal(Owner, 1);

    /// <summary>Stable id of the completed seeded goal.</summary>
    public static Guid CompletedGoalId => SeedIds.Goal(Owner, 2);

    /// <summary>Stable id of the goal that has no target date.</summary>
    public static Guid GoalWithoutTargetDateId => SeedIds.Goal(Owner, 3);

    public IReadOnlyList<Goal> CreateGoals(SeedContext context) =>
    [
        SeedGoals.Build(
            Owner,
            1,
            "Automated test: shared active goal",
            "Synthetic test data.",
            progressPercent: 40,
            targetDate: context.DaysFromToday(20),
            createdAt: context.DaysAgo(10),
            "Test Participant One",
            "Test Participant Two"),

        SeedGoals.Build(
            Owner,
            2,
            "Automated test: completed goal",
            null,
            progressPercent: 100,
            targetDate: context.DaysFromToday(3),
            createdAt: context.DaysAgo(20)),

        SeedGoals.Build(
            Owner,
            3,
            "Automated test: goal without target date",
            null,
            progressPercent: 0,
            targetDate: null,
            createdAt: context.DaysAgo(1)),
    ];
}
