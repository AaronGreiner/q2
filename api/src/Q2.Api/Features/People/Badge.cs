namespace Q2.Api.Features.People;

/// <summary>
/// The badges q2 can award. A closed set, because the profile screen shows
/// every badge — earned or not — and "not earned yet" needs something to draw.
/// </summary>
public enum BadgeKey
{
    /// <summary>Kept a streak going for a fortnight.</summary>
    StreakHero,

    /// <summary>Checked in before seven in the morning, repeatedly.</summary>
    EarlyBird,

    /// <summary>Finished a reading goal.</summary>
    Bookworm,

    /// <summary>Gave a hundred kudos to other people.</summary>
    KudosGiver,

    /// <summary>Completed a long-distance running goal.</summary>
    Marathon,

    /// <summary>Topped the weekly leaderboard.</summary>
    WeeklyWinner,
}

/// <summary>A badge somebody has earned, and when.</summary>
public sealed class PersonBadge
{
    // EF Core materialisation only.
    private PersonBadge()
    {
    }

    internal PersonBadge(Guid id, Guid personId, BadgeKey badge, DateOnly earnedOn)
    {
        Id = id;
        PersonId = personId;
        Badge = badge;
        EarnedOn = earnedOn;
    }

    public Guid Id { get; private set; }

    public Guid PersonId { get; private set; }

    public BadgeKey Badge { get; private set; }

    public DateOnly EarnedOn { get; private set; }
}

/// <summary>
/// The order badges are shown in.
/// </summary>
/// <remarks>
/// Server-side so every client agrees, and so a new badge appears in the right
/// place without a frontend release. The label and the icon are presentation
/// and stay in the frontend.
/// </remarks>
public static class BadgeCatalogue
{
    public static readonly IReadOnlyList<BadgeKey> InDisplayOrder =
    [
        BadgeKey.StreakHero,
        BadgeKey.EarlyBird,
        BadgeKey.Bookworm,
        BadgeKey.KudosGiver,
        BadgeKey.Marathon,
        BadgeKey.WeeklyWinner,
    ];
}
