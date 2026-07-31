using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>
/// A goal someone is working towards, alone or together with others.
/// </summary>
/// <remarks>
/// Progress is counted in <em>steps</em>, not in percent. A goal is "14 of 21
/// runs", and the percentage is derived from that. Storing the percentage
/// instead would let the ring and the caption under it disagree, and it would
/// leave "make some progress" without a defined meaning — which is exactly what
/// <see cref="Contribute"/> needs.
///
/// All invariants live here rather than in the service, and neither the current
/// time nor new ids are read from ambient state: both are parameters. That is
/// what makes seeds and tests reproducible.
/// </remarks>
public sealed class Goal
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 1000;
    public const int MaxParticipants = 20;
    public const int MaxSteps = 10_000;

    private readonly List<GoalParticipant> _participants = [];
    private readonly List<GoalContribution> _contributions = [];

    // EF Core materialisation only.
    private Goal()
    {
        Title = string.Empty;
        Icon = GoalIcons.Default;
    }

    private Goal(
        Guid id,
        string title,
        string? description,
        string icon,
        GoalRhythm rhythm,
        bool isGroup,
        int completedSteps,
        int totalSteps,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
        Rhythm = rhythm;
        IsGroup = isGroup;
        CompletedSteps = completedSteps;
        TotalSteps = totalSteps;
        ReminderAt = reminderAt;
        TargetDate = targetDate;
        CreatedAt = createdAt;
        Status = completedSteps >= totalSteps ? GoalStatus.Completed : GoalStatus.Active;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    /// <summary>One of <see cref="GoalIcons"/>, never a free-form icon name.</summary>
    public string Icon { get; private set; }

    /// <summary>How often the goal is worked on.</summary>
    public GoalRhythm Rhythm { get; private set; }

    /// <summary>True for a goal a whole group shares, rather than a pair of friends.</summary>
    public bool IsGroup { get; private set; }

    public GoalStatus Status { get; private set; }

    /// <summary>Steps done so far, never above <see cref="TotalSteps"/>.</summary>
    public int CompletedSteps { get; private set; }

    /// <summary>How many steps the goal takes in total. At least one.</summary>
    public int TotalSteps { get; private set; }

    /// <summary>Progress in whole percent, derived from the steps.</summary>
    public int ProgressPercent => TotalSteps == 0 ? 0 : (int)Math.Round(CompletedSteps * 100d / TotalSteps);

    /// <summary>Local time of the daily reminder, when there is one.</summary>
    public TimeOnly? ReminderAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Optional date the goal should be reached by.</summary>
    public DateOnly? TargetDate { get; private set; }

    public IReadOnlyList<GoalParticipant> Participants => _participants;

    /// <summary>The days this goal was worked on. What the streak is counted from.</summary>
    public IReadOnlyList<GoalContribution> Contributions => _contributions;

    /// <summary>
    /// Creates a goal, validating every invariant. <paramref name="id"/> and
    /// <paramref name="createdAt"/> are supplied by the caller so the result is
    /// fully determined by its arguments.
    /// </summary>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Goal Create(
        Guid id,
        string title,
        string? description,
        string? icon,
        GoalRhythm rhythm,
        bool isGroup,
        int completedSteps,
        int totalSteps,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt)
    {
        var errors = new Dictionary<string, string[]>();

        var normalisedTitle = title?.Trim() ?? string.Empty;
        if (normalisedTitle.Length == 0)
        {
            errors[nameof(Title)] = ["A title is required."];
        }
        else if (normalisedTitle.Length > MaxTitleLength)
        {
            errors[nameof(Title)] = [$"A title may be at most {MaxTitleLength} characters long."];
        }

        var normalisedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalisedDescription is { Length: > MaxDescriptionLength })
        {
            errors[nameof(Description)] = [$"A description may be at most {MaxDescriptionLength} characters long."];
        }

        var normalisedIcon = string.IsNullOrWhiteSpace(icon) ? GoalIcons.Default : icon.Trim();
        if (!GoalIcons.IsValid(normalisedIcon))
        {
            errors[nameof(Icon)] = ["That icon is not one of the available goal icons."];
        }

        if (totalSteps is < 1 or > MaxSteps)
        {
            errors[nameof(TotalSteps)] = [$"A goal must have between 1 and {MaxSteps} steps."];
        }
        else if (completedSteps < 0 || completedSteps > totalSteps)
        {
            errors[nameof(CompletedSteps)] = ["Completed steps must be between zero and the total."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new Goal(
            id,
            normalisedTitle,
            normalisedDescription,
            normalisedIcon,
            rhythm,
            isGroup,
            completedSteps,
            totalSteps,
            reminderAt,
            targetDate,
            createdAt);
    }

    /// <summary>
    /// Records one step of progress on <paramref name="day"/> and keeps
    /// <see cref="Status"/> consistent. Returns <c>false</c> when there was
    /// nothing left to do, so the caller can tell "already finished" from "you
    /// just got further".
    /// </summary>
    /// <remarks>
    /// Archived goals are left alone — reopening one is an explicit action, not
    /// a side effect of tapping a button on a card.
    ///
    /// A second step on the same day adds a step but not a second day: the
    /// streak counts days worked on, not taps.
    /// </remarks>
    public bool Contribute(Guid contributionId, DateOnly day)
    {
        if (Status == GoalStatus.Archived || CompletedSteps >= TotalSteps)
        {
            return false;
        }

        CompletedSteps++;
        Status = CompletedSteps >= TotalSteps ? GoalStatus.Completed : GoalStatus.Active;
        RecordContribution(contributionId, day);
        return true;
    }

    /// <summary>
    /// Notes that the goal was worked on that day, without moving the count.
    /// Used by the seeds to lay down the history a streak is read from.
    /// </summary>
    public void RecordContribution(Guid id, DateOnly day)
    {
        if (_contributions.Any(c => c.Date == day))
        {
            return;
        }

        _contributions.Add(new GoalContribution(id, Id, day));
    }

    /// <summary>Consecutive days this goal has been worked on.</summary>
    public int StreakOn(DateOnly today) => Streak.Count(_contributions.Select(c => c.Date), today);

    public void Archive() => Status = GoalStatus.Archived;

    /// <summary>Adds a participant. Adding the same person twice does nothing.</summary>
    public void AddParticipant(Guid id, Guid personId)
    {
        if (_participants.Any(p => p.PersonId == personId))
        {
            return;
        }

        if (_participants.Count >= MaxParticipants)
        {
            throw new DomainValidationException(
                nameof(Participants),
                $"A goal may have at most {MaxParticipants} participants.");
        }

        _participants.Add(new GoalParticipant(id, Id, personId));
    }

    /// <summary>
    /// True when the target date has passed and the goal is still open.
    /// Takes "today" as an argument so callers control the time zone and tests
    /// stay independent of the clock.
    /// </summary>
    public bool IsOverdue(DateOnly today) =>
        TargetDate is { } target && Status == GoalStatus.Active && target < today;
}

/// <summary>
/// The icons a goal may use.
/// </summary>
/// <remarks>
/// A closed set on purpose. The frontend bundles its icons at build time rather
/// than fetching them (see app/nuxt.config.ts), so an icon name the server
/// invented at runtime would simply not render. Adding one here means adding it
/// to that bundle in the same change.
/// </remarks>
public static class GoalIcons
{
    public const string Default = "target";

    public static readonly IReadOnlyList<string> All =
    [
        "target",
        "medal",
        "book-open",
        "sunrise",
        "droplet",
        "flame",
        "trophy",
        "sparkles",
        "calendar",
        "alarm-clock",
        "hand-heart",
        "users",
    ];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
