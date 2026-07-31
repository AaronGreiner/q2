using System.Net;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// What every guarded endpoint does when nobody is signed in.
/// </summary>
/// <remarks>
/// One property, asserted across the whole surface rather than feature by
/// feature: a request without a session is <em>refused</em>, and refusing is
/// not the same as falling over.
///
/// This exists because of a real release. Until
/// docs/adr/0011-authentication-with-identity.md the current person was a row
/// flagged in the database, and resolving it threw an ordinary
/// <see cref="InvalidOperationException"/> when the flag was missing — a 500.
/// Staging is never seeded (SeedProfile "None", and
/// <see cref="Q2.Api.Infrastructure.Persistence.DatabaseResetGuard"/> refuses
/// destructive work in a protected environment), so the flag could not exist
/// there and every person-scoped screen answered 500. Nothing failed loudly
/// enough to notice: the deployment's own checks saw a healthy /health and a
/// well-formed HTML error page, and reported success.
///
/// A 500 from an unauthenticated request is therefore the exact signature of
/// that class of bug, which is why the assertion below is "401, and nothing in
/// the 5xx range" rather than merely "not 200".
/// </remarks>
[Trait("Category", "Integration")]
public class AnonymousRequestTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>
    /// Every guarded GET the app calls to paint a screen. The five the start
    /// screen needs are all here, because those are the ones whose failure is
    /// the whole app failing.
    /// </summary>
    public static TheoryData<string> GuardedRoutes =>
    [
        "/api/profile",
        "/api/feed",
        "/api/leaderboard",
        "/api/goals",
        "/api/tasks",
        "/api/friends",
        "/api/chats",
        "/api/settings",
        "/api/auth/session",
    ];

    [Theory]
    [MemberData(nameof(GuardedRoutes))]
    public async Task WithoutASessionAGuardedEndpointRefusesRatherThanFails(string route)
    {
        var response = await AnonymousClient.GetAsync(route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthIsAnsweredWithoutASession()
    {
        // The counterpart to the theory above: /health has to stay reachable
        // for the deployment to be able to poll it, so it must not be swept
        // behind the guard by a future change.
        var response = await AnonymousClient.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
