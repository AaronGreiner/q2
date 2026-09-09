namespace Q2.Api.Features.Goals;

/// <summary>
/// Validates the incoming request shape before it reaches the domain.
/// </summary>
/// <remarks>
/// This is not a duplicate of the domain rules — it is the layer that turns a
/// request into good field-level messages, and it reads its limits from the
/// domain constants so the numbers have exactly one source of truth.
/// <see cref="Goal.Create"/> and <see cref="GoalSchedule"/> still enforce
/// everything themselves; a request that slips past this validator produces a
/// <see cref="Infrastructure.Errors.DomainValidationException"/>, which the
/// global handler also turns into a 400.
/// </remarks>
public static class CreateGoalRequestValidator
{
    /// <summary>
    /// Returns field name → messages. An empty dictionary means "valid".
    /// </summary>
    public static Dictionary<string, string[]> Validate(CreateGoalRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            errors[nameof(request.Title)] = ["A title is required."];
        }
        else if (title.Length > Goal.MaxTitleLength)
        {
            errors[nameof(request.Title)] = [$"A title may be at most {Goal.MaxTitleLength} characters long."];
        }

        if (request.Description is { } description && description.Trim().Length > Goal.MaxDescriptionLength)
        {
            errors[nameof(request.Description)] =
                [$"A description may be at most {Goal.MaxDescriptionLength} characters long."];
        }

        if (request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) && !GoalIcons.IsValid(icon.Trim()))
        {
            errors[nameof(request.Icon)] = ["That icon is not one of the available goal icons."];
        }

        foreach (var (field, messages) in ValidateSchedule(request.Schedule))
        {
            errors[field] = messages;
        }

        if (request.ParticipantIds is { } participants)
        {
            if (participants.Count > Goal.MaxParticipants)
            {
                errors[nameof(request.ParticipantIds)] =
                    [$"A goal may have at most {Goal.MaxParticipants} participants."];
            }
            else if (participants.Any(id => id == Guid.Empty))
            {
                errors[nameof(request.ParticipantIds)] = ["Participant ids must not be empty."];
            }
        }

        return errors;
    }

    /// <summary>
    /// Everything wrong with the schedule half of the request, under the field
    /// name the client sent it as.
    /// </summary>
    private static Dictionary<string, string[]> ValidateSchedule(GoalScheduleRequest? schedule)
    {
        var errors = new Dictionary<string, string[]>();
        const string field = nameof(CreateGoalRequest.Schedule);

        // No schedule at all is allowed: it means "every day", which is what
        // most people mean by a new habit and what the create sheet opens on.
        if (schedule?.Kind is not { } kind)
        {
            return errors;
        }

        if (!Enum.IsDefined(kind))
        {
            errors[field] = ["That is not a kind of schedule."];
            return errors;
        }

        switch (kind)
        {
            case ScheduleKind.Interval when schedule.EveryDays is not { } days || days is < 1 or > GoalSchedule.MaxEveryDays:
                errors[field] = [$"An interval must be between 1 and {GoalSchedule.MaxEveryDays} days."];
                break;

            case ScheduleKind.Weekdays when schedule.Weekdays is not { Count: > 0 }:
                errors[field] = ["Choose at least one weekday."];
                break;

            case ScheduleKind.Weekdays when schedule.Weekdays.Any(day => !Enum.IsDefined(day)):
                errors[field] = ["That is not a weekday."];
                break;

            case ScheduleKind.Times when schedule.Times is not { } times || times is < 1 or > GoalSchedule.MaxTimesPerPeriod:
                errors[field] = [$"A quota must be between 1 and {GoalSchedule.MaxTimesPerPeriod} times."];
                break;

            case ScheduleKind.Times when schedule.Period is not { } period || !Enum.IsDefined(period):
                errors[field] = ["Choose a week or a month."];
                break;

            default:
                break;
        }

        return errors;
    }

    /// <summary>
    /// The domain schedule a validated request asks for.
    /// </summary>
    /// <remarks>
    /// Defaults to every day when the request says nothing, which is the
    /// commitment people mean when they do not say otherwise.
    /// </remarks>
    public static GoalSchedule ToSchedule(GoalScheduleRequest? request) => request?.Kind switch
    {
        ScheduleKind.Once => GoalSchedule.Once(),
        ScheduleKind.Interval => GoalSchedule.EveryNDays(request.EveryDays ?? 1),
        ScheduleKind.Weekdays => GoalSchedule.OnWeekdays(request.Weekdays),
        ScheduleKind.Times => GoalSchedule.TimesPer(request.Times ?? 1, request.Period ?? QuotaPeriod.Week),
        _ => GoalSchedule.EveryNDays(1),
    };
}

/// <summary>
/// Validates a request to set a goal aside.
/// </summary>
/// <remarks>
/// Same division of labour as <see cref="CreateGoalRequestValidator"/>: this
/// turns a bad request into field-level messages, while
/// <see cref="Goal.RequestPause"/> and <see cref="GoalPause.Create"/> still
/// enforce everything themselves. The allowance and "one at a time" are
/// deliberately *not* here — they are facts about the goal, not about the
/// request, and checking them twice would leave two answers to be kept in step.
/// </remarks>
public static class RequestPauseValidator
{
    public static Dictionary<string, string[]> Validate(RequestPauseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();

        if (!PauseRules.IsReasonValid(request.Reason))
        {
            errors[nameof(request.Reason)] =
                [$"A pause needs a reason of at least {PauseRules.MinReasonLength} characters."];
        }
        else if (request.Reason!.Trim().Length > PauseRules.MaxReasonLength)
        {
            errors[nameof(request.Reason)] =
                [$"A reason may be at most {PauseRules.MaxReasonLength} characters long."];
        }

        if (request.Days is not { } days)
        {
            errors[nameof(request.Days)] = ["A pause needs a length in days."];
        }
        else if (days is < PauseRules.MinDays or > PauseRules.MaxDays)
        {
            errors[nameof(request.Days)] =
                [$"A pause runs for between {PauseRules.MinDays} and {PauseRules.MaxDays} days."];
        }

        return errors;
    }
}
