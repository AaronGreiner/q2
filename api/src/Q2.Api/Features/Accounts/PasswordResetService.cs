using Microsoft.AspNetCore.Identity;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Mail;

namespace Q2.Api.Features.Accounts;

/// <summary>Asking for a reset link, and setting a new password with one.</summary>
/// <remarks>
/// Nothing about the token is written here. Identity issues it, binds it to the
/// account's security stamp and checks it
/// (<see cref="UserManager{TUser}.ResetPasswordAsync"/>). Because a reset
/// changes the stamp, the same link cannot be used twice, and every session of
/// the account stops being accepted within
/// <see cref="AccountPolicy.SessionRecheckInterval"/>
/// (docs/adr/0026-mail-and-password-reset.md).
/// </remarks>
public sealed class PasswordResetService(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    IMailTransport transport,
    IPasswordResetQueue queue,
    ILogger<PasswordResetService> logger)
{
    /// <summary>Hands the address on to be mailed, whether or not it has an account.</summary>
    /// <exception cref="DomainValidationException">That is not an address.</exception>
    /// <exception cref="AccessDeniedException">This deployment sends no mail.</exception>
    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = AccountRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        // Asked before the address is looked at, and answered the same for
        // every address: whether mail works is a fact about this deployment,
        // not about anybody's account.
        if (!transport.IsConfigured)
        {
            logger.LogInformation("Password reset asked for, but this deployment sends no mail.");

            throw new AccessDeniedException(
                "Password reset is not available here: this deployment sends no mail.",
                PasswordResetFailures.MailUnavailable);
        }

        await queue.EnqueueAsync(request.Email!.Trim(), cancellationToken);

        logger.LogInformation("Password reset asked for.");
    }

    /// <summary>Sets a new password with the token from a reset link, and ends every session of the account.</summary>
    /// <exception cref="DomainValidationException">
    /// The link is unusable — expired, used, tampered with or never ours, all
    /// with the same answer — or the password breaks the policy.
    /// </exception>
    public async Task<PasswordResetResponse> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = AccountRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var account = PasswordResetToken.TryDecode(request.Token, out var accountId, out var identityToken)
            ? await users.FindByIdAsync(accountId.ToString())
            : null;

        if (account is null)
        {
            throw InvalidLink();
        }

        var result = await users.ResetPasswordAsync(account, identityToken, request.Password!);

        if (!result.Succeeded)
        {
            // Identity answers InvalidToken for a link that is expired, used, or
            // meant for somebody else, and it is right not to say which.
            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                throw InvalidLink();
            }

            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Password)] = [.. result.Errors.Select(error => error.Description)],
            });
        }

        // The way back into an account is also the way out of a lockout: a
        // lockout exists to stop somebody guessing, and this person did not guess.
        await users.ResetAccessFailedCountAsync(account);

        if (await users.IsLockedOutAsync(account))
        {
            await users.SetLockoutEndDateAsync(account, null);
        }

        // This device as well. Whoever set the new password signs in with it,
        // exactly like every other device the account was open on.
        await signIn.SignOutAsync();

        logger.LogInformation("Password reset for person {PersonId}; every session of the account ends.", account.PersonId);

        return new PasswordResetResponse(account.Email ?? string.Empty);
    }

    private static DomainValidationException InvalidLink() =>
        new(nameof(ResetPasswordRequest.Token), AccountRequestValidator.InvalidResetLink);
}
