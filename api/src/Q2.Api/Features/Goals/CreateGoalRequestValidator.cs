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

        if (request.ProgressPercent is { } progress && progress is < 0 or > 100)
        {
            errors[nameof(request.ProgressPercent)] = ["Progress must be between 0 and 100."];
        }

        if (request.Participants is { } participants)
        {
            if (participants.Count > Goal.MaxParticipants)
            {
                errors[nameof(request.Participants)] =
                    [$"A goal may have at most {Goal.MaxParticipants} participants."];
            }
            else if (participants.Any(p => string.IsNullOrWhiteSpace(p)))
            {
                errors[nameof(request.Participants)] = ["Participant names must not be empty."];
            }
            else if (participants.Any(p => p.Trim().Length > Goal.MaxParticipantNameLength))
            {
                errors[nameof(request.Participants)] =
                    [$"A participant name may be at most {Goal.MaxParticipantNameLength} characters long."];
            }
        }

        return errors;
    }
}
