using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Something to do on a given day — the unit a person actually ticks off.
/// </summary>
/// <remarks>
/// A task may belong to a goal ("Joggen 5 km" under "Halbmarathon im Mai") or
/// stand on its own, which is why <see cref="GoalId"/> is nullable: people add
/// a habit long before they decide what larger goal it serves.
///
/// "Done" is a date, not a flag. A daily task is done *today* and open again
/// tomorrow, and a boolean would need a nightly job to reset it — one that
/// would have to know every user's time zone and would silently rewrite history
/// if it ran twice. Storing the day it was last completed makes
/// <see cref="IsDoneOn"/> a pure function and needs no job at all.
/// </remarks>
public sealed class GoalTask
{
    public const int MaxTitleLength = 120;
    public const int MaxUnitLength = 16;

    // EF Core materialisation only.
    private GoalTask()
    {
        Title = string.Empty;
    }

    private GoalTask(
        Guid id,
        Guid ownerPersonId,
        Guid? goalId,
        string title,
        GoalRhythm rhythm,
        TimeOnly? reminderAt,
        DayOfWeek? weeklyOn,
        DateOnly? dueOn,
        double? measuredValue,
        double? targetValue,
        string? measureUnit,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerPersonId = ownerPersonId;
        GoalId = goalId;
        Title = title;
        Rhythm = rhythm;
        ReminderAt = reminderAt;
        WeeklyOn = weeklyOn;
        DueOn = dueOn;
        MeasuredValue = measuredValue;
        TargetValue = targetValue;
        MeasureUnit = measureUnit;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Whose task this is.
    /// </summary>
    /// <remarks>
    /// A task is personal even under a shared goal: two people running the same
    /// half-marathon each tick off their own runs, and one of them ticking a
    /// box must not put a mark on the other's day. Ticking one off writes a
    /// check-in for its owner, which is exactly why it needs one.
    /// </remarks>
    public Guid OwnerPersonId { get; private set; }

    /// <summary>The goal this contributes to, when there is one.</summary>
    public Guid? GoalId { get; private set; }

    public string Title { get; private set; }

    public GoalRhythm Rhythm { get; private set; }

    /// <summary>Local time this is meant to happen, when it has one.</summary>
    public TimeOnly? ReminderAt { get; private set; }

    /// <summary>Which day a <see cref="GoalRhythm.Weekly"/> task falls on.</summary>
    public DayOfWeek? WeeklyOn { get; private set; }

    /// <summary>The single day a <see cref="GoalRhythm.Once"/> task is due.</summary>
    public DateOnly? DueOn { get; private set; }

    /// <summary>The last day this was completed. Null means never.</summary>
    public DateOnly? CompletedOn { get; private set; }

    /// <summary>
    /// How much of a measurable task is done — 1.2 of 2 litres.
    /// </summary>
    /// <remarks>
    /// <c>double</c> rather than <c>decimal</c>: these are physical quantities
    /// a person reads off a bottle, not money, and SQLite has no decimal type —
    /// EF Core maps it to text and then cannot compare or order by it.
    /// </remarks>
    public double? MeasuredValue { get; private set; }

    /// <summary>The amount that counts as done, when the task is measurable.</summary>
    public double? TargetValue { get; private set; }

    /// <summary>Unit shown next to the measure, e.g. "L" or "km".</summary>
    public string? MeasureUnit { get; private set; }

    /// <summary>Position within the day's list. Lower comes first.</summary>
    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>True when this task has a measurable amount rather than a plain tick.</summary>
    public bool IsMeasurable => TargetValue is > 0;

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static GoalTask Create(
        Guid id,
        Guid ownerPersonId,
        Guid? goalId,
        string title,
        GoalRhythm rhythm,
        TimeOnly? reminderAt,
        DayOfWeek? weeklyOn,
        DateOnly? dueOn,
        double? measuredValue,
        double? targetValue,
        string? measureUnit,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        var errors = new Dictionary<string, string[]>();

        if (ownerPersonId == Guid.Empty)
        {
            errors[nameof(OwnerPersonId)] = ["A task needs somebody it belongs to."];
        }

        var normalisedTitle = title?.Trim() ?? string.Empty;
        if (normalisedTitle.Length == 0)
        {
            errors[nameof(Title)] = ["A title is required."];
        }
        else if (normalisedTitle.Length > MaxTitleLength)
        {
            errors[nameof(Title)] = [$"A title may be at most {MaxTitleLength} characters long."];
        }

        var normalisedUnit = string.IsNullOrWhiteSpace(measureUnit) ? null : measureUnit.Trim();
        if (normalisedUnit is { Length: > MaxUnitLength })
        {
            errors[nameof(MeasureUnit)] = [$"A unit may be at most {MaxUnitLength} characters long."];
        }

        if (targetValue is <= 0)
        {
            errors[nameof(TargetValue)] = ["A target amount must be greater than zero."];
        }

        if (measuredValue is < 0)
        {
            errors[nameof(MeasuredValue)] = ["A measured amount cannot be negative."];
        }

        if (measuredValue is not null && targetValue is null)
        {
            errors[nameof(TargetValue)] = ["A measured amount needs a target to be measured against."];
        }

        if (rhythm == GoalRhythm.Weekly && weeklyOn is null)
        {
            errors[nameof(WeeklyOn)] = ["A weekly task needs the day of the week it falls on."];
        }

        if (rhythm == GoalRhythm.Once && dueOn is null)
        {
            errors[nameof(DueOn)] = ["A one-off task needs the day it is due."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new GoalTask(
            id,
            ownerPersonId,
            goalId,
            normalisedTitle,
            rhythm,
            reminderAt,
            rhythm == GoalRhythm.Weekly ? weeklyOn : null,
            rhythm == GoalRhythm.Once ? dueOn : null,
            measuredValue,
            targetValue,
            normalisedUnit,
            sortOrder,
            createdAt);
    }

    /// <summary>Whether this task belongs on <paramref name="day"/>'s list.</summary>
    public bool IsScheduledFor(DateOnly day) => Rhythm switch
    {
        GoalRhythm.Daily => true,
        GoalRhythm.Weekdays => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        GoalRhythm.Weekly => day.DayOfWeek == WeeklyOn,
        GoalRhythm.Once => DueOn == day,
        _ => false,
    };

    public bool IsDoneOn(DateOnly day) => CompletedOn == day;

    /// <summary>
    /// Ticks the task off for <paramref name="day"/>, or un-ticks it if it was
    /// already done. Returns the new state.
    /// </summary>
    /// <param name="day">The day being ticked off, supplied by the caller.</param>
    public bool Toggle(DateOnly day)
    {
        CompletedOn = IsDoneOn(day) ? null : day;

        // A measurable task that is ticked off is, by definition, at its
        // target — otherwise the bar would still read 60% under a completed
        // row. Un-ticking leaves the amount alone: what was drunk was drunk.
        if (CompletedOn is not null && IsMeasurable)
        {
            MeasuredValue = TargetValue;
        }

        return CompletedOn is not null;
    }
}
