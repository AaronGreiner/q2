using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Mail;

namespace Q2.Api.UnitTests.Mail;

/// <summary>
/// The rules a deployment's mail configuration is held to before the host
/// takes its first request.
/// </summary>
public class MailSettingsTests
{
    /// <summary>A complete SMTP setup, as a deployment renders it.</summary>
    private static readonly (string Key, string? Value)[] WorkingSmtp =
    [
        ("Q2:PublicAppUrl", "https://q2.example.com"),
        ("Q2:Mail:Transport", "Smtp"),
        ("Q2:Mail:From", "noreply@q2.example.com"),
        ("Q2:Mail:Smtp:Host", "smtp.example.com"),
        ("Q2:Mail:Smtp:UserName", "q2"),
        ("Q2:Mail:Smtp:Password", "a-secret-nobody-may-print"),
    ];

    /// <summary>What development runs with.</summary>
    private static readonly (string Key, string? Value)[] WorkingFile =
    [
        ("Q2:PublicAppUrl", "http://localhost:3000"),
        ("Q2:Mail:Transport", "File"),
        ("Q2:Mail:From", "noreply@q2.invalid"),
    ];

    [Fact]
    public void NothingConfiguredMeansNoMail()
    {
        var settings = Resolve(ApplicationEnvironments.Development);

        Assert.Equal(MailTransportKind.Off, settings.Transport);
        Assert.Null(settings.PublicAppUrl);
    }

    [Fact]
    public void StagingMayRunWithoutMailUntilItHasAnAccount()
    {
        Assert.Equal(MailTransportKind.Off, Resolve(ApplicationEnvironments.Staging).Transport);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Off")]
    [InlineData("File")]
    public void ProductionRefusesToStartWithoutSmtp(string transport)
    {
        // A reset is the only way back into an account. A production host that
        // cannot send one must not look as if it could.
        var refused = Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Production,
            [.. WorkingFile.Where(entry => entry.Key != "Q2:Mail:Transport"), ("Q2:Mail:Transport", transport)]));

        Assert.Contains("Production", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionWithSmtpStarts()
    {
        var settings = Resolve(ApplicationEnvironments.Production, WorkingSmtp);

        Assert.Equal(MailTransportKind.Smtp, settings.Transport);
        Assert.Equal(MailSettings.DefaultSmtpPort, settings.SmtpPort);
        Assert.Equal(new Uri("https://q2.example.com"), settings.PublicAppUrl);
    }

    [Fact]
    public void StagingNeverWritesMailToDisk()
    {
        // A reset mail is a key to somebody's account for an hour.
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Staging,
            [.. WorkingFile.Where(entry => entry.Key != "Q2:PublicAppUrl"), ("Q2:PublicAppUrl", "https://q2.example.com")]));
    }

    [Fact]
    public void OnceMailIsOnALinkBaseIsRequired()
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Development,
            [.. WorkingFile.Where(entry => entry.Key != "Q2:PublicAppUrl")]));
    }

    [Fact]
    public void AProtectedHostLinksOverHttpsOnly()
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Staging,
            [.. WorkingSmtp.Where(entry => entry.Key != "Q2:PublicAppUrl"), ("Q2:PublicAppUrl", "http://q2.example.com")]));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("ftp://q2.example.com")]
    [InlineData("https://q2.example.com/?next=somewhere")]
    public void ALinkBaseThatIsNotAPlainAddressIsRefused(string value)
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Development,
            [.. WorkingFile.Where(entry => entry.Key != "Q2:PublicAppUrl"), ("Q2:PublicAppUrl", value)]));
    }

    [Fact]
    public void SmtpNeedsAServer()
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Staging,
            [.. WorkingSmtp.Where(entry => entry.Key != "Q2:Mail:Smtp:Host")]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Qdos <noreply@q2.example.com>")]
    [InlineData("noreply")]
    public void TheSenderHasToBeOneAddress(string from)
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Staging,
            [.. WorkingSmtp.Where(entry => entry.Key != "Q2:Mail:From"), ("Q2:Mail:From", from)]));
    }

    [Theory]
    [InlineData("Sendgrid")]
    [InlineData("2")]
    public void AnUnknownTransportIsRefusedRatherThanGuessed(string transport)
    {
        Assert.Throws<MailConfigurationException>(() => Resolve(
            ApplicationEnvironments.Development,
            ("Q2:Mail:Transport", transport)));
    }

    [Fact]
    public void ImplicitTlsIsChosenByItsPort()
    {
        var settings = Resolve(ApplicationEnvironments.Staging, [.. WorkingSmtp, ("Q2:Mail:Smtp:Port", "465")]);

        Assert.Equal(MailSettings.ImplicitTlsPort, settings.SmtpPort);
    }

    [Fact]
    public void MailFilesLandBesideTheDatabaseUnlessConfigured()
    {
        var settings = Resolve(ApplicationEnvironments.Development, WorkingFile);

        Assert.True(Path.IsPathRooted(settings.FileDirectory));
        Assert.Equal("mail", Path.GetFileName(settings.FileDirectory));
    }

    [Fact]
    public void ThePasswordIsNeverPartOfHowTheSettingsPrint()
    {
        // A class, not a record, for exactly this: a record's ToString would
        // put the SMTP password into any log line that printed the settings.
        var settings = Resolve(ApplicationEnvironments.Staging, WorkingSmtp);

        Assert.DoesNotContain("a-secret-nobody-may-print", settings.ToString(), StringComparison.Ordinal);
    }

    private static MailSettings Resolve(string environment, params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value)))
            .Build();

        return MailSettings.FromConfiguration(configuration, new TestHostEnvironment(environment));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Q2.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
