namespace Q2.Api.Features.Goals;

/// <summary>A Do that waits for its owner's first friend.</summary>
/// <param name="Schedule">How often it will be due once it runs.</param>
/// <param name="ReminderAt">The daily reminder it starts with, or null.</param>
public sealed record GoalDraftResponse(
    Guid Id,
    string Title,
    string Icon,
    GoalScheduleResponse Schedule,
    TimeOnly? ReminderAt,
    DateTimeOffset CreatedAt)
{
    public static GoalDraftResponse From(GoalDraft draft) => new(
        draft.Id,
        draft.Title,
        draft.Icon,
        GoalScheduleResponse.From(draft.Schedule),
        draft.ReminderAt,
        draft.CreatedAt);
}

/// <summary>Whether somebody has a Do waiting, and whether they could.</summary>
/// <param name="Draft">Null when there is none — the ordinary answer.</param>
/// <param name="HasFriends">
/// Whether there is anybody to make a goal in front of. False is the state a
/// waiting Do exists for: the start screen then points at the invite link, and
/// the create sheet offers to leave a Do waiting instead of a form that could
/// only be refused.
/// </param>
/// <remarks>
/// A wrapper rather than a 404 for "none": having no waiting Do is the normal
/// state of almost every account, and a client should not have to tell an
/// expected absence from a missing route.
/// </remarks>
public sealed record GoalDraftStateResponse(GoalDraftResponse? Draft, bool HasFriends);

/// <summary>Request body for writing the waiting Do.</summary>
/// <remarks>
/// A Do only, without friends — which is the point of it. Nullable like every
/// request here, so a missing title produces our own field-level message.
/// </remarks>
public sealed record SaveGoalDraftRequest(
    string? Title = null,
    string? Icon = null,
    GoalScheduleRequest? Schedule = null,
    TimeOnly? ReminderAt = null);
