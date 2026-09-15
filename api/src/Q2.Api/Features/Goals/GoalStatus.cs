namespace Q2.Api.Features.Goals;

/// <summary>
/// Lifecycle of a goal. Persisted as text so the database stays readable and
/// a later provider change does not depend on enum ordinals.
/// </summary>
public enum GoalStatus
{
    /// <summary>Running: its windows open, and are delivered or missed, on its schedule.</summary>
    Active,

    /// <summary>
    /// Carried through: closed by its owner, or finished by delivering the only
    /// window of a one-off. Keeps its whole record.
    /// </summary>
    Completed,

    /// <summary>Given up: stopped by its owner without carrying it through. Keeps its whole record.</summary>
    Archived,
}
