using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Everything a seed is allowed to depend on.
/// </summary>
/// <remarks>
/// Seeds are pure functions of this record — they never read the clock, never
/// call <see cref="Guid.NewGuid"/> and never touch the network. Rebuilding a
/// database therefore always produces the same business state.
///
/// <see cref="Now"/> is the current instant truncated to midnight UTC, taken
/// from the injected <see cref="TimeProvider"/>. Dates are expressed relative
/// to it ("due in 14 days") so a goal that is meant to be upcoming stays
/// upcoming, instead of silently becoming overdue as the calendar moves on.
/// A test that needs byte-identical output injects a fixed
/// <see cref="TimeProvider"/>; both the seed and <see cref="Goal.IsOverdue"/>
/// then agree on the same "today".
/// </remarks>
public sealed record SeedContext(DateTimeOffset Now)
{
    public static SeedContext FromTimeProvider(TimeProvider timeProvider) =>
        new(new DateTimeOffset(timeProvider.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero));

    public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    public DateTimeOffset DaysAgo(int days) => Now.AddDays(-days);

    public DateOnly DaysFromToday(int days) => Today.AddDays(days);
}

/// <summary>
/// Deterministic identifiers for seeded rows.
/// </summary>
/// <remarks>
/// Each profile owns a GUID prefix, so a row's origin is obvious at a glance
/// and two profiles can never collide. E2E ids are stable across runs, which
/// is what lets Playwright navigate straight to <c>/goals/e2e00000-…-000000000001</c>.
/// </remarks>
public static class SeedIds
{
    public static Guid Goal(SeedProfile profile, int index) =>
        new($"{Prefix(profile)}-0000-4000-8000-{index:D12}");

    public static Guid Participant(SeedProfile profile, int goalIndex, int participantIndex) =>
        new($"{Prefix(profile)}-0000-4000-9000-{(goalIndex * 100) + participantIndex:D12}");

    private static string Prefix(SeedProfile profile) => profile switch
    {
        SeedProfile.Development => "d0000000",
        SeedProfile.ManualTesting => "a0000000",
        SeedProfile.AutomatedTest => "70000000",
        SeedProfile.E2E => "e2e00000",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "No id prefix for this seed profile."),
    };
}
