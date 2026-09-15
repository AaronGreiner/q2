namespace Q2.Api.Infrastructure.Mail;

/// <summary>Registers the one way a mail leaves this process.</summary>
public static class MailRegistration
{
    /// <summary>
    /// Resolves the mail settings — refusing to start on ones that cannot be
    /// honoured — and registers the transport they name.
    /// </summary>
    /// <exception cref="MailConfigurationException">The configuration cannot be honoured.</exception>
    public static WebApplicationBuilder AddQ2Mail(this WebApplicationBuilder builder)
    {
        var settings = MailSettings.FromConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(settings);

        // Chosen here, once. A feature depends on IMailTransport and never asks
        // which one it was given.
        switch (settings.Transport)
        {
            case MailTransportKind.Smtp:
                builder.Services.AddSingleton<IMailTransport, SmtpMailTransport>();
                break;

            case MailTransportKind.File:
                builder.Services.AddSingleton<IMailTransport, FileMailTransport>();
                break;

            default:
                builder.Services.AddSingleton<IMailTransport, DisabledMailTransport>();
                break;
        }

        return builder;
    }
}
