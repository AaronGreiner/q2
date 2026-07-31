using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Observability;

namespace Q2.Api.UnitTests.Observability;

[Trait("Category", "Sentry")]
public class SentryEnvironmentsTests
{
    [Theory]
    [InlineData(ApplicationEnvironments.Development, SentryEnvironments.LocalDevelopment)]
    [InlineData(ApplicationEnvironments.ManualTesting, SentryEnvironments.ManualTesting)]
    [InlineData(ApplicationEnvironments.AutomatedTest, SentryEnvironments.AutomatedTest)]
    [InlineData(ApplicationEnvironments.E2E, SentryEnvironments.E2E)]
    [InlineData(ApplicationEnvironments.Staging, SentryEnvironments.Staging)]
    [InlineData(ApplicationEnvironments.Production, SentryEnvironments.Production)]
    public void EachEnvironmentMapsToItsOwnSentryEnvironment(string aspNetCore, string expected)
    {
        Assert.Equal(expected, SentryEnvironments.For(aspNetCore));
    }

    [Fact]
    public void EveryMappingIsDistinct()
    {
        var mapped = ApplicationEnvironments.All.Select(SentryEnvironments.For).ToList();

        // A local run must never land in the same Sentry environment as production.
        Assert.Equal(mapped.Count, mapped.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AnUnknownEnvironmentIsNotMistakenForProduction()
    {
        var mapped = SentryEnvironments.For("SomethingNew");

        Assert.StartsWith("unknown-", mapped);
        Assert.NotEqual(SentryEnvironments.Production, mapped);
    }
}

[Trait("Category", "Sentry")]
public class SentrySettingsTests
{
    private static IHostEnvironment Environment(string name) => new TestHostEnvironment(name);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Q2.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void WithoutADsnSentryIsDisabledButTheApplicationStillStarts()
    {
        var settings = SentrySettings.FromConfiguration(Configuration(), Environment(ApplicationEnvironments.Development));

        Assert.False(settings.Enabled);
        Assert.Equal(SentryEnvironments.LocalDevelopment, settings.Environment);
        Assert.NotEmpty(settings.Release);
    }

    [Fact]
    public void ADsnEnablesSentryWithoutAnExplicitFlag()
    {
        var settings = SentrySettings.FromConfiguration(
            Configuration(("Sentry:Dsn", "https://publickey@sentry.example.com/42")),
            Environment(ApplicationEnvironments.Development));

        Assert.True(settings.Enabled);
    }

    [Fact]
    public void ExplicitlyEnabledWithoutADsnFailsWithAReadableMessage()
    {
        var exception = Assert.Throws<SentryConfigurationException>(() => SentrySettings.FromConfiguration(
            Configuration(("Sentry:Enabled", "true")),
            Environment(ApplicationEnvironments.Development)));

        Assert.Contains("no DSN is configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMalformedDsnFailsWithoutEchoingTheValue()
    {
        const string secretLookingValue = "not-a-dsn-but-secret";

        var exception = Assert.Throws<SentryConfigurationException>(() => SentrySettings.FromConfiguration(
            Configuration(("Sentry:Dsn", secretLookingValue)),
            Environment(ApplicationEnvironments.Development)));

        Assert.DoesNotContain(secretLookingValue, exception.Message);
    }

    [Fact]
    public void TheRecordingTransportSuppliesItsOwnPlaceholderDsn()
    {
        var settings = SentrySettings.FromConfiguration(
            Configuration(("Sentry:TestTransport", "recording")),
            Environment(ApplicationEnvironments.AutomatedTest));

        Assert.True(settings.UseRecordingTransport);
        Assert.True(settings.Enabled);
        Assert.Equal(SentryEnvironments.AutomatedTest, settings.Environment);
    }

    [Theory]
    [InlineData(ApplicationEnvironments.Staging)]
    [InlineData(ApplicationEnvironments.Production)]
    public void TheRecordingTransportIsRefusedInProtectedEnvironments(string environmentName)
    {
        Assert.Throws<SentryConfigurationException>(() => SentrySettings.FromConfiguration(
            Configuration(("Sentry:TestTransport", "recording")),
            Environment(environmentName)));
    }

    [Fact]
    public void ErrorEventsAreNeverSampledAway()
    {
        foreach (var environmentName in ApplicationEnvironments.All)
        {
            var settings = SentrySettings.FromConfiguration(Configuration(), Environment(environmentName));

            Assert.Equal(1.0f, settings.SampleRate);
        }
    }

    [Fact]
    public void TraceSamplingIsFullLocallyAndReducedInProduction()
    {
        var local = SentrySettings.FromConfiguration(Configuration(), Environment(ApplicationEnvironments.Development));
        var test = SentrySettings.FromConfiguration(Configuration(), Environment(ApplicationEnvironments.AutomatedTest));
        var production = SentrySettings.FromConfiguration(Configuration(), Environment(ApplicationEnvironments.Production));

        Assert.Equal(1.0, local.TracesSampleRate);

        // A test that waits for a specific trace must not lose it to chance.
        Assert.Equal(1.0, test.TracesSampleRate);
        Assert.True(production.TracesSampleRate < 1.0);
    }

    [Fact]
    public void LogsAndMetricsAreOnUnlessAnEnvironmentTurnsThemOff()
    {
        var settings = SentrySettings.FromConfiguration(Configuration(), Environment(ApplicationEnvironments.Development));

        Assert.True(settings.EnableLogs);
        Assert.True(settings.EnableMetrics);
    }

    [Theory]
    [InlineData("Sentry:EnableLogs", true)]
    [InlineData("SENTRY_ENABLE_LOGS", true)]
    [InlineData("Sentry:EnableMetrics", false)]
    [InlineData("SENTRY_ENABLE_METRICS", false)]
    public void LogsAndMetricsCanBeTurnedOffFromEitherConfigurationSource(string key, bool turnsOffLogs)
    {
        var settings = SentrySettings.FromConfiguration(
            Configuration((key, "false")),
            Environment(ApplicationEnvironments.Production));

        Assert.Equal(!turnsOffLogs, settings.EnableLogs);
        Assert.Equal(turnsOffLogs, settings.EnableMetrics);
    }

    [Fact]
    public void TestTagsAreAttachedWhenTheHarnessSuppliesThem()
    {
        var settings = SentrySettings.FromConfiguration(
            Configuration(
                ("Sentry:TestTransport", "recording"),
                ("TEST_RUN_ID", "run-42"),
                ("TEST_SEED_PROFILE", "E2E"),
                ("GIT_COMMIT_SHA", "abc123")),
            Environment(ApplicationEnvironments.E2E));

        Assert.Equal("run-42", settings.DefaultTags["test.run_id"]);
        Assert.Equal("E2E", settings.DefaultTags["seed.profile"]);
        Assert.Equal("abc123", settings.DefaultTags["git.sha"]);
        Assert.Equal("q2-api", settings.DefaultTags["service.name"]);
    }
}

[Trait("Category", "Sentry")]
public class ReleaseIdentityTests
{
    [Fact]
    public void AnExplicitReleaseWins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Sentry:Release", "q2@1.2.3")])
            .Build();

        Assert.Equal("q2@1.2.3", ReleaseIdentity.Resolve(configuration));
    }

    [Fact]
    public void ACommitShaBecomesAShortenedRelease()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>("GIT_COMMIT_SHA", "0123456789abcdef0123456789abcdef01234567"),
            ])
            .Build();

        Assert.Equal("q2@0123456789ab", ReleaseIdentity.Resolve(configuration));
    }

    [Fact]
    public void TheFallbackIsStableAcrossCalls()
    {
        var configuration = new ConfigurationBuilder().Build();

        // Release ids must never be random: two starts of the same build have
        // to report the same value or Sentry cannot group them.
        Assert.Equal(ReleaseIdentity.Resolve(configuration), ReleaseIdentity.Resolve(configuration));
        Assert.StartsWith("q2@", ReleaseIdentity.Resolve(configuration));
    }
}
