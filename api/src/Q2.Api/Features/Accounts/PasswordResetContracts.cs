namespace Q2.Api.Features.Accounts;

/// <summary>Request body for asking for a reset link.</summary>
/// <remarks>
/// Nullable with a default, like every request DTO here, so an empty body is a
/// field error rather than a binding failure.
/// </remarks>
public sealed record ForgotPasswordRequest(string? Email = null);

/// <summary>Request body for setting a new password with the token from a reset link.</summary>
/// <remarks>
/// <paramref name="Token"/> is everything after <c>#token=</c> in the link, and
/// opaque to the client: it names the account and carries Identity's proof in
/// one string (<see cref="PasswordResetToken"/>).
/// </remarks>
public sealed record ResetPasswordRequest(string? Token = null, string? Password = null);

/// <summary>A reset went through.</summary>
/// <remarks>
/// The address, so the sign-in screen can be filled in for somebody who has
/// just shown they can read that inbox — and nothing else. Every session of the
/// account has ended, the one on this device included, so there is no person
/// to return.
/// </remarks>
public sealed record PasswordResetResponse(string Email);

/// <summary>
/// The machine-readable reasons a reset request can be refused with, beside
/// field errors. Part of the API contract, like <c>AuthenticationFailures</c>:
/// the client picks the sentence.
/// </summary>
public static class PasswordResetFailures
{
    /// <summary>This deployment sends no mail, so no link can come.</summary>
    public const string MailUnavailable = "mailUnavailable";
}
