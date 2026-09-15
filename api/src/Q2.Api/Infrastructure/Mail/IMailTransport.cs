namespace Q2.Api.Infrastructure.Mail;

/// <summary>Where a composed mail leaves the process.</summary>
/// <remarks>
/// One interface, three implementations, each for a stated reason:
/// <see cref="SmtpMailTransport"/> for a deployment, <see cref="FileMailTransport"/>
/// so development and the E2E suite need no running mail service (AGENTS.md
/// section 6), and <see cref="DisabledMailTransport"/> for a deployment that has
/// no mail account yet. Which one runs is configuration, decided once at
/// startup (<see cref="MailSettings"/>, <see cref="MailRegistration"/>).
///
/// The same shape as <c>IPushSender</c>: <see cref="IsConfigured"/> is what a
/// feature asks before it promises somebody a mail.
/// </remarks>
public interface IMailTransport
{
    /// <summary>Whether this deployment can send anything at all.</summary>
    bool IsConfigured { get; }

    /// <exception cref="InvalidOperationException">This deployment sends no mail.</exception>
    Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken);
}
