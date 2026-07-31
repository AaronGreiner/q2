namespace Q2.Api.Features.Goals;

/// <summary>
/// A task as the app shows it, already resolved against a particular day.
/// </summary>
/// <param name="IsDone">
/// Whether it is done <em>on the day this was asked for</em>. A daily task is
/// done today and open again tomorrow, and the client is not asked to work that
/// out from a stored date and its own clock.
/// </param>
/// <param name="MeasurePercent">
/// How full the little bar is, derived from the amounts. Null when the task is
/// a plain tick rather than a measurable one.
/// </param>
public sealed record GoalTaskResponse(
    Guid Id,
    Guid? GoalId,
    string Title,
    GoalRhythm Rhythm,
    TimeOnly? ReminderAt,
    bool IsDone,
    double? MeasuredValue,
    double? TargetValue,
    string? MeasureUnit,
    int? MeasurePercent)
{
    public static GoalTaskResponse From(GoalTask task, DateOnly day)
    {
        int? measurePercent = task is { IsMeasurable: true, TargetValue: { } target }
            ? (int)Math.Round(Math.Clamp((task.MeasuredValue ?? 0) / target, 0, 1) * 100)
            : null;

        return new GoalTaskResponse(
            task.Id,
            task.GoalId,
            task.Title,
            task.Rhythm,
            task.ReminderAt,
            task.IsDoneOn(day),
            task.MeasuredValue,
            task.TargetValue,
            task.MeasureUnit,
            measurePercent);
    }
}

/// <summary>How much of today is done. What the ring on the home screen shows.</summary>
public sealed record DaySummaryResponse(int Done, int Total, int Percent)
{
    public static DaySummaryResponse From(int done, int total) =>
        new(done, total, total == 0 ? 0 : (int)Math.Round(done * 100d / total));
}

/// <summary>
/// Request body for creating a task.
/// </summary>
/// <remarks>
/// Every property is nullable on purpose: a missing title should produce our
/// own field-level validation message, not a model-binding failure.
/// </remarks>
public sealed record CreateTaskRequest(
    string? Title = null,
    GoalRhythm? Rhythm = null,
    Guid? GoalId = null,
    TimeOnly? ReminderAt = null,
    DayOfWeek? WeeklyOn = null,
    DateOnly? DueOn = null,
    double? TargetValue = null,
    string? MeasureUnit = null);
