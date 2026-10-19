using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Mail;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Accounts;

/// <summary>Changing the password while signed in (#40).</summary>
/// <remarks>
/// Identity does the work (<see cref="UserManager{TUser}.ChangePasswordAsync"/>),
/// and changing the password changes the security stamp, so every other session
/// of the account stops being accepted within
/// <see cref="AccountPolicy.SessionRecheckInterval"/> — the same rule as a
/// reset (docs/adr/0026-mail-and-password-reset.md). Unlike a reset, this
/// device stays signed in: whoever just proved the current password is the one
/// person not to send back to the sign-in screen.
/// </remarks>
public sealed class PasswordChangeService(
    Q2DbContext database,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    CurrentPerson currentPerson,
    IMailTransport transport,
    ILogger<PasswordChangeService> logger)
{
    /// <exception cref="DomainValidationException">
    /// A field is missing, the new password breaks the policy, the current one
    /// is wrong, or the account is locked out after too many wrong ones.
    /// </exception>
    public async Task<PasswordChangeResponse> ChangeAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = AccountRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var me = await currentPerson.GetAsync(cancellationToken);

        var account = await database.Users.SingleOrDefaultAsync(user => user.PersonId == me.Id, cancellationToken)
            ?? throw new AuthenticationRequiredException("This session has no account behind it.");

        /*
         * Counted towards the lockout, unlike the check before deleting an
         * account. A stolen session could otherwise guess the password here
         * as often as it liked, and the prize is the whole account.
         *
         * A field error rather than a 401: the session is fine, the answer is
         * wrong. A 401 would also be retried once by the iOS app with a fresh
         * token (api/client.ts), counting one wrong password twice.
         */
        var check = await signIn.CheckPasswordSignInAsync(account, request.CurrentPassword!, lockoutOnFailure: true);

        if (check.IsLockedOut)
        {
            throw new DomainValidationException(
                nameof(request.CurrentPassword),
                "Too many wrong passwords. Try again in a few minutes.",
                new(FieldErrorCodes.LockedOut));
        }

        if (!check.Succeeded)
        {
            throw new DomainValidationException(
                nameof(request.CurrentPassword),
                "That password is not correct.",
                new(FieldErrorCodes.WrongPassword));
        }

        var result = await users.ChangePasswordAsync(account, request.CurrentPassword!, request.NewPassword!);

        if (!result.Succeeded)
        {
            throw new DomainValidationException(
                nameof(request.NewPassword),
                result.Errors.First().Description,
                new(FieldErrorCodes.Invalid));
        }

        // A new cookie with the new stamp, so this device is not the next one
        // the stamp check signs out. The refresh keeps how the session was
        // opened; only the stamp in it changes.
        await signIn.RefreshSignInAsync(account);

        logger.LogInformation("Password changed for person {PersonId}; every other session of the account ends.", account.PersonId);

        await NotifyAsync(account, cancellationToken);

        return new PasswordChangeResponse(account.Email ?? string.Empty);
    }

    /// <summary>Tells the address on the account that its password changed.</summary>
    /// <remarks>
    /// After the change rather than before, and never able to undo it: the
    /// password is set by now, and a mail service having a bad minute must not
    /// make somebody believe it was not. A deployment with no mail sends none,
    /// which is a fact about the deployment rather than a failure.
    /// </remarks>
    private async Task NotifyAsync(AppUser account, CancellationToken cancellationToken)
    {
        if (!transport.IsConfigured || account.Email is null)
        {
            return;
        }

        try
        {
            var language = await database.UserSettings
                .AsNoTracking()
                .Where(preferences => preferences.PersonId == account.PersonId)
                .Select(preferences => (LanguagePreference?)preferences.Language)
                .SingleOrDefaultAsync(cancellationToken) ?? LanguagePreference.German;

            await transport.SendAsync(PasswordChangedMail.For(account.Email, language), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The notice that person {PersonId}'s password changed could not be sent.", account.PersonId);
        }
    }
}
