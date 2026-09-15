using System.Net.Mail;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// Validates the shape of the requests under <c>/api/auth</c> before Identity
/// ever sees them.
/// </summary>
/// <remarks>
/// Identity validates too, and its answers are perfectly correct — they are
/// just not answers a person can act on: <c>PasswordTooShort</c> arrives as an
/// error code, and the whole set is phrased for a developer. This layer
/// produces the same field names the form uses, in the same shape every other
/// request in q2 rejects with.
///
/// The password length lives in <see cref="AccountPolicy"/> and is read from
/// there by both this validator and the Identity options, so the number on the
/// screen cannot disagree with the number that is enforced.
/// </remarks>
public static class AccountRequestValidator
{
    /// <summary>
    /// What every refused reset link is told, whatever the reason: expired,
    /// used, tampered with, or never one of ours.
    /// </summary>
    public const string InvalidResetLink = "This link is invalid, has expired, or has been used already.";

    /// <summary>
    /// Far longer than any link q2 writes. A bound, like the password's, so a
    /// megabyte of text is refused before anything decodes it.
    /// </summary>
    public const int MaximumResetTokenLength = 2048;

    /// <summary>Returns field name → messages. An empty dictionary means "valid".</summary>
    public static Dictionary<string, string[]> Validate(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors[nameof(request.Name)] = ["A name is required."];
        }
        else if (name.Length > Person.MaxDisplayNameLength)
        {
            errors[nameof(request.Name)] = [$"A name may be at most {Person.MaxDisplayNameLength} characters long."];
        }

        AddEmailErrors(request.Email, nameof(request.Email), errors);
        AddNewPasswordErrors(request.Password, nameof(request.Password), errors);

        return errors;
    }

    /// <summary>
    /// Validates a sign-in request.
    /// </summary>
    /// <remarks>
    /// Only "is something there": whether the password is long enough is not
    /// this endpoint's business, and telling somebody their input is too short
    /// to be a password of ours would say something about which passwords exist.
    /// </remarks>
    public static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        AddEmailErrors(request.Email, nameof(request.Email), errors);

        if (string.IsNullOrEmpty(request.Password))
        {
            errors[nameof(request.Password)] = ["A password is required."];
        }

        return errors;
    }

    /// <summary>Validates a request for a reset link: an address, and nothing about whether it has an account.</summary>
    public static Dictionary<string, string[]> Validate(ForgotPasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        AddEmailErrors(request.Email, nameof(request.Email), errors);

        return errors;
    }

    /// <summary>Validates that a token came with a new password, and that the password would be accepted.</summary>
    /// <remarks>
    /// The same password rule as registration, checked before the token is
    /// used, so a password that would be refused never spends the link.
    /// </remarks>
    public static Dictionary<string, string[]> Validate(ResetPasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > MaximumResetTokenLength)
        {
            errors[nameof(request.Token)] = [InvalidResetLink];
        }

        AddNewPasswordErrors(request.Password, nameof(request.Password), errors);

        return errors;
    }

    private static void AddNewPasswordErrors(string? value, string field, Dictionary<string, string[]> errors)
    {
        var password = value ?? string.Empty;

        if (password.Length == 0)
        {
            errors[field] = ["A password is required."];
        }
        else if (password.Length < AccountPolicy.MinimumPasswordLength)
        {
            errors[field] = [$"A password must be at least {AccountPolicy.MinimumPasswordLength} characters long."];
        }
        else if (password.Length > AccountPolicy.MaximumPasswordLength)
        {
            // Not a security rule — a bound, so a megabyte of text cannot be
            // handed to the hasher.
            errors[field] = [$"A password may be at most {AccountPolicy.MaximumPasswordLength} characters long."];
        }
    }

    private static void AddEmailErrors(string? value, string field, Dictionary<string, string[]> errors)
    {
        var email = value?.Trim();

        if (string.IsNullOrEmpty(email))
        {
            errors[field] = ["An email address is required."];
            return;
        }

        if (email.Length > AccountPolicy.MaximumEmailLength)
        {
            errors[field] = [$"An email address may be at most {AccountPolicy.MaximumEmailLength} characters long."];
            return;
        }

        // MailAddress rather than a regular expression: the grammar in RFC 5322
        // is not something a pattern should be trying to express, and every
        // attempt at one either rejects valid addresses or accepts nonsense.
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('.'))
        {
            errors[field] = ["That does not look like an email address."];
        }
    }
}
