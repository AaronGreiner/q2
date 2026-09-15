using System.Net.Mail;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Infrastructure.Mail;

/// <summary>Mail configuration was requested that cannot be honoured safely.</summary>
public sealed class MailConfigurationException(string message) : Exception(message);

/// <summary>How a mail leaves the process.</summary>
public enum MailTransportKind
{
    /// <summary>Nothing is sent, and a feature that would send says so.</summary>
    Off,

    /// <summary>Each mail becomes a file. Development, ManualTesting and E2E only.</summary>
    File,

    /// <summary>Handed to the SMTP server of the deployment's mail provider.</summary>
    Smtp,
}

/// <summary>
/// The resolved mail configuration for this process: <c>Q2:Mail</c>, plus the
/// <c>Q2:PublicAppUrl</c> every link in a mail starts with.
/// </summary>
/// <remarks>
/// Resolved and checked once, at startup, like <c>SentrySettings</c>: a host
/// whose configuration cannot be honoured fails before its first request rather
/// than on the first person who has forgotten their password. Three rules are
/// not negotiable:
/// <list type="bullet">
///   <item><b>Production sends mail.</b> A reset is the only way back into an
///   account, so a production host without an SMTP account is refused rather
///   than started with recovery quietly missing.</item>
///   <item><b>Staging and Production never write mail to disk.</b> A reset mail
///   is a key to somebody's account for an hour.</item>
///   <item><b>Links start from configuration, never from the request.</b> A
///   reset link built from the <c>Host</c> header points wherever the sender of
///   that request wants it to.</item>
/// </list>
/// Staging may run with <see cref="MailTransportKind.Off"/>, which is how it
/// starts until it has a mail account; the reset screen says so
/// (docs/adr/0026-mail-and-password-reset.md).
///
/// A class rather than a record on purpose: a record prints every property in
/// <c>ToString()</c>, and one of these is the SMTP password.
/// </remarks>
public sealed class MailSettings
{
    public const string SectionName = "Q2:Mail";

    public const string PublicAppUrlKey = "Q2:PublicAppUrl";

    /// <summary>Submission with STARTTLS — what every transactional provider offers.</summary>
    public const int DefaultSmtpPort = 587;

    /// <summary>The one port that speaks TLS from the first byte rather than upgrading to it.</summary>
    public const int ImplicitTlsPort = 465;

    public required MailTransportKind Transport { get; init; }

    /// <summary>The sender address. Empty when <see cref="Transport"/> is Off.</summary>
    public required string From { get; init; }

    public required string FromName { get; init; }

    /// <summary>Where the app is served. Null when unset, which only Off allows.</summary>
    public required Uri? PublicAppUrl { get; init; }

    /// <summary>Absolute. Only written to with <see cref="MailTransportKind.File"/>.</summary>
    public required string FileDirectory { get; init; }

    public required string SmtpHost { get; init; }

    public required int SmtpPort { get; init; }

    public required string SmtpUserName { get; init; }

    /// <summary>A credential, like the Sentry DSN: from the environment, never from a committed file.</summary>
    public required string SmtpPassword { get; init; }

    /// <exception cref="MailConfigurationException">The configuration cannot be honoured.</exception>
    public static MailSettings FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(SectionName);
        var environmentName = environment.EnvironmentName;
        var isProtected = ApplicationEnvironments.Protected.Contains(environmentName);
        var transport = ParseTransport(section["Transport"]);

        if (environmentName == ApplicationEnvironments.Production && transport != MailTransportKind.Smtp)
        {
            throw new MailConfigurationException(
                "Production must be able to send mail: a password reset is the only way back into an account. "
                + "Set Q2:Mail:Transport to Smtp and configure Q2:Mail:Smtp.");
        }

        if (isProtected && transport == MailTransportKind.File)
        {
            throw new MailConfigurationException(
                $"Mail must never be written to disk in '{environmentName}': a reset mail is a key to somebody's account. "
                + "Use Smtp, or Off until this deployment has a mail account.");
        }

        var publicAppUrl = ParsePublicAppUrl(configuration[PublicAppUrlKey], isProtected);
        var from = section["From"]?.Trim() ?? string.Empty;
        var smtpHost = section["Smtp:Host"]?.Trim() ?? string.Empty;

        if (transport != MailTransportKind.Off)
        {
            if (publicAppUrl is null)
            {
                throw new MailConfigurationException(
                    $"Q2:Mail:Transport is {transport}, but Q2:PublicAppUrl is empty, so a link in a mail would have nowhere to point.");
            }

            if (!MailAddress.TryCreate(from, out var parsed) || parsed.Address != from)
            {
                throw new MailConfigurationException(
                    $"Q2:Mail:Transport is {transport}, but Q2:Mail:From is not a single mail address.");
            }
        }

        if (transport == MailTransportKind.Smtp && smtpHost.Length == 0)
        {
            throw new MailConfigurationException("Q2:Mail:Transport is Smtp, but Q2:Mail:Smtp:Host is empty.");
        }

        var configuredDirectory = section["FileDirectory"];
        var fromName = section["FromName"];

        return new MailSettings
        {
            Transport = transport,
            From = from,
            FromName = string.IsNullOrWhiteSpace(fromName) ? "Qdos" : fromName.Trim(),
            PublicAppUrl = publicAppUrl,

            // Beside the database unless configured, like the images: the data
            // directory is the one place a deployment already keeps what it
            // writes (FileSystemImageStore).
            FileDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(DatabaseLocation.ResolveDataDirectory(environment.ContentRootPath), "mail")
                : Path.GetFullPath(configuredDirectory),
            SmtpHost = smtpHost,
            SmtpPort = ParsePort(section["Smtp:Port"]),
            SmtpUserName = section["Smtp:UserName"]?.Trim() ?? string.Empty,
            SmtpPassword = section["Smtp:Password"] ?? string.Empty,
        };
    }

    private static MailTransportKind ParseTransport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MailTransportKind.Off;
        }

        // Names only: Enum.TryParse would also take "2" for Smtp.
        foreach (var kind in Enum.GetValues<MailTransportKind>())
        {
            if (string.Equals(kind.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new MailConfigurationException($"Q2:Mail:Transport must be Off, File or Smtp; '{value}' is none of them.");
    }

    private static int ParsePort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultSmtpPort;
        }

        return int.TryParse(value, out var port) && port is > 0 and <= 65535
            ? port
            : throw new MailConfigurationException($"Q2:Mail:Smtp:Port must be a port number; '{value}' is not one.");
    }

    private static Uri? ParsePublicAppUrl(string? value, bool isProtected)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new MailConfigurationException(
                "Q2:PublicAppUrl must be an absolute http or https address without a query, such as https://q2.example.com.");
        }

        if (isProtected && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new MailConfigurationException(
                "Q2:PublicAppUrl must be https in Staging and Production: a reset link carries a key to an account.");
        }

        return uri;
    }
}
