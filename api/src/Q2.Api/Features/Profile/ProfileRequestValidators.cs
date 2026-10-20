using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;

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
    /// <summary>Returns what is wrong, per field, with the codes the app words it by (#80). Empty means "valid".</summary>
    public static FieldErrors Validate(UpdateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();

        // Null means "leave it alone"; a blank string means somebody cleared
        // the field and pressed save, which is a different mistake and deserves
        // to be named.
        if (request.DisplayName is { } name)
        {
            var trimmed = name.Trim();

            if (trimmed.Length == 0)
            {
                errors.Add(nameof(request.DisplayName), new(FieldErrorCodes.Required), "A display name is required.");
            }
            else if (trimmed.Length > Person.MaxDisplayNameLength)
            {
                errors.Add(
                    nameof(request.DisplayName),
                    new(FieldErrorCodes.TooLong, Max: Person.MaxDisplayNameLength),
                    $"A display name may be at most {Person.MaxDisplayNameLength} characters long.");
            }
        }

        if (request.Bio is { } bio && bio.Trim().Length > Person.MaxBioLength)
        {
            errors.Add(
                nameof(request.Bio),
                new(FieldErrorCodes.TooLong, Max: Person.MaxBioLength),
                $"A bio may be at most {Person.MaxBioLength} characters long.");
        }

        if (request.AvatarImageId == Guid.Empty)
        {
            errors.Add(nameof(request.AvatarImageId), new(FieldErrorCodes.Invalid), "An image id must not be empty.");
        }

        return errors;
    }
}
