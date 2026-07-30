namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for tests that talk to the API over HTTP.
/// </summary>
/// <remarks>
/// The database, the recorded Sentry events and the id sequence are all rebuilt
/// before <em>every</em> test, not once per class. That costs a few
/// milliseconds and buys the property the test strategy actually depends on:
/// any test can be run alone, in any order, repeatedly, with the same result.
///
/// Test classes share nothing — each gets its own <see cref="Q2ApiFactory"/>
/// and therefore its own in-memory database, so classes can run in parallel.
/// </remarks>
public abstract class ApiTestBase(Q2ApiFactory factory) : IClassFixture<Q2ApiFactory>, IAsyncLifetime
{
    protected Q2ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; private set; } = null!;

    public virtual async ValueTask InitializeAsync()
    {
        await Factory.ResetAsync();
        Client = Factory.CreateClient();
    }

    public virtual ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
