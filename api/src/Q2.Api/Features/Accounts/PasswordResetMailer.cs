using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Mail;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Accounts;

/// <summary>Issues a reset token for an address and mails the link — if the address has an account.</summary>
/// <remarks>
/// Runs after the request has been answered (<see cref="IPasswordResetQueue"/>),
/// which is why "no such account" is a log line here rather than an answer
/// anybody receives.
///
/// The token is Identity's (<see cref="UserManager{TUser}.GeneratePasswordResetTokenAsync"/>):
/// data-protected, bound to the account's security stamp, and valid for
/// <see cref="AccountPolicy.PasswordResetLinkLifetime"/>. Nothing about it is
/// stored, so there is nothing here to leak or to clean up.
/// </remarks>
public sealed class PasswordResetMailer(
    Q2DbContext database,
    UserManager<AppUser> users,
    IMailTransport transport,
    MailSettings settings,
    PasswordResetCooldown cooldown,
    TimeProvider timeProvider,
    ILogger<PasswordResetMailer> logger)
{
    public async Task SendAsync(string email, CancellationToken cancellationToken)
    {
        var account = await users.FindByEmailAsync(email);

        if (account?.Email is null)
        {
            // Not a warning: somebody mistyped, or is trying addresses. Either
            // way nothing is sent, and the answer they got said nothing.
            logger.LogInformation("Password reset asked for an address with no account; nothing sent.");
            return;
        }

        if (!cooldown.TryClaim(account.Id, timeProvider.GetUtcNow()))
        {
            logger.LogInformation(
                "Password reset mail for person {PersonId} not sent: one went out less than {CooldownMinutes} minutes ago.",
                account.PersonId,
                AccountPolicy.PasswordResetMailCooldown.TotalMinutes);
            return;
        }

        try
        {
            var identityToken = await users.GeneratePasswordResetTokenAsync(account);
            var link = PasswordResetToken.Link(PublicAppUrl(), PasswordResetToken.Encode(account.Id, identityToken));

            // The language the person chose in q2, not the one their browser
            // happens to have: a mail is read wherever they read their mail.
            var language = await database.UserSettings
                .AsNoTracking()
                .Where(preferences => preferences.PersonId == account.PersonId)
                .Select(preferences => (LanguagePreference?)preferences.Language)
                .SingleOrDefaultAsync(cancellationToken) ?? LanguagePreference.German;

            await transport.SendAsync(PasswordResetMail.For(account.Email, language, link), cancellationToken);
        }
        catch
        {
            cooldown.Release(account.Id);
            throw;
        }

        logger.LogInformation("Password reset mail sent for person {PersonId}.", account.PersonId);
    }

    private Uri PublicAppUrl() => settings.PublicAppUrl
        ?? throw new InvalidOperationException("Q2:PublicAppUrl is not configured, so a reset link has nowhere to point.");
}
