using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>
/// A goal someone is working towards, alone or together with others.
/// </summary>
/// <remarks>
/// The model is deliberately small. There are no users, owners or permissions
/// yet — participants are just display names. Introducing identity is a
/// separate, security-relevant step (see docs/adr/0006-authentication-deferred.md).
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
    public const int MaxParticipantNameLength = 80;

    private readonly List<GoalParticipant> _participants = [];

    // EF Core materialisation only.
    private Goal()
    {
        Title = string.Empty;
    }

    private Goal(Guid id, string title, string? description, int progressPercent, DateOnly? targetDate, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        ProgressPercent = progressPercent;
        TargetDate = targetDate;
        CreatedAt = createdAt;
        Status = progressPercent >= 100 ? GoalStatus.Completed : GoalStatus.Active;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public GoalStatus Status { get; private set; }

    /// <summary>Progress in whole percent, always between 0 and 100.</summary>
    public int ProgressPercent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Optional date the goal should be reached by.</summary>
    public DateOnly? TargetDate { get; private set; }

    public IReadOnlyList<GoalParticipant> Participants => _participants;

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
        int progressPercent,
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

        if (progressPercent is < 0 or > 100)
        {
            errors[nameof(ProgressPercent)] = ["Progress must be between 0 and 100."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new Goal(id, normalisedTitle, normalisedDescription, progressPercent, targetDate, createdAt);
    }

    /// <summary>
    /// Sets progress and keeps <see cref="Status"/> consistent: reaching 100%
    /// completes the goal, dropping below 100% reopens it. Archived goals are
    /// left alone — reopening one is an explicit action, not a side effect.
    /// </summary>
    public void UpdateProgress(int progressPercent)
    {
        if (progressPercent is < 0 or > 100)
        {
            throw new DomainValidationException(nameof(ProgressPercent), "Progress must be between 0 and 100.");
        }

        ProgressPercent = progressPercent;

        if (Status == GoalStatus.Archived)
        {
            return;
        }

        Status = progressPercent >= 100 ? GoalStatus.Completed : GoalStatus.Active;
    }

    public void Archive() => Status = GoalStatus.Archived;

    /// <summary>Adds a participant. Names are compared case-insensitively.</summary>
    public void AddParticipant(Guid id, string displayName)
    {
        var normalised = displayName?.Trim() ?? string.Empty;

        if (normalised.Length == 0)
        {
            throw new DomainValidationException(nameof(Participants), "A participant needs a display name.");
        }

        if (normalised.Length > MaxParticipantNameLength)
        {
            throw new DomainValidationException(
                nameof(Participants),
                $"A participant name may be at most {MaxParticipantNameLength} characters long.");
        }

        if (_participants.Count >= MaxParticipants)
        {
            throw new DomainValidationException(
                nameof(Participants),
                $"A goal may have at most {MaxParticipants} participants.");
        }

        if (_participants.Any(p => string.Equals(p.DisplayName, normalised, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _participants.Add(new GoalParticipant(id, Id, normalised));
    }

    /// <summary>
    /// True when the target date has passed and the goal is still open.
    /// Takes "today" as an argument so callers control the time zone and tests
    /// stay independent of the clock.
    /// </summary>
    public bool IsOverdue(DateOnly today) =>
        TargetDate is { } target && Status == GoalStatus.Active && target < today;
}
