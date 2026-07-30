namespace Q2.Api.Features.Goals;

/// <summary>
/// Lifecycle of a goal. Persisted as text so the database stays readable and
/// a later provider change does not depend on enum ordinals.
/// </summary>
public enum GoalStatus
{
    /// <summary>Being worked on. Progress is below 100%.</summary>
    Active,

    /// <summary>Reached 100% progress.</summary>
    Completed,

    /// <summary>Put aside without completing it. Stays visible in history.</summary>
    Archived,
}
