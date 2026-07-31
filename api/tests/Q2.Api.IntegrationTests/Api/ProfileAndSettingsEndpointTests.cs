using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The profile — which is also where the tab bar gets its badges — and the
/// preferences screen.
/// </summary>
[Trait("Category", "Integration")]
public class ProfileAndSettingsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record PersonDocument(Guid Id, string DisplayName, string Handle, string Initials);

    private sealed record DaySummaryDocument(int Done, int Total, int Percent);

    private sealed record BadgeDocument(BadgeKey Key, bool IsEarned, DateOnly? EarnedOn);

    private sealed record ActivityDocument(Guid Id, PersonDocument Actor);

    private sealed record ProfileDocument(
        PersonDocument Person,
        int Streak,
        int KudosReceived,
        int GoalsCompleted,
        IReadOnlyList<bool> WeekActivity,
        DaySummaryDocument Today,
        int UnreadChats,
        int PendingFriendRequests,
        IReadOnlyList<BadgeDocument> Badges,
        IReadOnlyList<ActivityDocument> RecentActivity);

    private sealed record SettingsDocument(
        ThemePreference Theme,
        LanguagePreference Language,
        bool NotifyReminders,
        bool NotifyKudos,
        bool NotifyMessages,
        bool NotifyWeeklyReview);

    private async Task<ProfileDocument> ProfileAsync() =>
        await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

    private async Task<SettingsDocument> SettingsAsync() =>
        await (await Client.GetAsync("/api/settings", TestContext.Current.CancellationToken))
            .ReadAsync<SettingsDocument>();

    [Fact]
    public async Task TheProfileIsTheSignedInPerson()
    {
        var profile = await ProfileAsync();

        Assert.Equal(AutomatedTestSeed.CurrentPersonId, profile.Person.Id);
        Assert.Equal("@test.one", profile.Person.Handle);
    }

    [Fact]
    public async Task TheStreakIsDerivedFromTheDaysTheSeedLaidDown()
    {
        Assert.Equal(3, (await ProfileAsync()).Streak);
    }

    [Fact]
    public async Task TheWeekAlwaysHasSevenEntries()
    {
        // The client never has to work out which end of the array is Monday.
        Assert.Equal(7, (await ProfileAsync()).WeekActivity.Count);
    }

    [Fact]
    public async Task TodaysProgressCountsOnlyWhatIsScheduledForToday()
    {
        var profile = await ProfileAsync();

        Assert.Equal(2, profile.Today.Total);
        Assert.Equal(1, profile.Today.Done);
        Assert.Equal(50, profile.Today.Percent);
    }

    [Fact]
    public async Task EveryBadgeIsReturnedIncludingTheUnearnedOnes()
    {
        var badges = (await ProfileAsync()).Badges;

        // A badge you cannot see is not something to aim for.
        Assert.Equal(BadgeCatalogue.InDisplayOrder, badges.Select(badge => badge.Key));
        Assert.Contains(badges, badge => badge.IsEarned);
        Assert.Contains(badges, badge => !badge.IsEarned);
    }

    [Fact]
    public async Task RecentActivityIsYourOwnRatherThanYourFriends()
    {
        var profile = await ProfileAsync();

        Assert.NotEmpty(profile.RecentActivity);
        Assert.All(profile.RecentActivity, entry => Assert.Equal(profile.Person.Id, entry.Actor.Id));
    }

    [Fact]
    public async Task TheBadgeCountsAreOnTheProfileSoTheTabBarNeedsNoSecondRequest()
    {
        var profile = await ProfileAsync();

        Assert.Equal(1, profile.UnreadChats);
        Assert.Equal(1, profile.PendingFriendRequests);
    }

    [Fact]
    public async Task ReadingAChatClearsItsBadge()
    {
        await Client.GetAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(0, (await ProfileAsync()).UnreadChats);
    }

    [Fact]
    public async Task SettingsExistOnFirstReadRatherThanFailing()
    {
        var settings = await SettingsAsync();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(LanguagePreference.German, settings.Language);
    }

    [Fact]
    public async Task UpdatingSettingsKeepsWhatWasNotSent()
    {
        var response = await Client.PutJsonAsync("/api/settings", new { theme = "Dark" });

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<SettingsDocument>();

        Assert.Equal(ThemePreference.Dark, updated.Theme);

        // A client that only knows about four switches must not silently reset
        // the fifth.
        Assert.True(updated.NotifyReminders);
        Assert.Equal(LanguagePreference.German, updated.Language);
    }

    [Fact]
    public async Task ASettingSurvivesTheNextRead()
    {
        await Client.PutJsonAsync("/api/settings", new { language = "English", notifyWeeklyReview = true });

        var settings = await SettingsAsync();

        Assert.Equal(LanguagePreference.English, settings.Language);
        Assert.True(settings.NotifyWeeklyReview);
    }
}
