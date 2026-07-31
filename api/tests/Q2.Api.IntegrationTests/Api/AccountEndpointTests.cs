using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Registering, signing in, signing out — and what the rest of the API answers
/// to somebody who has not done any of that.
/// </summary>
/// <remarks>
/// The unauthenticated, wrong-person and correct-person cases ADR 0006 asks for
/// before authentication ships. They run against the real Identity stack: a
/// real password hash, a real cookie, a real sign-in.
/// </remarks>
[Trait("Category", "Integration")]
public class AccountEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record PersonDocument(Guid Id, string DisplayName, string Handle, string Initials, string AvatarColor);

    private sealed record SessionDocument(PersonDocument Person, string Email);

    private sealed record ProblemDocument(string? Title, string? Detail, string? Reason);

    /// <summary>What the tests here register with. Long, like the policy asks.</summary>
    private const string NewPassword = "ein-langes-passwort";

    [Fact]
    public async Task RegisteringCreatesAnAccountAndSignsItIn()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Neue Person",
            email = "neue.person@kudos.example",
            password = NewPassword,
        });

        response.EnsureSuccessStatusCode();

        var session = await response.ReadAsync<SessionDocument>();
        Assert.Equal("Neue Person", session.Person.DisplayName);
        Assert.Equal("neue.person@kudos.example", session.Email);

        // Derived rather than asked for, so registration stays three fields.
        Assert.Equal("@neue.person", session.Person.Handle);
        Assert.Equal("NP", session.Person.Initials);

        // Signed in already: the response carries the session cookie, so
        // nobody has to type the password they just chose a second time.
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ARegisteredPersonStartsWithTheirOwnEmptyWorld()
    {
        await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Ganz Neu",
            email = "ganz.neu@kudos.example",
            password = NewPassword,
        });

        var signedIn = await Factory.CreateClient().AcceptingJson()
            .AsAsync("ganz.neu@kudos.example", NewPassword);

        // None of the seeded person's goals, tasks or chats. This is the whole
        // reason ownership had to arrive together with accounts.
        var goals = await (await signedIn.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<object>>();
        var tasks = await (await signedIn.GetAsync("/api/tasks", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<object>>();
        var chats = await (await signedIn.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<object>>();

        Assert.Empty(goals);
        Assert.Empty(tasks);
        Assert.Empty(chats);
    }

    [Fact]
    public async Task TwoPeopleWithTheSameNameGetDifferentHandles()
    {
        await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Lena Schmidt",
            email = "lena.one@kudos.example",
            password = NewPassword,
        });

        var second = await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Lena Schmidt",
            email = "lena.two@kudos.example",
            password = NewPassword,
        });

        second.EnsureSuccessStatusCode();
        Assert.Equal("@lena.schmidt-2", (await second.ReadAsync<SessionDocument>()).Person.Handle);
    }

    [Fact]
    public async Task AnAddressThatIsAlreadyTakenIsAFieldError()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Doppelt",
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = NewPassword,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AFailedRegistrationLeavesNoHalfCreatedPersonBehind()
    {
        var before = 0;
        await Factory.WithDatabaseAsync(async database =>
            before = await database.People.CountAsync(TestContext.Current.CancellationToken));

        await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Doppelt",
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = NewPassword,
        });

        // The person is written before the account, so a rejected account has
        // to take the person with it — otherwise every duplicate address would
        // leave an orphan nobody can sign in as.
        await Factory.WithDatabaseAsync(async database =>
            Assert.Equal(before, await database.People.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("", "valid@kudos.example", NewPassword)]
    [InlineData("Ohne Adresse", "", NewPassword)]
    [InlineData("Keine Adresse", "not-an-email", NewPassword)]
    [InlineData("Kurzes Passwort", "kurz@kudos.example", "zu-kurz")]
    public async Task AnUnusableRegistrationIsRejectedWithFieldErrors(string name, string email, string password)
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/auth/register",
            new { name, email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SigningInWithASeededAccountWorks()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/login", new
        {
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = SeedAccounts.Password,
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            AutomatedTestSeed.CurrentPersonId,
            (await response.ReadAsync<SessionDocument>()).Person.Id);
    }

    [Fact]
    public async Task AWrongPasswordAndAnUnknownAddressAnswerTheSameWay()
    {
        var wrongPassword = await AnonymousClient.PostJsonAsync("/api/auth/login", new
        {
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = "definitely-not-the-password",
        });

        var unknownAddress = await AnonymousClient.PostJsonAsync("/api/auth/login", new
        {
            email = "nobody@kudos.example",
            password = SeedAccounts.Password,
        });

        // A different answer would turn this endpoint into a way to find out
        // who has an account here.
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAddress.StatusCode);

        var first = await wrongPassword.ReadAsync<ProblemDocument>();
        var second = await unknownAddress.ReadAsync<ProblemDocument>();

        Assert.Equal(first.Detail, second.Detail);
        Assert.Equal(AuthenticationFailures.InvalidCredentials, first.Reason);
        Assert.Equal(AuthenticationFailures.InvalidCredentials, second.Reason);
    }

    [Fact]
    public async Task TheSessionEndpointSaysWhoIsSignedIn()
    {
        var session = await (await Client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken))
            .ReadAsync<SessionDocument>();

        Assert.Equal(AutomatedTestSeed.CurrentPersonId, session.Person.Id);
        Assert.Equal(AutomatedTestSeed.CurrentPersonEmail, session.Email);
    }

    [Fact]
    public async Task WithoutASessionTheSessionEndpointAnswers401()
    {
        var response = await AnonymousClient.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SigningOutSucceedsEvenWithoutASession()
    {
        // Somebody who signs out is unsure of their state; an error there would
        // leave them holding the cookie they were trying to get rid of.
        var response = await AnonymousClient.PostAsync(
            "/api/auth/logout",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/profile")]
    [InlineData("/api/goals")]
    [InlineData("/api/tasks")]
    [InlineData("/api/feed")]
    [InlineData("/api/leaderboard")]
    [InlineData("/api/friends")]
    [InlineData("/api/chats")]
    [InlineData("/api/settings")]
    public async Task EveryFeatureEndpointRefusesAnAnonymousCaller(string url)
    {
        // The guard is on the endpoint group rather than on each route, and
        // this is what proves no group was forgotten.
        var response = await AnonymousClient.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthStaysReachableWithoutASession()
    {
        // A load balancer does not sign in.
        var response = await AnonymousClient.GetAsync("/health", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

}
