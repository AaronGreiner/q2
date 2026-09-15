namespace Q2.Api.Infrastructure.Mail;

/// <summary>Writes each mail to a file instead of sending it.</summary>
/// <remarks>
/// One <c>.eml</c> per mail in <see cref="MailSettings.FileDirectory"/>, which
/// any mail client opens and the E2E suite reads a link out of. That is the
/// whole of the local sink: nothing to install and nothing listening, so the
/// rule that developing q2 needs no running service still holds (AGENTS.md
/// section 6).
///
/// Development, ManualTesting and E2E only — <see cref="MailSettings"/> refuses
/// it anywhere a real person's reset mail could end up on a disk.
/// </remarks>
public sealed class FileMailTransport(
    MailSettings settings,
    TimeProvider time,
    ILogger<FileMailTransport> logger) : IMailTransport
{
    public bool IsConfigured => true;

    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        Directory.CreateDirectory(settings.FileDirectory);

        // Sortable, so the newest mail is the last name in the listing.
        var path = Path.Combine(
            settings.FileDirectory,
            $"{time.GetUtcNow():yyyyMMdd'T'HHmmssfff}-{Guid.NewGuid():N}.eml");

        // Written beside the target and moved into place, so whatever is
        // watching the directory never reads half a mail.
        var temporary = $"{path}.tmp";

        try
        {
            await using (var stream = File.Create(temporary))
            {
                await mail.ToMimeMessage(settings).WriteToAsync(stream, cancellationToken);
            }

            File.Move(temporary, path);
        }
        catch
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            throw;
        }

        // The file, never the address: somebody running `bun run dev` needs to
        // know where the mail went, and nothing more.
        logger.LogInformation("Mail written to {MailFile} instead of being sent.", path);
    }
}
