using MailKit.Net.Smtp;
using MailKit.Security;

namespace Q2.Api.Infrastructure.Mail;

/// <summary>Hands a mail to the SMTP server of the deployment's mail provider.</summary>
/// <remarks>
/// SMTP rather than a provider's own HTTP API, so the provider is configuration:
/// every transactional mail service speaks it, and moving from one to another is
/// a handful of values in the environment rather than a new class
/// (docs/adr/0026-mail-and-password-reset.md).
///
/// One connection per mail. q2 sends a handful a day, and a pooled connection
/// is a connection that has to notice it went stale.
///
/// TLS is required, never negotiated away: port 465 speaks it from the first
/// byte, and any other port has to upgrade with STARTTLS or the send fails.
/// MailKit's <c>Auto</c> would carry on in plain text against a server that
/// offered no upgrade — with the account's password in it.
/// </remarks>
public sealed class SmtpMailTransport(MailSettings settings) : IMailTransport
{
    /// <summary>Ten seconds, like a push: a reset mail that late is one somebody has already asked for again.</summary>
    private const int TimeoutMilliseconds = 10_000;

    public bool IsConfigured => true;

    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        using var client = new SmtpClient { Timeout = TimeoutMilliseconds };

        var security = settings.SmtpPort == MailSettings.ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, security, cancellationToken);

        if (settings.SmtpUserName.Length > 0)
        {
            await client.AuthenticateAsync(settings.SmtpUserName, settings.SmtpPassword, cancellationToken);
        }

        await client.SendAsync(mail.ToMimeMessage(settings), cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
