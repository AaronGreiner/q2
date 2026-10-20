using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Who can find somebody, and whether their activity shows (#106): chosen in
/// the settings, and kept by the search, the suggestions and every response
/// that carries somebody's presence.
/// </summary>
[Trait("Category", "Integration")]
public class PrivacyChoicesEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record SettingsDocument(Findability Findability, bool ShowActivity);

    private sealed record PersonDocument(Guid Id, string Handle, bool IsOnline);

    private sealed record SearchResultDocument(PersonDocument Person);

    private sealed record FriendDocument(PersonDocument Person, DateTimeOffset? LastSeenAt);

    private sealed record SuggestionDocument(PersonDocument Person);

    private sealed record FriendsDocument(IReadOnlyList<FriendDocument> Friends, IReadOnlyList<SuggestionDocument> Suggestions);

    private sealed record ThreadDocument(bool IsOnline, DateTimeOffset? OtherLastSeenAt);

    private static async Task ChooseAsync(HttpClient client, object choice) =>
        (await client.PutJsonAsync("/api/settings", choice)).EnsureSuccessStatusCode();

    private async Task<IReadOnlyList<Guid>> FoundAsync(string term) =>
        [.. (await (await Client.GetAsync($"/api/friends/search?query={Uri.EscapeDataString(term)}", TestContext.Current.CancellationToken))
                .ReadAsync<IReadOnlyList<SearchResultDocument>>())
            .Select(result => result.Person.Id)];

    [Fact]
    public async Task TheChoicesAreEverybodyAndShownUntilChangedAndThenKept()
    {
        var before = await (await Client.GetAsync("/api/settings", TestContext.Current.CancellationToken)).ReadAsync<SettingsDocument>();
        Assert.Equal(Findability.Everyone, before.Findability);
        Assert.True(before.ShowActivity);

        var after = await (await Client.PutJsonAsync("/api/settings", new { findability = "Handle", showActivity = false }))
            .ReadAsync<SettingsDocument>();
        Assert.Equal(Findability.Handle, after.Findability);
        Assert.False(after.ShowActivity);

        // One choice changed, the other kept.
        var again = await (await Client.PutJsonAsync("/api/settings", new { findability = "Link" })).ReadAsync<SettingsDocument>();
        Assert.Equal(Findability.Link, again.Findability);
        Assert.False(again.ShowActivity);
    }

    [Fact]
    public async Task AChoiceThatDoesNotExistIsRefused()
    {
        var response = await Client.PutJsonAsync("/api/settings", new { findability = "Nobody" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SomebodyFindableByHandleIsFoundByTheWholeHandleOnly()
    {
        var four = await ClientForAsync(SeedAccounts.EmailFor("@test.four"));
        Assert.Contains(SeedPeople.Four, await FoundAsync("Person Four"));

        await ChooseAsync(four, new { findability = "Handle" });

        Assert.DoesNotContain(SeedPeople.Four, await FoundAsync("Person Four"));
        Assert.DoesNotContain(SeedPeople.Four, await FoundAsync("test.fo"));
        Assert.Contains(SeedPeople.Four, await FoundAsync("@test.four"));
        Assert.Contains(SeedPeople.Four, await FoundAsync("TEST.FOUR"));
    }

    [Fact]
    public async Task SomebodyFindableByLinkOnlyIsNotFoundOrSuggestedToStrangers()
    {
        // Four is a friend of my friend: the one suggestion this world offers.
        var friends = await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken)).ReadAsync<FriendsDocument>();
        Assert.Contains(friends.Suggestions, suggestion => suggestion.Person.Id == SeedPeople.Four);

        await ChooseAsync(await ClientForAsync(SeedAccounts.EmailFor("@test.four")), new { findability = "Link" });

        Assert.DoesNotContain(SeedPeople.Four, await FoundAsync("@test.four"));
        Assert.DoesNotContain(SeedPeople.Four, await FoundAsync("Person Four"));

        var later = await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken)).ReadAsync<FriendsDocument>();
        Assert.DoesNotContain(later.Suggestions, suggestion => suggestion.Person.Id == SeedPeople.Four);
    }

    [Fact]
    public async Task AFriendOrAnOpenRequestIsFoundAsBeforeWhateverTheyChose()
    {
        // My friend, and the stranger whose request to me is open.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await ChooseAsync(friend, new { findability = "Link" });
        await ChooseAsync(await ClientForAsync(SeedAccounts.EmailFor("@test.three")), new { findability = "Link" });

        Assert.Contains(AutomatedTestSeed.FriendPersonId, await FoundAsync("Person Two"));
        Assert.Contains(SeedPeople.Three, await FoundAsync("Person Three"));
    }

    [Fact]
    public async Task SomebodyWhoHidesTheirActivityShowsNeitherOnlineNorLastSeen()
    {
        // The seeded friend was last seen a minute ago.
        var shown = (await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken)).ReadAsync<FriendsDocument>())
            .Friends.Single(entry => entry.Person.Id == AutomatedTestSeed.FriendPersonId);
        Assert.True(shown.Person.IsOnline);
        Assert.NotNull(shown.LastSeenAt);

        await ChooseAsync(await ClientForAsync(AutomatedTestSeed.FriendEmail), new { showActivity = false });

        var hidden = (await (await Client.GetAsync("/api/friends", TestContext.Current.CancellationToken)).ReadAsync<FriendsDocument>())
            .Friends.Single(entry => entry.Person.Id == AutomatedTestSeed.FriendPersonId);
        Assert.False(hidden.Person.IsOnline);
        Assert.Null(hidden.LastSeenAt);

        // The direct chat's header, too.
        var thread = await (await Client.GetAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();
        Assert.False(thread.IsOnline);
        Assert.Null(thread.OtherLastSeenAt);
    }

    /// <summary>The seeded people the tests above look for, by the ids the seed gives them.</summary>
    private static class SeedPeople
    {
        public static Guid Three => SeedIds.For(SeedProfile.AutomatedTest, SeedEntity.Person, 3);

        public static Guid Four => SeedIds.For(SeedProfile.AutomatedTest, SeedEntity.Person, 4);
    }
}
