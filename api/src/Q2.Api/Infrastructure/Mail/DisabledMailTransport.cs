namespace Q2.Api.Infrastructure.Mail;

/// <summary>The transport for a deployment that has no mail account yet.</summary>
/// <remarks>
/// Not a silent no-op. A feature asks <see cref="IsConfigured"/> first and
/// tells the person that no mail will come; anything that sends regardless has
/// a bug worth an exception, rather than a mail that quietly never arrives.
/// </remarks>
public sealed class DisabledMailTransport : IMailTransport
{
    public bool IsConfigured => false;

    public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("This deployment sends no mail (Q2:Mail:Transport is Off).");
}
