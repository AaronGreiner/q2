namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Everything a seed is allowed to depend on.
/// </summary>
/// <remarks>
/// Seeds are pure functions of this record — they never read the clock, never
/// call <see cref="Guid.NewGuid"/> and never touch the network. Rebuilding a
/// database therefore always produces the same business state.
///
/// There are two instants here on purpose. <see cref="Today"/> anchors
/// everything with a date — a goal due in 14 days stays upcoming instead of
/// silently going overdue as the calendar moves on. <see cref="SeededAt"/> is
/// the real moment the seed ran, and is what "sent 12 minutes ago" and "online"
/// are measured from; a chat whose newest message was timestamped at midnight
/// would look abandoned by lunchtime.
///
/// Both come from the injected <see cref="TimeProvider"/>, so a test that needs
/// byte-identical output injects a fixed one and both agree.
/// </remarks>
public sealed record SeedContext(DateTimeOffset SeededAt)
{
    public static SeedContext FromTimeProvider(TimeProvider timeProvider) =>
        new(timeProvider.GetUtcNow());

    /// <summary>Midnight UTC of the day the seed ran.</summary>
    public DateTimeOffset Midnight => new(SeededAt.UtcDateTime.Date, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(SeededAt.UtcDateTime);

    public DateTimeOffset DaysAgo(int days) => Midnight.AddDays(-days);

    public DateOnly DaysFromToday(int days) => Today.AddDays(days);

    public DateTimeOffset MinutesAgo(int minutes) => SeededAt.AddMinutes(-minutes);

    public DateTimeOffset HoursAgo(double hours) => SeededAt.AddHours(-hours);
}

/// <summary>The kinds of row a seed creates. Only used to keep their ids apart.</summary>
public enum SeedEntity
{
    // Zero keeps goal ids at the shape they have always had
    // (e2e00000-0000-4000-8000-000000000001), which is what E2E navigates to.
    Goal = 0,
    Person = 1,
    GoalTask = 2,
    GoalParticipant = 3,
    GoalContribution = 4,
    CheckIn = 5,
    Badge = 6,
    Friendship = 7,
    Activity = 8,
    Kudos = 9,
    Conversation = 10,
    ConversationParticipant = 11,
    Message = 12,
    Reaction = 13,
    Settings = 14,
    Account = 15,
}

/// <summary>
/// Deterministic identifiers for seeded rows.
/// </summary>
/// <remarks>
/// Each profile owns a GUID prefix and each kind of row owns the group after
/// it, so a row's origin is obvious at a glance and two profiles can never
/// collide. E2E ids are stable across runs, which is what lets Playwright
/// navigate straight to <c>/goals/e2e00000-0000-4000-8000-000000000001</c>.
/// </remarks>
public static class SeedIds
{
    public static Guid For(SeedProfile profile, SeedEntity entity, int index) =>
        new($"{Prefix(profile)}-{(int)entity:D4}-4000-8000-{index:D12}");

    /// <summary>The first goal of a profile — the one tests navigate to.</summary>
    public static Guid Goal(SeedProfile profile, int index) => For(profile, SeedEntity.Goal, index);

    private static string Prefix(SeedProfile profile) => profile switch
    {
        SeedProfile.Development => "d0000000",
        SeedProfile.ManualTesting => "a0000000",
        SeedProfile.AutomatedTest => "70000000",
        SeedProfile.E2E => "e2e00000",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "No id prefix for this seed profile."),
    };
}
