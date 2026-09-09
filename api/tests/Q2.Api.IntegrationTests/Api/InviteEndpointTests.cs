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
/// friendship — and that the code is a secret rather than a handle, because a
/// link built from something public would let anybody force a friendship on
/// anybody.
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

    [Fact]
    public async Task ThereIsNoWayToLookACodeUp()
    {
        var invite = await CodeAsync(Client);

        // An endpoint that answered "whose code is this?" would turn an
        // unguessable string into something worth guessing at.
        var probe = await Client.GetAsync(
            $"/api/invite/{invite.Code}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, probe.StatusCode);
    }

    [Fact]
    public async Task ACodeNeedsASession()
    {
        var response = await AnonymousClient.GetAsync("/api/invite", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
