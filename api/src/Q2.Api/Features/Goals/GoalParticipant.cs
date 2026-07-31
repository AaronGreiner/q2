namespace Q2.Api.Features.Goals;

/// <summary>
/// Someone taking part in a shared goal.
/// </summary>
/// <remarks>
/// Points at a <see cref="People.Person"/> rather than repeating a name, so
/// renaming somebody updates every goal they are on, and a chat, a goal and the
/// leaderboard all mean the same person when they say "Lena".
/// </remarks>
public sealed class GoalParticipant
{
    // EF Core materialisation only.
    private GoalParticipant()
    {
    }

    internal GoalParticipant(Guid id, Guid goalId, Guid personId)
    {
        Id = id;
        GoalId = goalId;
        PersonId = personId;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    public Guid PersonId { get; private set; }
}

/// <summary>One day on which a goal was worked on.</summary>
/// <remarks>
/// The history a goal's streak is read from. Separate from
/// <see cref="People.DailyCheckIn"/> on purpose: "you did something today" and
/// "you ran today" are different claims, and the card that says "12 days" about
/// the marathon goal must not borrow the number from a day spent reading.
/// </remarks>
public sealed class GoalContribution
{
    // EF Core materialisation only.
    private GoalContribution()
    {
    }

    internal GoalContribution(Guid id, Guid goalId, DateOnly date)
    {
        Id = id;
        GoalId = goalId;
        Date = date;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    public DateOnly Date { get; private set; }
}
