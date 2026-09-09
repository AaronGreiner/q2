using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Getting away from somebody.
/// </summary>
/// <remarks>
/// A block is either enforced in every read that can put a person on a screen
/// or it is decoration, so most of these tests are not about the block endpoint
/// at all — they are about search, profiles, requests and chats behaving as
/// though the other person were not there.
///
/// The second thing they check is that it is never announced. Everything the
/// blocked person tries answers the way it would for a stranger: 404, not 403,
/// and never a sentence that reads differently from the one anybody else gets.
/// </remarks>
[Trait("Category", "Integration")]
public class BlockEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record PersonDocument(Guid Id, string DisplayName, string Handle);

    private sealed record ChatDocument(Guid Id, string Name);

    private static async Task<IReadOnlyList<PersonDocument>> BlockAsync(HttpClient client, Guid personId) =>
        await (await client.PostJsonAsync($"/api/blocks/{personId}", new { }))
            .ReadAsync<IReadOnlyList<PersonDocument>>();

    private static async Task<IReadOnlyList<PersonDocument>> ListAsync(HttpClient client) =>
        await (await client.GetAsync("/api/blocks", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<PersonDocument>>();

    private static async Task<IReadOnlyList<PersonDocument>> SearchAsync(HttpClient client, string term) =>
        await (await client.GetAsync($"/api/friends/search?query={term}", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SearchRow>>() is var rows
            ? [.. rows.Select(row => row.Person)]
            : [];

    private sealed record SearchRow(PersonDocument Person, string State, int MutualFriends);

    [Fact]
    public async Task BlockingEndsTheFriendship()
    {
        var blocked = await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        Assert.Equal(AutomatedTestSeed.FriendPersonId, Assert.Single(blocked).Id);

        /*
         * Not "set to blocked": the row between the two of them is gone. A
         * friendship that survives a block is one that reappears the moment
         * anything forgets to check.
         *
         * Only the row between them — the friend's own other friendships are
         * none of this action's business.
         */
        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Friendships.AnyAsync(
                friendship =>
                    (friendship.RequesterId == AutomatedTestSeed.CurrentPersonId
                        && friendship.AddresseeId == AutomatedTestSeed.FriendPersonId)
                    || (friendship.RequesterId == AutomatedTestSeed.FriendPersonId
                        && friendship.AddresseeId == AutomatedTestSeed.CurrentPersonId),
                TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ABlockedPersonIsGoneFromSearchInBothDirections()
    {
        Assert.Contains(await SearchAsync(Client, "Test Person Two"), person => person.Id == AutomatedTestSeed.FriendPersonId);

        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        Assert.DoesNotContain(await SearchAsync(Client, "Test Person Two"), person => person.Id == AutomatedTestSeed.FriendPersonId);

        // And the other way: the person blocked stops seeing the one who
        // blocked them, or the protection is one-sided and worth nothing.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        Assert.DoesNotContain(await SearchAsync(friend, "Test Person One"), person => person.Id == AutomatedTestSeed.CurrentPersonId);
    }

    [Fact]
    public async Task ABlockedPersonHasNoProfile()
    {
        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        var mine = await Client.GetAsync(
            $"/api/people/{AutomatedTestSeed.FriendPersonId}",
            TestContext.Current.CancellationToken);

        var theirs = await (await ClientForAsync(AutomatedTestSeed.FriendEmail)).GetAsync(
            $"/api/people/{AutomatedTestSeed.CurrentPersonId}",
            TestContext.Current.CancellationToken);

        // 404 rather than 403 in both directions: a refusal that says why is a
        // notification.
        Assert.Equal(HttpStatusCode.NotFound, mine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }

    [Fact]
    public async Task ABlockedPersonCannotAskToBeFriendsAgain()
    {
        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.PostJsonAsync(
            $"/api/friends/{AutomatedTestSeed.CurrentPersonId}/request",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Hidden, not destroyed. Blocking is reversible and erasing is not — and
    /// one person does not get to delete another's copy of a shared history.
    /// </summary>
    [Fact]
    public async Task ADirectChatDisappearsAndComesBack()
    {
        var before = await (await Client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ChatDocument>>();

        Assert.Contains(before, chat => chat.Id == AutomatedTestSeed.DirectConversationId);

        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        var during = await (await Client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ChatDocument>>();
        var opened = await Client.GetAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}",
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(during, chat => chat.Id == AutomatedTestSeed.DirectConversationId);
        Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);

        (await Client.DeleteAsync(
            $"/api/blocks/{AutomatedTestSeed.FriendPersonId}",
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var after = await (await Client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ChatDocument>>();

        Assert.Contains(after, chat => chat.Id == AutomatedTestSeed.DirectConversationId);
    }

    [Fact]
    public async Task OnlyThePersonWhoSetItCanLiftIt()
    {
        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.DeleteAsync(
            $"/api/blocks/{AutomatedTestSeed.CurrentPersonId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Still blocked: the person who was blocked cannot unblock themselves.
        Assert.Single(await ListAsync(Client));
    }

    [Fact]
    public async Task BlockingTwiceChangesNothing()
    {
        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);
        var second = await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        Assert.Single(second);
    }

    /// <summary>
    /// Only who you blocked, never who blocked you. The second list would be
    /// the announcement this whole feature is built to avoid.
    /// </summary>
    [Fact]
    public async Task TheListShowsOnlyYourOwnBlocks()
    {
        await BlockAsync(Client, AutomatedTestSeed.FriendPersonId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        Assert.Empty(await ListAsync(friend));
    }

    [Fact]
    public async Task YouCannotBlockYourself()
    {
        var response = await Client.PostJsonAsync($"/api/blocks/{AutomatedTestSeed.CurrentPersonId}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NoneOfItIsReachableWithoutASession()
    {
        var listed = await AnonymousClient.GetAsync("/api/blocks", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, listed.StatusCode);
    }
}
