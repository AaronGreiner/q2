namespace Q2.Api.Features.Goals;

/// <summary>
/// Someone taking part in a shared goal.
/// </summary>
/// <remarks>
/// Intentionally just a display name for now. When real accounts arrive this
/// grows a user reference; it is a separate entity (rather than a string list)
/// precisely so that change stays a migration instead of a rewrite.
/// Seed data uses obviously fictional names.
/// </remarks>
public sealed class GoalParticipant
{
    // EF Core materialisation only.
    private GoalParticipant()
    {
        DisplayName = string.Empty;
    }

    internal GoalParticipant(Guid id, Guid goalId, string displayName)
    {
        Id = id;
        GoalId = goalId;
        DisplayName = displayName;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    public string DisplayName { get; private set; }
}
