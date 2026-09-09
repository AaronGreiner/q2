using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Observability;

/// <summary>
/// The Sentry integration, driven through the real SDK.
/// </summary>
/// <remarks>
/// These tests do not assert that a wrapper method was called. A request goes
/// through the real pipeline, the real SDK builds the event, the real
/// <c>BeforeSend</c> runs, the envelope is serialised — and only the network
/// send is replaced by <see cref="Q2.Api.Infrastructure.Observability.Testing.RecordingTransport"/>.
/// Assertions run against the serialised payload, so "this value is not in the
/// event" means it would not have been transmitted.
/// </remarks>
[Trait("Category", "Sentry")]
public class SentryPipelineTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task TheSdkIsInitialisedInTheTestEnvironment()
    {
        var status = await (await Client.GetAsync("/api/diagnostics/sentry", TestContext.Current.CancellationToken))
            .ReadAsync<SentryStatusDocument>();

        Assert.True(status.Enabled);
        Assert.True(status.RecordingTransport);
        Assert.Equal(SentryEnvironments.AutomatedTest, status.Environment);

        // Logs and metrics are part of "is reporting working?", so the page
        // that answers it says whether they are on.
        Assert.True(status.Logs);
        Assert.True(status.Metrics);
    }

    [Fact]
    public async Task AnUnexpectedErrorProducesExactlyOneEvent()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        // One failure, one issue — the exception passes both our handler and
        // the Sentry logging integration, and must not be reported twice.
        Assert.Single(await Factory.RecordedEventsAsync());
    }

    [Fact]
    public async Task EventsCarryTheEnvironmentAndReleaseThisRunWasConfiguredWith()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = (await Factory.RecordedEventsAsync()).Single();

        Assert.Equal(SentryEnvironments.AutomatedTest, recorded.Environment);
        Assert.Equal("q2@integration-tests", recorded.Release);
        Assert.Equal("error", recorded.Level);
        Assert.Equal("q2-api", recorded.Tags["service.name"]);
    }

    [Fact]
    public async Task TheEventDescribesTheActualException()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = (await Factory.RecordedEventsAsync()).Single();

        Assert.Contains("DiagnosticsTestException", recorded.ExceptionTypes.Single());
    }

    [Fact]
    public async Task ExpectedValidationFailuresProduceNoEvent()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "", progressPercent = 900 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Factory.RecordedEventsAsync());
    }

    [Fact]
    public async Task AMissingResourceProducesNoEvent()
    {
        await Client.GetAsync("/api/goals/11111111-1111-4111-8111-111111111111", TestContext.Current.CancellationToken);

        Assert.Empty(await Factory.RecordedEventsAsync());
    }

    [Fact]
    public async Task HealthChecksProduceNoEvent()
    {
        for (var i = 0; i < 5; i++)
        {
            await Client.GetAsync("/health", TestContext.Current.CancellationToken);
        }

        Assert.Empty(await Factory.RecordedEventsAsync());
    }

    [Fact]
    public async Task CredentialsAndLocationDataNeverReachTheEvent()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/diagnostics/boom?token=supersecret123&lat=48.1372&lon=11.5756");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "aaa.bbb.ccc");
        request.Headers.Add("Cookie", "q2_session=should-not-appear");
        request.Headers.Add("X-Api-Key", "key-should-not-appear");

        await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var recorded = (await Factory.RecordedEventsAsync()).Single();

        Assert.False(recorded.Contains("supersecret123"), "The query token reached Sentry.");
        Assert.False(recorded.Contains("aaa.bbb.ccc"), "The bearer token reached Sentry.");
        Assert.False(recorded.Contains("should-not-appear"), "A cookie or api key reached Sentry.");
        Assert.False(recorded.Contains("48.1372"), "Location data reached Sentry.");
        Assert.False(recorded.Contains("11.5756"), "Location data reached Sentry.");
    }

    /// <summary>
    /// The IP address is deliberately reported; nothing else about the user is.
    /// </summary>
    /// <remarks>
    /// Asserted on the serialised payload rather than on the options, because
    /// the two ways to break this are both invisible in configuration:
    /// <c>SendDefaultPii</c> going back to false, and
    /// <see cref="SentryEventScrubber"/> clearing <c>User.IpAddress</c> again.
    /// See docs/privacy.md section 4.
    /// </remarks>
    [Fact]
    public async Task OnlyTheIpAddressIdentifiesTheCaller()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = (await Factory.RecordedEventsAsync()).Single();

        Assert.True(recorded.Contains("\"ip_address\""), "The IP address did not reach Sentry.");

        // The host the code runs on is still never reported.
        Assert.Null(recorded.ServerName);
        Assert.False(recorded.Contains(Environment.MachineName));

        Assert.False(recorded.Contains("\"email\""));
        Assert.False(recorded.Contains("\"username\""));
    }

    [Fact]
    public async Task UserContentIsNotAttachedToEvents()
    {
        const string personalTitle = "Therapy appointment every Tuesday";

        await Client.PostJsonAsync("/api/goals", new { title = personalTitle });
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = (await Factory.RecordedEventsAsync()).Single();

        // Goal titles and descriptions are personal by nature; nothing in the
        // error path is allowed to carry them along.
        Assert.False(recorded.Contains(personalTitle), "A goal title reached Sentry.");
    }

    [Fact]
    public async Task ErrorsAlsoArriveAsStructuredLogs()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var logs = await Factory.RecordedLogsAsync(log => log.Level == "error");

        // The event says what broke; the logs say what the system was doing
        // around it. Without both, an issue is a stack trace with no context.
        Assert.Contains(logs, log => log.Level == "error");
    }

    [Fact]
    public async Task LogsCarryTheEnvironmentAndReleaseThisRunWasConfiguredWith()
    {
        Factory.Logger("Q2.Api.Tests").LogWarning("A line with an {Attribute}", "attribute");

        var log = Assert.Single(
            await Factory.RecordedLogsAsync(entry => entry.Contains("A line with an attribute")),
            entry => entry.Contains("A line with an attribute"));

        Assert.Contains(SentryEnvironments.AutomatedTest, log.RawJson, StringComparison.Ordinal);
        Assert.Contains("q2@integration-tests", log.RawJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one place where logs are more dangerous than events, and the reason
    /// <see cref="SentryEventScrubber.ScrubLog"/> overwrites named attributes.
    /// </summary>
    /// <remarks>
    /// <c>SendDefaultPii</c> makes the SDK attach the signed-in user to every
    /// log — including <c>user.email</c>, which is the address somebody signs in
    /// with — and <c>server.address</c>, which is the machine name events have
    /// stripped since the beginning. None of it is visible in the configuration,
    /// so only an assertion on the payload can notice it coming back.
    /// </remarks>
    [Fact]
    public async Task LogsCarryNeitherTheSignedInPersonNorTheMachine()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var logs = await Factory.RecordedLogsAsync(log => log.Level == "error");

        Assert.NotEmpty(logs);

        foreach (var log in logs)
        {
            Assert.False(log.Contains(AutomatedTestSeed.CurrentPersonEmail), "An email address reached Sentry Logs.");
            Assert.False(log.Contains(Environment.MachineName), "The machine name reached Sentry Logs.");
            Assert.False(log.Contains("\"user.id\":{\"value\":\"7"), "A person id reached Sentry Logs.");
        }
    }

    /// <summary>
    /// A log cannot be edited on its way out — <c>Message</c> and
    /// <c>Template</c> are init-only, and the attributes copied from the message
    /// parameters cannot be enumerated — so the scrubber's only safe answer is
    /// to drop the line entirely.
    /// </summary>
    [Fact]
    public async Task ALogLineCarryingASecretIsDroppedRatherThanSent()
    {
        var logger = Factory.Logger("Q2.Api.Tests");

        logger.LogWarning("Calling https://example.test/api?token=supersecret123");
        logger.LogWarning("Loaded {Count} goals", 7);

        // Waiting for the harmless line proves the batch holding both was
        // processed, so the missing one was dropped rather than still queued.
        var logs = await Factory.RecordedLogsAsync(log => log.Contains("Loaded 7 goals"));

        Assert.Contains(logs, log => log.Contains("Loaded 7 goals"));
        Assert.DoesNotContain(logs, log => log.Contains("supersecret123"));
    }

    [Fact]
    public async Task AnEmailAddressInALogLineIsDroppedRatherThanSent()
    {
        var logger = Factory.Logger("Q2.Api.Tests");

        logger.LogWarning("Mail sent to robin.sample@example.com");
        logger.LogWarning("Mail sent to {Count} people", 3);

        var logs = await Factory.RecordedLogsAsync(log => log.Contains("Mail sent to 3 people"));

        Assert.Contains(logs, log => log.Contains("Mail sent to 3 people"));
        Assert.DoesNotContain(logs, log => log.Contains("robin.sample@example.com"));
    }

    [Fact]
    public async Task UserContentNamedAsALogParameterIsDroppedRatherThanSent()
    {
        var logger = Factory.Logger("Q2.Api.Tests");

        logger.LogWarning("Created goal {GoalTitle}", "Therapy appointment every Tuesday");
        logger.LogWarning("Created {Count} goals", 1);

        var logs = await Factory.RecordedLogsAsync(log => log.Contains("Created 1 goals"));

        Assert.Contains(logs, log => log.Contains("Created 1 goals"));
        Assert.DoesNotContain(logs, log => log.Contains("Therapy appointment every Tuesday"));
    }

    [Fact]
    public async Task CreatingAGoalCountsItWithoutSayingWhichOne()
    {
        const string personalTitle = "Therapy appointment every Tuesday";

        await Client.PostJsonAsync("/api/goals", new
        {
            title = personalTitle,
            schedule = new { kind = "Weekdays", weekdays = new[] { "Tuesday" } },
        });

        var metrics = await Factory.RecordedMetricsAsync(metric => metric.Name == Q2Metrics.GoalCreated);
        var recorded = Assert.Single(metrics, metric => metric.Name == Q2Metrics.GoalCreated);

        Assert.Equal("counter", recorded.Type);
        Assert.Equal(1, recorded.Value);
        Assert.Equal("Weekdays", recorded.Attributes["schedule"]);

        // A counter says how often, never about what.
        Assert.False(recorded.Contains(personalTitle), "A goal title reached a metric.");
    }

    [Fact]
    public async Task AMetricNotDeclaredInQ2MetricsIsNeverSent()
    {
        Factory.Hub.Metrics.EmitCounter("something.undeclared", 1);
        Factory.Hub.Metrics.EmitCounter(Q2Metrics.AccountSignedIn, 1);

        var metrics = await Factory.RecordedMetricsAsync(metric => metric.Name == Q2Metrics.AccountSignedIn);

        Assert.Contains(metrics, metric => metric.Name == Q2Metrics.AccountSignedIn);
        Assert.DoesNotContain(metrics, metric => metric.Name == "something.undeclared");
    }

    [Fact]
    public async Task ABrokenSentryTransportDoesNotBreakTheApi()
    {
        Factory.SentryEvents.FailOnSend = true;

        try
        {
            var failing = await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, failing.StatusCode);

            // Reporting is best-effort; the API must keep answering normally.
            var afterwards = await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken);
            afterwards.EnsureSuccessStatusCode();
        }
        finally
        {
            Factory.SentryEvents.FailOnSend = false;
        }
    }

    private sealed record SentryStatusDocument(
        bool Enabled,
        string Environment,
        string Release,
        bool RecordingTransport,
        bool Logs,
        bool Metrics);
}
