using MimeKit;
using MimeKit.Text;
using MimeKit.Utils;

namespace Q2.Api.Infrastructure.Mail;

/// <summary>One mail as q2 writes it: one recipient, a subject, plain text.</summary>
/// <remarks>
/// Plain text and nothing else. A mail from q2 carries a few sentences and one
/// link; an HTML part would be a second rendering of the same words to keep in
/// step, and remote images a mail client has to decide about
/// (docs/adr/0026-mail-and-password-reset.md).
///
/// A class rather than a record on purpose: a record prints every property in
/// <c>ToString()</c>, and the body of a reset mail is a key to an account.
/// </remarks>
public sealed class OutgoingMail(string to, string subject, string body)
{
    public string To { get; } = to;

    public string Subject { get; } = subject;

    public string Body { get; } = body;

    /// <summary>The MIME message both transports hand on.</summary>
    /// <remarks>
    /// 8bit rather than quoted-printable, so a mail file somebody opens — or a
    /// test reads a link out of — says "zurücksetzen" rather than
    /// "zur=C3=BCcksetzen" and keeps a long link on one line. MailKit re-encodes
    /// before it sends to a server that does not announce 8BITMIME.
    /// </remarks>
    public MimeMessage ToMimeMessage(MailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var sender = new MailboxAddress(settings.FromName, settings.From);

        var message = new MimeMessage();
        message.From.Add(sender);
        message.To.Add(MailboxAddress.Parse(To));
        message.Subject = Subject;

        // On the sender's domain. MimeKit's default is the host name of the
        // machine that wrote the mail, which nobody who receives one has any
        // business learning (docs/privacy.md: no machine names).
        message.MessageId = MimeUtils.GenerateMessageId(sender.Domain);
        message.Body = new TextPart(TextFormat.Plain)
        {
            Text = Body,
            ContentTransferEncoding = ContentEncoding.EightBit,
        };

        return message;
    }
}
