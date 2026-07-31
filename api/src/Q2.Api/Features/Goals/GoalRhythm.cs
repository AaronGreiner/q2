namespace Q2.Api.Features.Goals;

/// <summary>
/// How often something recurs.
/// </summary>
/// <remarks>
/// Shared by <see cref="Goal"/> and <see cref="GoalTask"/> because they answer
/// the same question, and a goal whose rhythm disagreed with its tasks' would
/// be lying to the person looking at it. Persisted as text so the database
/// stays readable and reordering the members cannot change what a row means.
/// </remarks>
public enum GoalRhythm
{
    /// <summary>Every day.</summary>
    Daily,

    /// <summary>Monday to Friday.</summary>
    Weekdays,

    /// <summary>Once a week, on <see cref="GoalTask.WeeklyOn"/>.</summary>
    Weekly,

    /// <summary>Once, on a specific day.</summary>
    Once,
}
