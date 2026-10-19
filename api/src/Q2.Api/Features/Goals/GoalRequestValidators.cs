using Q2.Api.Infrastructure.Errors;

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
///
/// Every message comes with a <see cref="FieldErrorCode"/>, which is what the
/// create sheet words in the reader's language (#80).
/// </remarks>
public static class CreateGoalRequestValidator
{
    /// <summary>
    /// Returns what is wrong, per field. Empty means "valid".
    /// </summary>
    public static FieldErrors Validate(CreateGoalRequest request)
    {
        var errors = new FieldErrors();

        AddTitleErrors(request.Title, nameof(request.Title), errors);

        if (request.Description is { } description && description.Trim().Length > Goal.MaxDescriptionLength)
        {
            errors.Add(
                nameof(request.Description),
                new(FieldErrorCodes.TooLong, Max: Goal.MaxDescriptionLength),
                $"A description may be at most {Goal.MaxDescriptionLength} characters long.");
        }

        AddIconErrors(request.Icon, nameof(request.Icon), errors);

        var kind = request.Kind ?? GoalKind.Do;

        if (!Enum.IsDefined(kind))
        {
            errors.Add(nameof(request.Kind), new(FieldErrorCodes.UnknownOption), "That is not a kind of goal.");
        }
        else if (kind == GoalKind.Dont)
        {
            errors.Add(ValidateDont(request));
        }
        else
        {
            errors.Add(ValidateSchedule(request.Schedule));
        }

        // At least one friend: a goal is checked by the people it is made in
        // front of, and one nobody is on would confirm its own photographs
        // (docs/adr/0027-goal-conversations.md).
        if (request.ParticipantIds is not { Count: > 0 } participants)
        {
            errors.Add(
                nameof(request.ParticipantIds),
                new(FieldErrorCodes.NoChecker),
                "Invite at least one friend to check this goal.");
        }
        else
        {
            if (participants.Count > Goal.MaxParticipants)
            {
                errors.Add(
                    nameof(request.ParticipantIds),
                    new(FieldErrorCodes.TooMany, Max: Goal.MaxParticipants),
                    $"A goal may have at most {Goal.MaxParticipants} participants.");
            }
            else if (participants.Any(id => id == Guid.Empty))
            {
                errors.Add(nameof(request.ParticipantIds), new(FieldErrorCodes.Invalid), "Participant ids must not be empty.");
            }
        }

        return errors;
    }

    /// <summary>A title is required and bounded — for a goal and for a waiting Do alike.</summary>
    internal static void AddTitleErrors(string? value, string field, FieldErrors errors)
    {
        var title = value?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            errors.Add(field, new(FieldErrorCodes.Required), "A title is required.");
        }
        else if (title.Length > Goal.MaxTitleLength)
        {
            errors.Add(
                field,
                new(FieldErrorCodes.TooLong, Max: Goal.MaxTitleLength),
                $"A title may be at most {Goal.MaxTitleLength} characters long.");
        }
    }

    /// <summary>No icon is fine — the default is used — but one that is given has to exist.</summary>
    internal static void AddIconErrors(string? icon, string field, FieldErrors errors)
    {
        if (icon is not null && !string.IsNullOrWhiteSpace(icon) && !GoalIcons.IsValid(icon.Trim()))
        {
            errors.Add(field, new(FieldErrorCodes.UnknownOption), "That icon is not one of the available goal icons.");
        }
    }

    /// <summary>
    /// A Don't: one limit per day, week or month, and a way for friends to
    /// check it. No schedule at all means per day.
    /// </summary>
    private static FieldErrors ValidateDont(CreateGoalRequest request)
    {
        var errors = new FieldErrors();

        if (request.Schedule?.Kind is { } kind && kind != ScheduleKind.Limit)
        {
            errors.Add(nameof(request.Schedule), new(FieldErrorCodes.UnknownOption), "A Don't is counted per day, week or month.");
        }
        else if (request.Schedule?.Period is { } period && !Enum.IsDefined(period))
        {
            errors.Add(nameof(request.Schedule), new(FieldErrorCodes.UnknownOption), "Choose a day, a week or a month.");
        }

        if (request.Limit is not { } limit || limit is < 0 or > Goal.MaxLimit)
        {
            errors.Add(
                nameof(request.Limit),
                new(FieldErrorCodes.OutOfRange, Min: 0, Max: Goal.MaxLimit),
                $"A limit must be between 0 and {Goal.MaxLimit}.");
        }

        if (request.ProofMode is not { } mode || !Enum.IsDefined(mode))
        {
            errors.Add(nameof(request.ProofMode), new(FieldErrorCodes.UnknownOption), "Choose how your friends can check this.");
        }

        return errors;
    }

    /// <summary>
    /// Everything wrong with the schedule half of the request, under the field
    /// name the client sent it as.
    /// </summary>
    internal static FieldErrors ValidateSchedule(GoalScheduleRequest? schedule)
    {
        var errors = new FieldErrors();
        const string field = nameof(CreateGoalRequest.Schedule);

        // No schedule at all is allowed: it means "every day", which is what
        // most people mean by a new habit and what the create sheet opens on.
        if (schedule?.Kind is not { } kind)
        {
            return errors;
        }

        if (!Enum.IsDefined(kind))
        {
            errors.Add(field, new(FieldErrorCodes.UnknownOption), "That is not a kind of schedule.");
            return errors;
        }

        switch (kind)
        {
            case ScheduleKind.Interval when schedule.EveryDays is not { } days || days is < 1 or > GoalSchedule.MaxEveryDays:
                errors.Add(
                    field,
                    new(FieldErrorCodes.OutOfRange, Min: 1, Max: GoalSchedule.MaxEveryDays),
                    $"An interval must be between 1 and {GoalSchedule.MaxEveryDays} days.");
                break;

            case ScheduleKind.Weekdays when schedule.Weekdays is not { Count: > 0 }:
                errors.Add(field, new(FieldErrorCodes.NoWeekday), "Choose at least one weekday.");
                break;

            case ScheduleKind.Weekdays when schedule.Weekdays.Any(day => !Enum.IsDefined(day)):
                errors.Add(field, new(FieldErrorCodes.UnknownOption), "That is not a weekday.");
                break;

            case ScheduleKind.Times when schedule.Times is not { } times || times is < 1 or > GoalSchedule.MaxTimesPerPeriod:
                errors.Add(
                    field,
                    new(FieldErrorCodes.OutOfRange, Min: 1, Max: GoalSchedule.MaxTimesPerPeriod),
                    $"A quota must be between 1 and {GoalSchedule.MaxTimesPerPeriod} times.");
                break;

            case ScheduleKind.Times when schedule.Period is not { } period || !Enum.IsDefined(period) || period == QuotaPeriod.Day:
                errors.Add(field, new(FieldErrorCodes.UnknownOption), "Choose a week or a month.");
                break;

            case ScheduleKind.Limit:
                errors.Add(field, new(FieldErrorCodes.UnknownOption), "Only a Don't has a limit.");
                break;

            default:
                break;
        }

        return errors;
    }

    /// <summary>
    /// What is wrong with a one-off's day, or null when nothing is.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Validate(CreateGoalRequest)"/> because it needs
    /// a clock: the day is checked against the owner's today, in the owner's
    /// time zone, which only the service knows (#94). No day at all is fine —
    /// the one-off is then due today, as before.
    /// </remarks>
    public static (FieldErrorCode Code, string Message)? ValidateTargetDate(DateOnly? targetDate, GoalSchedule schedule, DateOnly today)
    {
        if (schedule.Kind != ScheduleKind.Once || targetDate is not { } day)
        {
            return null;
        }

        if (day < today)
        {
            return (new(FieldErrorCodes.PastDay), "A one-off cannot be due on a day that has already passed.");
        }

        if (day > today.AddDays(Goal.MaxTargetDaysAhead))
        {
            return (
                new(FieldErrorCodes.TooFarAhead, Max: Goal.MaxTargetDaysAhead),
                $"A one-off may be due at most {Goal.MaxTargetDaysAhead} days ahead.");
        }

        return null;
    }

    /// <summary>
    /// The domain schedule a validated request asks for.
    /// </summary>
    /// <remarks>
    /// Defaults to every day when the request says nothing, which is the
    /// commitment people mean when they do not say otherwise.
    /// </remarks>
    public static GoalSchedule ToSchedule(GoalScheduleRequest? request, GoalKind kind = GoalKind.Do) => kind == GoalKind.Dont
        ? GoalSchedule.Per(request?.Period ?? QuotaPeriod.Day)
        : request?.Kind switch
        {
            ScheduleKind.Once => GoalSchedule.Once(),
            ScheduleKind.Interval => GoalSchedule.EveryNDays(request.EveryDays ?? 1),
            ScheduleKind.Weekdays => GoalSchedule.OnWeekdays(request.Weekdays),
            ScheduleKind.Times => GoalSchedule.TimesPer(request.Times ?? 1, request.Period ?? QuotaPeriod.Week),
            _ => GoalSchedule.EveryNDays(1),
        };
}

/// <summary>
/// Validates a request to edit a running goal.
/// </summary>
/// <remarks>
/// The shape of each field that was sent, with the create sheet's messages and
/// codes. Whether it fits *this* goal — a limit on a Do, a one-off becoming a
/// repeating goal, a later day too close to the deadline — is
/// <see cref="Goal.PlanEdit"/>'s to say, because only the goal knows.
/// </remarks>
public static class EditGoalRequestValidator
{
    public static FieldErrors Validate(EditGoalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();

        if (request.Title is not null)
        {
            CreateGoalRequestValidator.AddTitleErrors(request.Title, nameof(request.Title), errors);
        }

        if (request.Description is { } description && description.Trim().Length > Goal.MaxDescriptionLength)
        {
            errors.Add(
                nameof(request.Description),
                new(FieldErrorCodes.TooLong, Max: Goal.MaxDescriptionLength),
                $"A description may be at most {Goal.MaxDescriptionLength} characters long.");
        }

        CreateGoalRequestValidator.AddIconErrors(request.Icon, nameof(request.Icon), errors);

        // A Don't's period, or a Do's schedule; which of the two this goal
        // takes is the goal's to say.
        if (request.Schedule is { Kind: ScheduleKind.Limit } period)
        {
            if (period.Period is not { } value || !Enum.IsDefined(value))
            {
                errors.Add(nameof(request.Schedule), new(FieldErrorCodes.UnknownOption), "Choose a day, a week or a month.");
            }
        }
        else if (request.Schedule is { } schedule)
        {
            errors.Add(CreateGoalRequestValidator.ValidateSchedule(schedule));
        }

        if (request.Limit is { } limit && limit is < 0 or > Goal.MaxLimit)
        {
            errors.Add(
                nameof(request.Limit),
                new(FieldErrorCodes.OutOfRange, Min: 0, Max: Goal.MaxLimit),
                $"A limit must be between 0 and {Goal.MaxLimit}.");
        }

        if (request.ProofMode is { } mode && !Enum.IsDefined(mode))
        {
            errors.Add(nameof(request.ProofMode), new(FieldErrorCodes.UnknownOption), "Choose how your friends can check this.");
        }

        if (request.ParticipantIds is { } participants)
        {
            if (participants.Count == 0)
            {
                errors.Add(
                    nameof(request.ParticipantIds),
                    new(FieldErrorCodes.NoChecker),
                    "A goal needs at least one friend to check it.");
            }
            else if (participants.Count > Goal.MaxParticipants)
            {
                errors.Add(
                    nameof(request.ParticipantIds),
                    new(FieldErrorCodes.TooMany, Max: Goal.MaxParticipants),
                    $"A goal may have at most {Goal.MaxParticipants} participants.");
            }
            else if (participants.Any(id => id == Guid.Empty))
            {
                errors.Add(nameof(request.ParticipantIds), new(FieldErrorCodes.Invalid), "Participant ids must not be empty.");
            }
        }

        return errors;
    }

    /// <summary>The domain schedule an edit asks for, or null when it leaves the schedule alone.</summary>
    /// <remarks>
    /// Built from the kind the request names rather than the goal's, so a
    /// Do's schedule sent for a Don't reaches <see cref="Goal.PlanEdit"/> as
    /// what it is and is refused there, instead of quietly becoming a period.
    /// </remarks>
    public static GoalSchedule? ToSchedule(GoalScheduleRequest? request, GoalKind kind) => request switch
    {
        null => null,
        { Kind: ScheduleKind.Limit } => GoalSchedule.Per(request.Period ?? QuotaPeriod.Day),
        { Kind: null } when kind == GoalKind.Dont => GoalSchedule.Per(request.Period ?? QuotaPeriod.Day),
        _ => CreateGoalRequestValidator.ToSchedule(request, GoalKind.Do),
    };
}

/// <summary>
/// Validates a request to write the waiting Do.
/// </summary>
/// <remarks>
/// The "what" and "how often" halves of <see cref="CreateGoalRequestValidator"/>,
/// without friends — a waiting Do is what somebody makes when they have none —
/// and with one rule of its own: it repeats, because a one-off has a day, and
/// nobody knows on which day the first friend arrives.
/// <see cref="GoalDraft.Describe"/> still enforces all of it itself.
/// </remarks>
public static class SaveGoalDraftRequestValidator
{
    public static FieldErrors Validate(SaveGoalDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();

        CreateGoalRequestValidator.AddTitleErrors(request.Title, nameof(request.Title), errors);
        CreateGoalRequestValidator.AddIconErrors(request.Icon, nameof(request.Icon), errors);
        errors.Add(CreateGoalRequestValidator.ValidateSchedule(request.Schedule));

        if (request.Schedule?.Kind == ScheduleKind.Once)
        {
            errors.Add(nameof(request.Schedule), new(FieldErrorCodes.MustRepeat), "A waiting Do has to repeat.");
        }

        return errors;
    }
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
    public static FieldErrors Validate(RequestPauseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();

        if (!PauseRules.IsReasonValid(request.Reason))
        {
            errors.Add(
                nameof(request.Reason),
                new(FieldErrorCodes.TooShort, Min: PauseRules.MinReasonLength),
                $"A pause needs a reason of at least {PauseRules.MinReasonLength} characters.");
        }
        else if (request.Reason!.Trim().Length > PauseRules.MaxReasonLength)
        {
            errors.Add(
                nameof(request.Reason),
                new(FieldErrorCodes.TooLong, Max: PauseRules.MaxReasonLength),
                $"A reason may be at most {PauseRules.MaxReasonLength} characters long.");
        }

        if (request.Days is not { } days)
        {
            errors.Add(nameof(request.Days), new(FieldErrorCodes.Required), "A pause needs a length in days.");
        }
        else if (days is < PauseRules.MinDays or > PauseRules.MaxDays)
        {
            errors.Add(
                nameof(request.Days),
                new(FieldErrorCodes.OutOfRange, Min: PauseRules.MinDays, Max: PauseRules.MaxDays),
                $"A pause runs for between {PauseRules.MinDays} and {PauseRules.MaxDays} days.");
        }

        return errors;
    }
}
