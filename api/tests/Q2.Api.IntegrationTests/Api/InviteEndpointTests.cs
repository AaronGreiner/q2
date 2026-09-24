using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The way in for somebody who does not know anybody here.
/// </summary>
/// <remarks>
/// The gap this closes is the one both projects had: a new account has no
/// friends, and almost everything in q2 is something you do where friends can
/// see. What is worth a pipeline is that a link actually produces the
/// friendship — for somebody who registers through it and for somebody who
/// already had an account — and that the code is a secret rather than a handle,
/// because a link built from something public would let anybody force a
/// friendship on anybody.
/// </remarks>
[Trait("Category", "Integration")]
public class InviteEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record InviteDocument(string Code);

    private sealed record SessionDocument(SessionPerson Person, string Email);

    private sealed record SessionPerson(Guid Id, string DisplayName, string Handle);

    private sealed record FriendsDocument(IReadOnlyList<FriendRow> Friends);

    private sealed record FriendRow(FriendPerson Person);

    private sealed record FriendPerson(Guid Id, string DisplayName);

    private sealed record PreviewDocument(string DisplayName, string Initials, string AvatarColor, string? Relation);

    private sealed record AcceptedDocument(Guid Id, string DisplayName);

    private const string UnconnectedEmail = "test.four@" + SeedAccounts.EmailDomain;

    private static Task<HttpResponseMessage> PreviewAsync(HttpClient client, string code) =>
        client.PostJsonAsync("/api/invite/preview", new { code });

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string code) =>
        client.PostJsonAsync("/api/invite/accept", new { code });

    private async Task<List<Friendship>> RowsBetweenAsync(Guid one, Guid other)
    {
        var rows = new List<Friendship>();

        await Factory.WithDatabaseAsync(async database => rows = await database.Friendships
            .AsNoTracking()
            .Where(friendship => (friendship.RequesterId == one && friendship.AddresseeId == other)
                || (friendship.RequesterId == other && friendship.AddresseeId == one))
            .ToListAsync(TestContext.Current.CancellationToken));

        return rows;
    }

    private static async Task<InviteDocument> CodeAsync(HttpClient client) =>
        await (await client.GetAsync("/api/invite", TestContext.Current.CancellationToken))
            .ReadAsync<InviteDocument>();

    private async Task<SessionDocument> RegisterAsync(string name, string email, string? inviteCode)
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/auth/register",
            new { name, email, password = SeedAccounts.Password, inviteCode });

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<SessionDocument>();
    }

    [Fact]
    public async Task ACodeIsMadeOnFirstAskAndThenStaysTheSame()
    {
        var first = await CodeAsync(Client);
        var second = await CodeAsync(Client);

        Assert.False(string.IsNullOrWhiteSpace(first.Code));
        Assert.Equal(first.Code, second.Code);

        // Not the handle: a link built from something public and searchable
        // would let anybody force a friendship on anybody.
        Assert.DoesNotContain("test.one", first.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FollowingALinkArrivesAsAFriendship()
    {
        var invite = await CodeAsync(Client);

        var newcomer = await RegisterAsync("Neu Angekommen", "neu.angekommen@example.test", invite.Code);

        // Accepted, not pending: whoever sent the link has already said yes,
        // and a request neither of them can act on is the dead end this exists
        // to remove.
        await Factory.WithDatabaseAsync(async database =>
            Assert.True(await database.Friendships.AnyAsync(
                friendship => friendship.Status == FriendshipStatus.Accepted
                    && ((friendship.RequesterId == AutomatedTestSeed.CurrentPersonId
                            && friendship.AddresseeId == newcomer.Person.Id)
                        || (friendship.RequesterId == newcomer.Person.Id
                            && friendship.AddresseeId == AutomatedTestSeed.CurrentPersonId)),
                TestContext.Current.CancellationToken)));

        var friends = await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken))
            .ReadAsync<FriendsDocument>();

        Assert.Contains(friends.Friends, friend => friend.Person.Id == newcomer.Person.Id);
    }

    /// <summary>
    /// Registration is the worst possible moment to fail over a link somebody
    /// was forwarded. The account is what they came for.
    /// </summary>
    [Fact]
    public async Task AStaleCodeIsIgnoredRatherThanRefused()
    {
        var newcomer = await RegisterAsync("Ohne Freunde", "ohne.freunde@example.test", "nichts-das-je-galt");

        Assert.NotEqual(Guid.Empty, newcomer.Person.Id);

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Friendships.AnyAsync(
                friendship => friendship.RequesterId == newcomer.Person.Id
                    || friendship.AddresseeId == newcomer.Person.Id,
                TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task RegisteringWithoutOneStillWorks()
    {
        var newcomer = await RegisterAsync("Ganz Allein", "ganz.allein@example.test", inviteCode: null);

        Assert.NotEqual(Guid.Empty, newcomer.Person.Id);
    }

    /// <summary>
    /// What makes a leaked link recoverable. The friendships already made
    /// through it stay — those were real invitations that were accepted.
    /// </summary>
    [Fact]
    public async Task ReplacingTheCodeStopsTheOldLinkWorking()
    {
        var old = await CodeAsync(Client);

        var replaced = await (await Client.PostJsonAsync("/api/invite/regenerate", new { }))
            .ReadAsync<InviteDocument>();

        Assert.NotEqual(old.Code, replaced.Code);

        var newcomer = await RegisterAsync("Zu Spaet", "zu.spaet@example.test", old.Code);

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Friendships.AnyAsync(
                friendship => friendship.RequesterId == newcomer.Person.Id
                    || friendship.AddresseeId == newcomer.Person.Id,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// The page a link lands on says whose it is, to somebody with no account
    /// yet — and nothing more than it takes to say that.
    /// </summary>
    [Fact]
    public async Task WithoutASessionALinkNamesItsSenderAndNothingElse()
    {
        var invite = await CodeAsync(Client);

        var response = await PreviewAsync(AnonymousClient, invite.Code);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var preview = await response.ReadAsync<PreviewDocument>();

        Assert.Equal("Test Person One", preview.DisplayName);
        Assert.Equal("T1", preview.Initials);
        Assert.Null(preview.Relation);

        // Whoever holds the link may never sign up; the id and the handle are
        // not theirs to have.
        Assert.DoesNotContain(AutomatedTestSeed.CurrentPersonId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test.one", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AutomatedTestSeed.CurrentPersonEmail, "Self")]
    [InlineData(AutomatedTestSeed.FriendEmail, "Friends")]
    [InlineData(AutomatedTestSeed.RequesterEmail, "RequestSent")]
    [InlineData(UnconnectedEmail, "None")]
    public async Task WithASessionALinkSaysWhereYouAlreadyStand(string visitorEmail, string relation)
    {
        var invite = await CodeAsync(Client);
        var visitor = await ClientForAsync(visitorEmail);

        var preview = await (await PreviewAsync(visitor, invite.Code)).ReadAsync<PreviewDocument>();

        Assert.Equal(relation, preview.Relation);
    }

    [Theory]
    [InlineData("nichts-das-je-galt")]
    [InlineData("")]
    public async Task ACodeThatMeansNothingIsNotFound(string code)
    {
        var response = await PreviewAsync(AnonymousClient, code);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A code in the path would end up in request logs and in Sentry's fetch
    /// breadcrumbs, which keep paths. There is no route that takes one.
    /// </summary>
    [Fact]
    public async Task ACodeIsNeverPartOfAPath()
    {
        var invite = await CodeAsync(Client);

        var probe = await AnonymousClient.GetAsync(
            $"/api/invite/{invite.Code}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, probe.StatusCode);
    }

    [Fact]
    public async Task SomebodyWithAnAccountAcceptsALinkAndIsFriends()
    {
        var invite = await CodeAsync(Client);
        var visitor = await ClientForAsync(UnconnectedEmail);

        var response = await AcceptAsync(visitor, invite.Code);
        var sender = await response.ReadAsync<AcceptedDocument>();

        Assert.Equal(AutomatedTestSeed.CurrentPersonId, sender.Id);

        var rows = await RowsBetweenAsync(AutomatedTestSeed.CurrentPersonId, AutomatedTestSeed.UnconnectedPersonId);
        Assert.Equal(FriendshipStatus.Accepted, Assert.Single(rows).Status);

        var friends = await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken))
            .ReadAsync<FriendsDocument>();

        Assert.Contains(friends.Friends, friend => friend.Person.Id == AutomatedTestSeed.UnconnectedPersonId);
    }

    /// <summary>
    /// A request already waiting between the two, either way round, becomes
    /// the friendship — not a second row next to it.
    /// </summary>
    [Fact]
    public async Task ARequestTheVisitorHadSentIsAnsweredByTheLink()
    {
        var invite = await CodeAsync(Client);
        var requester = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        (await AcceptAsync(requester, invite.Code)).EnsureSuccessStatusCode();

        var rows = await RowsBetweenAsync(AutomatedTestSeed.CurrentPersonId, AutomatedTestSeed.RequestingPersonId);
        Assert.Equal(FriendshipStatus.Accepted, Assert.Single(rows).Status);
    }

    [Fact]
    public async Task ARequestTheSenderHadSentIsAnsweredByTheLink()
    {
        (await Client.PostJsonAsync($"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request", new { }))
            .EnsureSuccessStatusCode();

        var invite = await CodeAsync(Client);
        var visitor = await ClientForAsync(UnconnectedEmail);

        (await AcceptAsync(visitor, invite.Code)).EnsureSuccessStatusCode();

        var rows = await RowsBetweenAsync(AutomatedTestSeed.CurrentPersonId, AutomatedTestSeed.UnconnectedPersonId);
        Assert.Equal(FriendshipStatus.Accepted, Assert.Single(rows).Status);
    }

    /// <summary>
    /// Tapped twice, or forwarded to somebody who was already in: what they
    /// wanted is true either way, so it is not an error.
    /// </summary>
    [Fact]
    public async Task AcceptingAsAnExistingFriendChangesNothing()
    {
        var invite = await CodeAsync(Client);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await AcceptAsync(friend, invite.Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await RowsBetweenAsync(AutomatedTestSeed.CurrentPersonId, AutomatedTestSeed.FriendPersonId));
    }

    [Fact]
    public async Task YourOwnLinkCannotBeAccepted()
    {
        var invite = await CodeAsync(Client);

        var response = await AcceptAsync(Client, invite.Code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A blocked pair sees the same answer as a code that means nothing: a page
    /// that read differently for them would be announcing the block.
    /// </summary>
    [Fact]
    public async Task ABlockMakesTheLinkMeanNothingToTheOtherSide()
    {
        var invite = await CodeAsync(Client);

        (await Client.PostJsonAsync($"/api/blocks/{AutomatedTestSeed.UnconnectedPersonId}", new { }))
            .EnsureSuccessStatusCode();

        var blocked = await ClientForAsync(UnconnectedEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await PreviewAsync(blocked, invite.Code)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(blocked, invite.Code)).StatusCode);
        Assert.Empty(await RowsBetweenAsync(AutomatedTestSeed.CurrentPersonId, AutomatedTestSeed.UnconnectedPersonId));
    }

    [Fact]
    public async Task AcceptingNeedsASession()
    {
        var invite = await CodeAsync(Client);

        var response = await AcceptAsync(AnonymousClient, invite.Code);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACodeNeedsASession()
    {
        var response = await AnonymousClient.GetAsync("/api/invite", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
