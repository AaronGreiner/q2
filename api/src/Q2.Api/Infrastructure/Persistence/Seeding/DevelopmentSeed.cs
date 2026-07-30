using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// A handful of goals so a fresh checkout shows something useful.
/// </summary>
/// <remarks>
/// Only inserted into an empty Development database, and never re-inserted on
/// later starts — your local data is yours.
/// </remarks>
public sealed class DevelopmentSeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.Development;

    public SeedProfile Profile => Owner;

    public string Description => "3 goals covering the active, completed and no-progress states.";

    public IReadOnlyList<Goal> CreateGoals(SeedContext context) =>
    [
        SeedGoals.Build(
            Owner,
            1,
            "Walk 8.000 steps a day",
            "A short walk after lunch is usually enough to get there.",
            progressPercent: 45,
            targetDate: context.DaysFromToday(30),
            createdAt: context.DaysAgo(12),
            "Robin Sample",
            "Kim Example"),

        SeedGoals.Build(
            Owner,
            2,
            "Read one book this month",
            null,
            progressPercent: 100,
            targetDate: context.DaysFromToday(6),
            createdAt: context.DaysAgo(26)),

        SeedGoals.Build(
            Owner,
            3,
            "Start a weekly stretching routine",
            "Not started yet — the point is to see the zero-progress state.",
            progressPercent: 0,
            targetDate: null,
            createdAt: context.DaysAgo(2)),
    ];
}
