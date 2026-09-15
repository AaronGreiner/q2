using MimeKit;
using Q2.Api.Infrastructure.Mail;

namespace Q2.Api.UnitTests.Mail;

/// <summary>The MIME form a mail leaves the process in.</summary>
public class OutgoingMailTests
{
    private static readonly MailSettings Settings = new()
    {
        Transport = MailTransportKind.File,
        From = "noreply@q2.example.com",
        FromName = "Qdos",
        PublicAppUrl = new Uri("https://q2.example.com"),
        FileDirectory = Path.GetTempPath(),
        SmtpHost = string.Empty,
        SmtpPort = MailSettings.DefaultSmtpPort,
        SmtpUserName = string.Empty,
        SmtpPassword = string.Empty,
    };

    private static MimeMessage Message() => new OutgoingMail(
        "mara.k@kudos.example",
        "Dein Qdos-Passwort zurücksetzen",
        "Hallo,\n\nhttps://q2.example.com/reset-password#token=abc.def\n").ToMimeMessage(Settings);

    [Fact]
    public void AMailIsPlainTextFromTheConfiguredSender()
    {
        var message = Message();

        Assert.Equal("noreply@q2.example.com", Assert.IsType<MailboxAddress>(Assert.Single(message.From)).Address);
        Assert.Equal("mara.k@kudos.example", Assert.IsType<MailboxAddress>(Assert.Single(message.To)).Address);

        var body = Assert.IsType<TextPart>(message.Body);
        Assert.True(body.IsPlain);
        Assert.Contains("#token=abc.def", body.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageIdNamesTheSendersDomainRatherThanTheMachine()
    {
        // MimeKit's default is the host name of whatever wrote the mail — on a
        // deployment, the server's.
        var id = Message().MessageId;

        Assert.EndsWith("@q2.example.com", id, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.MachineName, id, StringComparison.OrdinalIgnoreCase);
    }
}
