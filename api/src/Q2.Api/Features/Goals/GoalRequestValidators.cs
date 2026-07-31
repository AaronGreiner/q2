namespace Q2.Api.Features.Goals;

/// <summary>
/// Validates the incoming request shape before it reaches the domain.
/// </summary>
/// <remarks>
/// This is not a duplicate of the domain rules — it is the layer that turns a
/// request into good field-level messages, and it reads its limits from the
/// domain constants so the numbers have exactly one source of truth.
/// <see cref="Goal.Create"/> still enforces everything itself; a request that
/// slips past this validator produces a
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

        if (request.TotalSteps is { } steps && steps is < 1 or > Goal.MaxSteps)
        {
            errors[nameof(request.TotalSteps)] = [$"A goal must have between 1 and {Goal.MaxSteps} steps."];
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
}

/// <summary>Validates the incoming shape of a new task. See the goal validator for why.</summary>
public static class CreateTaskRequestValidator
{
    public static Dictionary<string, string[]> Validate(CreateTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            errors[nameof(request.Title)] = ["A title is required."];
        }
        else if (title.Length > GoalTask.MaxTitleLength)
        {
            errors[nameof(request.Title)] = [$"A title may be at most {GoalTask.MaxTitleLength} characters long."];
        }

        if (request.TargetValue is { } target && target <= 0)
        {
            errors[nameof(request.TargetValue)] = ["A target amount must be greater than zero."];
        }

        if (request.MeasureUnit is { } unit && unit.Trim().Length > GoalTask.MaxUnitLength)
        {
            errors[nameof(request.MeasureUnit)] =
                [$"A unit may be at most {GoalTask.MaxUnitLength} characters long."];
        }

        // A unit with nothing to measure would render as "· L" under the task.
        if (!string.IsNullOrWhiteSpace(request.MeasureUnit) && request.TargetValue is null)
        {
            errors[nameof(request.TargetValue)] = ["A unit needs a target amount to go with it."];
        }

        return errors;
    }
}
