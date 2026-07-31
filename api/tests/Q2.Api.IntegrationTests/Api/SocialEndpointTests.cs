using System.Net;
using Q2.Api.Features.Activity;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The feed, the kudos on it, the leaderboard, and the friends screen.
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

    private sealed record LeaderboardDocument(int Rank, PersonDocument Person, int Kudos, bool IsMe);

    private sealed record FriendDocument(PersonDocument Person, int Streak, DateTimeOffset? LastSeenAt);

    private sealed record RequestDocument(Guid Id, PersonDocument Person, int MutualFriends);

    private sealed record SuggestionDocument(Guid Id, PersonDocument Person, int MutualFriends, bool IsInvited);

    private sealed record FriendsDocument(
        IReadOnlyList<FriendDocument> Friends,
        IReadOnlyList<RequestDocument> Requests,
        IReadOnlyList<SuggestionDocument> Suggestions);

    private async Task<IReadOnlyList<ActivityDocument>> FeedAsync() =>
        await (await Client.GetAsync("/api/feed", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ActivityDocument>>();

    private async Task<FriendsDocument> FriendsAsync(string query = "") =>
        await (await Client.GetAsync($"/api/friends{query}", TestContext.Current.CancellationToken))
            .ReadAsync<FriendsDocument>();

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
    public async Task GivingKudosMovesTheReceiverUpTheLeaderboard()
    {
        var before = await (await Client.GetAsync("/api/leaderboard", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<LeaderboardDocument>>();

        await Client.PostAsync($"/api/feed/{AutomatedTestSeed.ActivityWithoutKudosId}/kudos", null, TestContext.Current.CancellationToken);

        var after = await (await Client.GetAsync("/api/leaderboard", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<LeaderboardDocument>>();

        var friendBefore = before.Single(entry => entry.Person.Id == AutomatedTestSeed.FriendPersonId);
        var friendAfter = after.Single(entry => entry.Person.Id == AutomatedTestSeed.FriendPersonId);

        Assert.Equal(friendBefore.Kudos + 1, friendAfter.Kudos);
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

    [Fact]
    public async Task TheLeaderboardRanksYouAndYourFriendsAndSaysWhichOneIsYou()
    {
        var board = await (await Client.GetAsync("/api/leaderboard", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<LeaderboardDocument>>();

        Assert.Equal([1, 2], board.Select(entry => entry.Rank));
        Assert.Single(board, entry => entry.IsMe);

        // Somebody who only sent a request is not a friend, and not on it.
        Assert.DoesNotContain(board, entry => entry.Person.DisplayName == "Test Person Three");
    }

    [Fact]
    public async Task TheFriendsScreenComesBackInThreeSections()
    {
        var friends = await FriendsAsync();

        Assert.Single(friends.Friends);
        Assert.Single(friends.Requests);
        Assert.Single(friends.Suggestions);
    }

    [Fact]
    public async Task AcceptingARequestMakesThemAFriend()
    {
        var response = await Client.PostAsync(
            $"/api/friends/requests/{AutomatedTestSeed.PendingRequestId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var friends = await FriendsAsync();
        Assert.Equal(2, friends.Friends.Count);
        Assert.Empty(friends.Requests);
    }

    [Fact]
    public async Task DecliningARequestRemovesItWithoutLeavingARecord()
    {
        var response = await Client.PostAsync(
            $"/api/friends/requests/{AutomatedTestSeed.PendingRequestId}/decline",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var friends = await FriendsAsync();
        Assert.Empty(friends.Requests);

        // Storing who somebody did not want to know is not information q2 has
        // any use for.
        await Factory.WithDatabaseAsync(async database =>
        {
            Assert.DoesNotContain(database.Friendships, link => link.Id == AutomatedTestSeed.PendingRequestId);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ASuggestionCannotBeAcceptedOnSomebodyElsesBehalf()
    {
        var response = await Client.PostAsync(
            $"/api/friends/requests/{AutomatedTestSeed.SuggestionId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AskingASuggestedPersonMarksThemAsInvited()
    {
        var response = await Client.PostAsync(
            $"/api/friends/suggestions/{AutomatedTestSeed.SuggestionId}/request",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.True((await response.ReadAsync<SuggestionDocument>()).IsInvited);

        // Still on the screen, as "Angefragt" — a row that vanished on tap
        // would leave people wondering whether it worked.
        Assert.Single((await FriendsAsync()).Suggestions, suggestion => suggestion.IsInvited);
    }

    [Fact]
    public async Task SearchingFiltersFriendsButNeverHidesARequest()
    {
        var friends = await FriendsAsync("?search=nobody-by-this-name");

        Assert.Empty(friends.Friends);
        Assert.Empty(friends.Suggestions);

        // Hiding a pending request behind a search box is how it stays
        // unanswered forever.
        Assert.Single(friends.Requests);
    }

    [Fact]
    public async Task SearchingByNameFindsTheFriend()
    {
        var friends = await FriendsAsync("?search=person two");

        Assert.Single(friends.Friends);
        Assert.Equal("Test Person Two", friends.Friends[0].Person.DisplayName);
    }
}
