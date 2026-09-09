using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Deleting an account, and what it reaches.
/// </summary>
/// <remarks>
/// The one irreversible thing in q2, and the one with a legal deadline behind
/// it rather than a product argument. These tests exist because "deleted" is a
/// claim that has to be demonstrated over every table that pointed at somebody
/// — privacy.md's own wording is that deletion must be *tested*, not merely
/// documented.
///
/// The interesting cases are the two the cascades cannot express: a direct
/// conversation, which goes whole, and a group, which stays without them.
/// </remarks>
[Trait("Category", "Integration")]
public class AccountDeletionTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record DeletionDocument(
        int Goals,
        int Images,
        int Conversations,
        int Messages,
        int ChallengeEntries);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string password) =>
        client.DeleteJsonAsync("/api/auth/account", new { password });

    [Fact]
    public async Task ThePasswordIsAskedForAgain()
    {
        var wrong = await DeleteAsync(Client, "not-the-password");
        var missing = await Client.DeleteJsonAsync("/api/auth/account", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Still there, which is the point of asking.
        await Factory.WithDatabaseAsync(async database =>
            Assert.True(await database.People.AnyAsync(
                person => person.Id == AutomatedTestSeed.CurrentPersonId,
                TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ThePersonAndEverythingOfTheirsIsGone()
    {
        // A picture of their own, so the erasure has something outside the
        // database to reach as well.
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var summary = await (await DeleteAsync(Client, SeedAccounts.Password)).ReadAsync<DeletionDocument>();

        Assert.True(summary.Goals > 0);
        Assert.True(summary.Images > 0);

        await Factory.WithDatabaseAsync(async database =>
        {
            var token = TestContext.Current.CancellationToken;
            var id = AutomatedTestSeed.CurrentPersonId;

            Assert.False(await database.People.AnyAsync(person => person.Id == id, token));
            Assert.False(await database.Users.AnyAsync(user => user.PersonId == id, token));
            Assert.False(await database.Goals.AnyAsync(goal => goal.OwnerPersonId == id, token));
            Assert.False(await database.Images.AnyAsync(image => image.OwnerPersonId == id, token));
            Assert.False(await database.DailyCheckIns.AnyAsync(checkIn => checkIn.PersonId == id, token));
            Assert.False(await database.PersonBadges.AnyAsync(badge => badge.PersonId == id, token));

            // Both ends of every friendship, not only the ones they requested.
            Assert.False(await database.Friendships.AnyAsync(
                friendship => friendship.RequesterId == id || friendship.AddresseeId == id, token));
        });
    }

    /// <summary>
    /// A direct thread had two people in it and one no longer exists, so there
    /// is nothing left to keep. A group is other people's conversation too.
    /// </summary>
    [Fact]
    public async Task ADirectChatGoesWholeAndAGroupStaysWithoutThem()
    {
        await DeleteAsync(Client, SeedAccounts.Password);

        await Factory.WithDatabaseAsync(async database =>
        {
            var token = TestContext.Current.CancellationToken;

            Assert.False(await database.Conversations.AnyAsync(
                conversation => conversation.Id == AutomatedTestSeed.DirectConversationId, token));

            // The group this person was never in is untouched, and so is
            // everybody else's half of anything they were.
            Assert.True(await database.Conversations.AnyAsync(
                conversation => conversation.Id == AutomatedTestSeed.ForeignConversationId, token));

            Assert.False(await database.ChatMessages.AnyAsync(
                message => message.SenderPersonId == AutomatedTestSeed.CurrentPersonId, token));
        });
    }

    /// <summary>
    /// The counters are stored rather than derived, so a cascade would leave
    /// every person they ever cheered counting something that is gone.
    /// </summary>
    [Fact]
    public async Task TheKudosTheyGaveAreTakenBackRatherThanLeftStanding()
    {
        var before = 0;

        await Factory.WithDatabaseAsync(async database =>
            before = await database.People
                .Where(person => person.Id == AutomatedTestSeed.FriendPersonId)
                .Select(person => person.KudosReceived)
                .SingleAsync(TestContext.Current.CancellationToken));

        // The seed has this person cheering one of the friend's entries.
        await DeleteAsync(Client, SeedAccounts.Password);

        await Factory.WithDatabaseAsync(async database =>
        {
            var after = await database.People
                .Where(person => person.Id == AutomatedTestSeed.FriendPersonId)
                .Select(person => person.KudosReceived)
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(before - 1, after);
        });
    }

    [Fact]
    public async Task TheSessionEndsWithTheAccount()
    {
        (await DeleteAsync(Client, SeedAccounts.Password)).EnsureSuccessStatusCode();

        // The cookie is cleared rather than left pointing at nothing: otherwise
        // every later request fails as "the person behind this session no
        // longer exists" instead of as signed out.
        var afterwards = await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task DeletingNeedsASession()
    {
        var response = await AnonymousClient.DeleteJsonAsync(
            "/api/auth/account",
            new { password = SeedAccounts.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
