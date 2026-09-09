using Q2.Api.Features.People;

namespace Q2.Api.Features.Profile;

/// <summary>
/// Validates a profile change before it reaches the domain.
/// </summary>
/// <remarks>
/// Not a duplicate of <see cref="Person.Rename"/> — that still enforces
/// everything itself. This is the layer that turns a bad request into a
/// field-level message the sign-up and profile screens can put under the right
/// input, and it takes its limits from the domain so there is one source for
/// the number.
/// </remarks>
public static class UpdateProfileRequestValidator
{
    /// <summary>Returns field name → messages. An empty dictionary means "valid".</summary>
    public static Dictionary<string, string[]> Validate(UpdateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();

        // Null means "leave it alone"; a blank string means somebody cleared
        // the field and pressed save, which is a different mistake and deserves
        // to be named.
        if (request.DisplayName is { } name)
        {
            var trimmed = name.Trim();

            if (trimmed.Length == 0)
            {
                errors[nameof(request.DisplayName)] = ["A display name is required."];
            }
            else if (trimmed.Length > Person.MaxDisplayNameLength)
            {
                errors[nameof(request.DisplayName)] =
                    [$"A display name may be at most {Person.MaxDisplayNameLength} characters long."];
            }
        }

        if (request.AvatarImageId == Guid.Empty)
        {
            errors[nameof(request.AvatarImageId)] = ["An image id must not be empty."];
        }

        return errors;
    }
}
