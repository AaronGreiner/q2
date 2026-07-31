using Q2.Api.Infrastructure.Persistence.Seeding;

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
///
/// <see cref="Client"/> arrives signed in as the AutomatedTest seed's primary
/// person, because that is what every test about goals, chats or the feed
/// needs and none of them are about signing in. Tests that need somebody else
/// call <see cref="ClientForAsync"/>; tests about being signed out use
/// <see cref="AnonymousClient"/>.
/// </remarks>
public abstract class ApiTestBase(Q2ApiFactory factory) : IClassFixture<Q2ApiFactory>, IAsyncLifetime
{
    private readonly List<HttpClient> _clients = [];

    protected Q2ApiFactory Factory { get; } = factory;

    /// <summary>Signed in as <see cref="AutomatedTestSeed.CurrentPersonEmail"/>.</summary>
    protected HttpClient Client { get; private set; } = null!;

    /// <summary>No session at all, for the tests that check the guard holds.</summary>
    protected HttpClient AnonymousClient { get; private set; } = null!;

    public virtual async ValueTask InitializeAsync()
    {
        await Factory.ResetAsync();

        AnonymousClient = Track(Factory.CreateClient().AcceptingJson());
        Client = await ClientForAsync(AutomatedTestSeed.CurrentPersonEmail);
    }

    /// <summary>A client signed in as whoever owns <paramref name="email"/>.</summary>
    /// <remarks>
    /// What makes the two-sided tests possible: a friend request looks different
    /// from each end, and only a test that can be both ends can say so.
    /// </remarks>
    protected async Task<HttpClient> ClientForAsync(string email) =>
        await Track(Factory.CreateClient().AcceptingJson()).AsAsync(email);

    public virtual ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
        return ValueTask.CompletedTask;
    }

    private HttpClient Track(HttpClient client)
    {
        _clients.Add(client);
        return client;
    }
}
