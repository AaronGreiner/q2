using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>
/// A Do somebody has decided on before there is anybody to make it in front of.
/// </summary>
/// <remarks>
/// A goal is made in front of somebody: it needs a friend to check it, and it
/// comes with the conversation they check it in
/// (docs/adr/0027-goal-conversations.md). A new account has no friend, and was
/// told so the moment it tried to make its first Do (#93). This is what it
/// can make instead: the Do, fully described, waiting.
///
/// **Not a goal in another status.** It has no windows, no conversation and no
/// participants, and every query that reads goals would otherwise have to learn
/// to step around one that has none of those things. It becomes a goal through
/// <see cref="Start"/> the moment its owner's first friendship exists
/// (<see cref="GoalDraftStarter"/>), and is gone afterwards.
///
/// One per person: it is the first Do, not a planning list. A Do only, and one
/// that repeats — a one-off has a day, and the day could pass before anybody
/// arrives (docs/adr/0039-first-run-onboarding.md).
/// </remarks>
public sealed class GoalDraft
{
    // EF Core materialisation only.
    private GoalDraft()
    {
        Title = string.Empty;
        Schedule = GoalSchedule.EveryNDays(1);
    }

    private GoalDraft(Guid id, Guid ownerPersonId, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerPersonId = ownerPersonId;
        CreatedAt = createdAt;
        Title = string.Empty;
        Schedule = GoalSchedule.EveryNDays(1);
    }

    public Guid Id { get; private set; }

    /// <summary>Whose Do this will be.</summary>
    public Guid OwnerPersonId { get; private set; }

    public string Title { get; private set; }

    /// <summary>What kind of thing it is about (#18).</summary>
    public GoalCategory Category { get; private set; } = GoalCategories.Default;

    /// <summary>Its category's icon. Not stored.</summary>
    public string Icon => GoalCategories.IconFor(Category);

    /// <summary>How often it will be due. Always one that repeats.</summary>
    public GoalSchedule Schedule { get; private set; }

    /// <summary>The daily reminder it will start with, if any.</summary>
    public TimeOnly? ReminderAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Describes a waiting Do, validating it the way a goal is validated.</summary>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static GoalDraft Create(
        Guid id,
        Guid ownerPersonId,
        string title,
        GoalCategory? category,
        GoalSchedule schedule,
        TimeOnly? reminderAt,
        DateTimeOffset createdAt)
    {
        if (ownerPersonId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(OwnerPersonId), "A draft needs somebody it belongs to.");
        }

        var draft = new GoalDraft(id, ownerPersonId, createdAt);
        draft.Describe(title, category, schedule, reminderAt);
        return draft;
    }

    /// <summary>Replaces what the waiting Do says.</summary>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public void Describe(string title, GoalCategory? category, GoalSchedule schedule, TimeOnly? reminderAt)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var errors = new Dictionary<string, string[]>();

        var normalisedTitle = title?.Trim() ?? string.Empty;
        if (normalisedTitle.Length == 0)
        {
            errors[nameof(Title)] = ["A title is required."];
        }
        else if (normalisedTitle.Length > Goal.MaxTitleLength)
        {
            errors[nameof(Title)] = [$"A title may be at most {Goal.MaxTitleLength} characters long."];
        }

        var normalisedCategory = category ?? GoalCategories.Default;
        if (!Enum.IsDefined(normalisedCategory))
        {
            errors[nameof(Category)] = ["That is not one of the goal categories."];
        }

        // A one-off has a day, and nobody knows on which day the first friend
        // arrives. A Don't's limit is a separate decision for later.
        if (!schedule.Repeats || schedule.Kind == ScheduleKind.Limit)
        {
            errors[nameof(Schedule)] = ["A waiting Do has to repeat."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Title = normalisedTitle;
        Category = normalisedCategory;
        Schedule = schedule;
        ReminderAt = reminderAt;
    }

    /// <summary>
    /// The goal this becomes, with nobody on it yet: the caller adds the friend
    /// who made it possible and opens its first window.
    /// </summary>
    /// <param name="startedAt">
    /// The goal's creation time. Not the draft's: the Do starts when somebody
    /// is there to see it, and its first window and its conversation begin then.
    /// </param>
    public Goal Start(Guid goalId, DateTimeOffset startedAt) => Goal.Create(
        goalId,
        OwnerPersonId,
        Title,
        description: null,
        Category,
        Schedule.Copy(),
        isGroup: false,
        ReminderAt,
        targetDate: null,
        startedAt);
}
