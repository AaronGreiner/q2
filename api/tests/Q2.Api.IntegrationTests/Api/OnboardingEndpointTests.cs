using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The first-run introduction, from the server's side: whether somebody still
/// has it ahead of them, the handle they may choose while it is open, and
/// that it is over for good once it is (docs/adr/0039-first-run-onboarding.md).
/// </summary>
[Trait("Category", "Integration")]
public class OnboardingEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record SessionDocument(SessionPerson Person, string Email, bool IsOnboarded);

    private sealed record SessionPerson(Guid Id, string DisplayName, string Handle);

    private sealed record HandleCheckDocument(string? Handle, string Availability);

    private sealed record ProblemDocument(Dictionary<string, string[]> Errors);

    private async Task<(SessionDocument Session, HttpClient Client)> RegisterAsync(string name, string email)
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/auth/register",
            new { name, email, password = SeedAccounts.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.ReadAsync<SessionDocument>(), await ClientForAsync(email));
    }

    private static async Task<SessionDocument> SessionAsync(HttpClient client) =>
        await (await client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken))
            .ReadAsync<SessionDocument>();

    private static async Task<HandleCheckDocument> CheckAsync(HttpClient client, string handle) =>
        await (await client.PostJsonAsync("/api/onboarding/handle/check", new { handle }))
            .ReadAsync<HandleCheckDocument>();

    [Fact]
    public async Task ASeededPersonHasLongBeenThroughIt()
    {
        Assert.True((await SessionAsync(Client)).IsOnboarded);
    }

    [Fact]
    public async Task ANewAccountHasItAheadOfIt()
    {
        var (registered, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        Assert.False(registered.IsOnboarded);
        Assert.False((await SessionAsync(client)).IsOnboarded);
    }

    [Fact]
    public async Task FinishingItIsRememberedOnTheAccountAndOnlyOnce()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var first = await client.PostAsync("/api/onboarding/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True((await first.ReadAsync<SessionDocument>()).IsOnboarded);

        DateTimeOffset? firstMoment = null;
        await Factory.WithDatabaseAsync(async database => firstMoment = await database.People
            .Where(person => person.Handle == "@nora.neu")
            .Select(person => person.OnboardedAt)
            .SingleAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(firstMoment);

        // Another device, the same account: already done.
        var elsewhere = await ClientForAsync("nora.neu@example.test");
        Assert.True((await SessionAsync(elsewhere)).IsOnboarded);

        var again = await elsewhere.PostAsync("/api/onboarding/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        await Factory.WithDatabaseAsync(async database => Assert.Equal(
            firstMoment,
            await database.People
                .Where(person => person.Handle == "@nora.neu")
                .Select(person => person.OnboardedAt)
                .SingleAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task AHandleIsCheckedAsItIsTyped()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        Assert.Equal(new HandleCheckDocument("@nora.neu", "Current"), await CheckAsync(client, "nora.neu"));
        Assert.Equal(new HandleCheckDocument("@test.two", "Taken"), await CheckAsync(client, "@Test.Two"));
        Assert.Equal(new HandleCheckDocument("@no", "Invalid"), await CheckAsync(client, "no"));
        Assert.Equal(new HandleCheckDocument(null, "Invalid"), await CheckAsync(client, "  "));
        Assert.Equal(new HandleCheckDocument("@nora", "Available"), await CheckAsync(client, " Nora "));
    }

    [Fact]
    public async Task AChosenHandleIsTheOneOnTheProfile()
    {
        var (registered, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var response = await client.PutJsonAsync("/api/onboarding/handle", new { handle = "Nora" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("@nora", (await response.ReadAsync<SessionDocument>()).Person.Handle);
        Assert.Equal("@nora", (await SessionAsync(client)).Person.Handle);

        // And it is what somebody else now finds.
        var search = await Client.GetAsync("/api/friends/search?query=%40nora", TestContext.Current.CancellationToken);
        Assert.Contains(registered.Person.Id.ToString(), await search.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SomebodyElsesHandleIsRefused()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var response = await client.PutJsonAsync("/api/onboarding/handle", new { handle = "test.two" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Handle", (await response.ReadAsync<ProblemDocument>()).Errors.Keys);
        Assert.Equal("@nora.neu", (await SessionAsync(client)).Person.Handle);
    }

    [Fact]
    public async Task OnceTheIntroductionIsOverTheHandleIsFixed()
    {
        // test.one went through it long ago (the seed says so).
        var response = await Client.PutJsonAsync("/api/onboarding/handle", new { handle = "ganz.anders" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("@test.one", (await SessionAsync(Client)).Person.Handle);
    }

    [Fact]
    public async Task NothingHereWorksWithoutASession()
    {
        var check = await AnonymousClient.PostJsonAsync("/api/onboarding/handle/check", new { handle = "nora" });
        var complete = await AnonymousClient.PostAsync("/api/onboarding/complete", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, check.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, complete.StatusCode);
    }
}
