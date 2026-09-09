using System.Net;
using Q2.Api.Features.Activity;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The feed, the kudos on it, and the friends screen.
/// </summary>
[Trait("Category", "Integration")]
public class SocialEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record PersonDocument(Guid Id, string DisplayName, string Handle, bool IsOnline);

    private sealed record ActivityDocument(
        Guid Id,
        PersonDocument Actor,
        ActivityKind Kind,
        string? Subject,
        int? Amount,
        int KudosCount,
        bool HasMyKudos,
        DateTimeOffset OccurredAt);

    private sealed record FriendDocument(PersonDocument Person, int Streak, DateTimeOffset? LastSeenAt);

    private sealed record RequestDocument(PersonDocument Person, int MutualFriends, DateTimeOffset RequestedAt);

    private sealed record SentRequestDocument(PersonDocument Person, DateTimeOffset RequestedAt);

    private sealed record SuggestionDocument(PersonDocument Person, int MutualFriends);

    private sealed record SearchResultDocument(PersonDocument Person, string State, int MutualFriends);

    private sealed record FriendsDocument(
        IReadOnlyList<FriendDocument> Friends,
        IReadOnlyList<RequestDocument> Requests,
        IReadOnlyList<SentRequestDocument> SentRequests,
        IReadOnlyList<SuggestionDocument> Suggestions);

    private async Task<IReadOnlyList<ActivityDocument>> FeedAsync() =>
        await (await Client.GetAsync("/api/feed", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ActivityDocument>>();

    private async Task<FriendsDocument> FriendsAsync(string query = "") =>
        await FriendsAsync(Client, query);

    private static async Task<FriendsDocument> FriendsAsync(HttpClient client, string query = "") =>
        await (await client.GetAsync($"/api/friends{query}", TestContext.Current.CancellationToken))
            .ReadAsync<FriendsDocument>();

    private async Task<IReadOnlyList<SearchResultDocument>> SearchAsync(string term) =>
        await (await Client.GetAsync($"/api/friends/search?query={term}", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SearchResultDocument>>();

    [Fact]
    public async Task TheFeedShowsFriendsRatherThanYourself()
    {
        var feed = await FeedAsync();

        Assert.NotEmpty(feed);
        Assert.DoesNotContain(feed, entry => entry.Actor.Id == AutomatedTestSeed.CurrentPersonId);
    }

    [Fact]
    public async Task TheFeedIsNewestFirst()
    {
        var feed = await FeedAsync();

        Assert.Equal(feed.OrderByDescending(entry => entry.OccurredAt).Select(e => e.Id), feed.Select(e => e.Id));
    }

    [Fact]
    public async Task TheFeedSendsThePartsOfASentenceRatherThanTheSentence()
    {
        // A stored sentence could only ever be German or English.
        var streak = (await FeedAsync()).Single(entry => entry.Kind == ActivityKind.StreakReached);

        Assert.Null(streak.Subject);
        Assert.NotNull(streak.Amount);
    }

    [Fact]
    public async Task GivingKudosRaisesTheCountAndMarksItAsYours()
    {
        var response = await Client.PostAsync(
            $"/api/feed/{AutomatedTestSeed.ActivityWithoutKudosId}/kudos",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<ActivityDocument>();

        Assert.Equal(6, updated.KudosCount);
        Assert.True(updated.HasMyKudos);
    }

    [Fact]
    public async Task GivingKudosTwiceTakesThemBack()
    {
        await Client.PostAsync($"/api/feed/{AutomatedTestSeed.ActivityWithoutKudosId}/kudos", null, TestContext.Current.CancellationToken);
        var second = await Client.PostAsync($"/api/feed/{AutomatedTestSeed.ActivityWithoutKudosId}/kudos", null, TestContext.Current.CancellationToken);

        var updated = await second.ReadAsync<ActivityDocument>();

        Assert.Equal(5, updated.KudosCount);
        Assert.False(updated.HasMyKudos);
    }

    [Fact]
    public async Task KudosOnSomethingThatDoesNotExistIs404()
    {
        var response = await Client.PostAsync(
            $"/api/feed/{Guid.CreateVersion7()}/kudos",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// There is no ranking endpoint, and its absence is a product decision
    /// rather than an omission: q2 tells your friends when you miss, so a table
    /// that sorts everybody by how well they are doing would be a scoreboard
    /// somebody comes last on. This is the test that would notice one coming
    /// back by accident.
    /// </summary>
    [Fact]
    public async Task ThereIsNoRanking()
    {
        var response = await Client.GetAsync("/api/leaderboard", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheFriendsScreenComesBackInItsFourSections()
    {
        var friends = await FriendsAsync();

        Assert.Single(friends.Friends);
        Assert.Single(friends.Requests);
        Assert.Empty(friends.SentRequests);

        // Not connected to me, but known to my one friend.
        Assert.Single(friends.Suggestions);
    }

    [Fact]
    public async Task AcceptingARequestMakesThemAFriendForBothPeople()
    {
        var response = await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var mine = await FriendsAsync();
        Assert.Equal(2, mine.Friends.Count);
        Assert.Empty(mine.Requests);

        // The point of the whole two-sided model: it is one friendship, and
        // the person who asked can see it too.
        var theirs = await FriendsAsync(await ClientForAsync(AutomatedTestSeed.RequesterEmail));
        Assert.Contains(theirs.Friends, friend => friend.Person.Id == AutomatedTestSeed.CurrentPersonId);
        Assert.Empty(theirs.SentRequests);
    }

    [Fact]
    public async Task ARequestIsWaitingForOneSideAndSentByTheOther()
    {
        var mine = await FriendsAsync();
        var theirs = await FriendsAsync(await ClientForAsync(AutomatedTestSeed.RequesterEmail));

        Assert.Single(mine.Requests, request => request.Person.Id == AutomatedTestSeed.RequestingPersonId);
        Assert.Single(theirs.SentRequests, sent => sent.Person.Id == AutomatedTestSeed.CurrentPersonId);

        // And neither of them has it in the other list.
        Assert.Empty(mine.SentRequests);
        Assert.Empty(theirs.Requests);
    }

    [Fact]
    public async Task ARequestCannotBeAcceptedByThePersonWhoSentIt()
    {
        // Otherwise asking and being accepted would be the same action.
        var requester = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        var response = await requester.PostAsync(
            $"/api/friends/{AutomatedTestSeed.CurrentPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DecliningARequestRemovesItWithoutLeavingARecord()
    {
        var response = await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/decline",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await FriendsAsync()).Requests);

        // Storing who somebody did not want to know is not information q2 has
        // any use for.
        await Factory.WithDatabaseAsync(async database =>
        {
            Assert.DoesNotContain(
                database.Friendships,
                link => link.Involves(AutomatedTestSeed.RequestingPersonId));

            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AskingSomebodyPutsTheRequestOnBothScreens()
    {
        var response = await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("RequestSent", (await response.ReadAsync<SearchResultDocument>()).State);

        Assert.Single(
            (await FriendsAsync()).SentRequests,
            sent => sent.Person.Id == AutomatedTestSeed.UnconnectedPersonId);
    }

    [Fact]
    public async Task ARequestCanBeTakenBack()
    {
        await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request",
            content: null,
            TestContext.Current.CancellationToken);

        var response = await Client.DeleteAsync(
            $"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await FriendsAsync()).SentRequests);
    }

    [Fact]
    public async Task AskingSomebodyWhoAlreadyAskedYouAcceptsTheirRequest()
    {
        // Two people tapping "add" within the same minute is a friendship, not
        // a conflict — and certainly not two rows facing opposite ways.
        var response = await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/request",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Friends", (await response.ReadAsync<SearchResultDocument>()).State);

        var friends = await FriendsAsync();
        Assert.Equal(2, friends.Friends.Count);
        Assert.Empty(friends.Requests);
    }

    [Fact]
    public async Task AskingAFriendAgainIsRejected()
    {
        var response = await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.FriendPersonId}/request",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovingAFriendEndsItForBothOfThem()
    {
        var response = await Client.DeleteAsync(
            $"/api/friends/{AutomatedTestSeed.FriendPersonId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await FriendsAsync()).Friends);

        var theirs = await FriendsAsync(await ClientForAsync(AutomatedTestSeed.FriendEmail));
        Assert.DoesNotContain(theirs.Friends, friend => friend.Person.Id == AutomatedTestSeed.CurrentPersonId);
    }

    [Fact]
    public async Task SearchFindsPeopleByNameAndSaysWhereYouStandWithThem()
    {
        var results = await SearchAsync("Test Person");

        // Everybody but me.
        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, result => result.Person.Id == AutomatedTestSeed.CurrentPersonId);

        Assert.Equal("Friends", results.Single(r => r.Person.Id == AutomatedTestSeed.FriendPersonId).State);
        Assert.Equal(
            "RequestReceived",
            results.Single(r => r.Person.Id == AutomatedTestSeed.RequestingPersonId).State);
        Assert.Equal("None", results.Single(r => r.Person.Id == AutomatedTestSeed.UnconnectedPersonId).State);
    }

    [Fact]
    public async Task SearchAlsoMatchesAHandle()
    {
        Assert.Single(await SearchAsync("test.two"));
    }

    [Fact]
    public async Task ASearchTermTooShortToBeUsefulReturnsNothing()
    {
        // Rather than the entire directory, which is what an empty box would
        // otherwise ask for on every keystroke.
        Assert.Empty(await SearchAsync("T"));
    }

    [Fact]
    public async Task SearchDoesNotTreatAWildcardAsOne()
    {
        // "%" matching everybody would turn the search box into a directory
        // dump for anybody who typed one character.
        Assert.Empty(await SearchAsync("%%"));
    }


    [Fact]
    public async Task SearchNeverIncludesYourself()
    {
        // You are on every screen already, and "add yourself" is not an action.
        Assert.DoesNotContain(
            await SearchAsync("Test Person One"),
            result => result.Person.Id == AutomatedTestSeed.CurrentPersonId);
    }

    [Fact]
    public async Task SignedOutTheFriendsScreenAnswersNothingAtAll()
    {
        var response = await AnonymousClient.GetAsync("/api/friends", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
