using System.Net;
using System.Net.Http.Headers;
using Q2.Api.Infrastructure.Observability;
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
    }

    [Fact]
    public async Task AnUnexpectedErrorProducesExactlyOneEvent()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        // One failure, one issue — the exception passes both our handler and
        // the Sentry logging integration, and must not be reported twice.
        Assert.Single(Factory.SentryEvents.Events);
    }

    [Fact]
    public async Task EventsCarryTheEnvironmentAndReleaseThisRunWasConfiguredWith()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = Factory.SentryEvents.Events.Single();

        Assert.Equal(SentryEnvironments.AutomatedTest, recorded.Environment);
        Assert.Equal("q2@integration-tests", recorded.Release);
        Assert.Equal("error", recorded.Level);
        Assert.Equal("q2-api", recorded.Tags["service.name"]);
    }

    [Fact]
    public async Task TheEventDescribesTheActualException()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = Factory.SentryEvents.Events.Single();

        Assert.Contains("DiagnosticsTestException", recorded.ExceptionTypes.Single());
    }

    [Fact]
    public async Task ExpectedValidationFailuresProduceNoEvent()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "", progressPercent = 900 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.SentryEvents.Events);
    }

    [Fact]
    public async Task AMissingResourceProducesNoEvent()
    {
        await Client.GetAsync("/api/goals/11111111-1111-4111-8111-111111111111", TestContext.Current.CancellationToken);

        Assert.Empty(Factory.SentryEvents.Events);
    }

    [Fact]
    public async Task HealthChecksProduceNoEvent()
    {
        for (var i = 0; i < 5; i++)
        {
            await Client.GetAsync("/health", TestContext.Current.CancellationToken);
        }

        Assert.Empty(Factory.SentryEvents.Events);
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

        var recorded = Factory.SentryEvents.Events.Single();

        Assert.False(recorded.Contains("supersecret123"), "The query token reached Sentry.");
        Assert.False(recorded.Contains("aaa.bbb.ccc"), "The bearer token reached Sentry.");
        Assert.False(recorded.Contains("should-not-appear"), "A cookie or api key reached Sentry.");
        Assert.False(recorded.Contains("48.1372"), "Location data reached Sentry.");
        Assert.False(recorded.Contains("11.5756"), "Location data reached Sentry.");
    }

    [Fact]
    public async Task NoUserIdentityOrMachineNameIsAttached()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = Factory.SentryEvents.Events.Single();

        Assert.Null(recorded.ServerName);
        Assert.False(recorded.Contains(Environment.MachineName));
        Assert.False(recorded.Contains("\"email\""));
        Assert.False(recorded.Contains("\"ip_address\""));
    }

    [Fact]
    public async Task UserContentIsNotAttachedToEvents()
    {
        const string personalTitle = "Therapy appointment every Tuesday";

        await Client.PostJsonAsync("/api/goals", new { title = personalTitle });
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var recorded = Factory.SentryEvents.Events.Single();

        // Goal titles and descriptions are personal by nature; nothing in the
        // error path is allowed to carry them along.
        Assert.False(recorded.Contains(personalTitle), "A goal title reached Sentry.");
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
        bool RecordingTransport);
}
