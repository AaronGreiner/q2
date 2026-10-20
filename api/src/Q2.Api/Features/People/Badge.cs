namespace Q2.Api.Features.People;

/// <summary>
/// The badges q2 can award. A closed set, because the profile screen shows
/// every badge — earned or not — and "not earned yet" needs something to draw.
/// Each is earned with windows, photographs or kudos, and given by
/// <see cref="BadgeAwarder"/> (#22).
/// </summary>
public enum BadgeKey
{
    /// <summary>Kept a streak going for a fortnight (<see cref="BadgeRules.StreakHeroDays"/>).</summary>
    StreakHero,

    /// <summary>
    /// A believed photograph delivered before seven in the morning, on five
    /// different days (<see cref="BadgeRules.EarlyBirdDays"/>).
    /// </summary>
    EarlyBird,

    /// <summary>Ten kept windows on one reading goal (<see cref="BadgeRules.BookwormWindows"/>).</summary>
    Bookworm,

    /// <summary>Gave a hundred kudos to other people (<see cref="BadgeRules.KudosGiverCount"/>).</summary>
    KudosGiver,

    /// <summary>Forty-two kept windows on one sport goal (<see cref="BadgeRules.MarathonWindows"/>).</summary>
    Marathon,

    /// <summary>Kept every window due in one week, Monday to Sunday.</summary>
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
