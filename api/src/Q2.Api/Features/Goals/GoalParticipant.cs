namespace Q2.Api.Features.Goals;

/// <summary>
/// Someone taking part in a shared goal.
/// </summary>
/// <remarks>
/// Points at a <see cref="People.Person"/> rather than repeating a name, so
/// renaming somebody updates every goal they are on, and a chat, a goal and the
/// feed all mean the same person when they say "Lena".
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
