using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The profile, the badges the tab bar draws, and the preferences screen.
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
        IReadOnlyList<BadgeDocument> Badges,
        IReadOnlyList<ActivityDocument> RecentActivity);

    private sealed record CountsDocument(
        int UnreadChats,
        int PendingFriendRequests,
        int UnseenNotifications,
        int ProofsAwaitingVote);

    private sealed record NotificationSettingsDocument(
        bool Messages,
        bool Friendships,
        bool VotesDue,
        bool ProofResults,
        bool Reactions,
        bool GoalUpdates,
        bool FriendsAtRisk,
        bool Challenge,
        TimeOnly? QuietHoursFrom,
        TimeOnly? QuietHoursTo);

    private sealed record SettingsDocument(
        ThemePreference Theme,
        LanguagePreference Language,
        NotificationSettingsDocument Notifications);

    private async Task<ProfileDocument> ProfileAsync() =>
        await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

    private async Task<CountsDocument> CountsAsync() =>
        await (await Client.GetAsync("/api/counts", TestContext.Current.CancellationToken))
            .ReadAsync<CountsDocument>();

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

    /// <summary>
    /// Counted in windows that cover today, not in ticked-off tasks: a window is
    /// the only unit here that can be finished, and it is the unit somebody's
    /// friends can see.
    /// </summary>
    [Fact]
    public async Task TodaysProgressCountsTheWindowsThatCoverToday()
    {
        var profile = await ProfileAsync();

        // Three of the seeded goals have a window covering today; the delivered
        // one-off is finished and no longer has one.
        Assert.Equal(3, profile.Today.Total);
        Assert.Equal(0, profile.Today.Done);
        Assert.Equal(0, profile.Today.Percent);
    }

    [Fact]
    public async Task TodaysProgressMovesWhenAWindowIsDelivered()
    {
        // The day moves when a friend believes the photograph, not when it is
        // taken. That is the whole point of the change.
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var waiting = await ProfileAsync();
        Assert.Equal(0, waiting.Today.Done);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm });

        var profile = await ProfileAsync();

        Assert.Equal(3, profile.Today.Total);
        Assert.Equal(1, profile.Today.Done);
        Assert.Equal(33, profile.Today.Percent);
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

    /// <summary>
    /// Every badge the app draws, from the one read the live connection also
    /// sends — one unread direct chat, one request, one line in the bell.
    /// </summary>
    [Fact]
    public async Task TheBadgesHaveARead()
    {
        var counts = await CountsAsync();

        Assert.Equal(1, counts.UnreadChats);
        Assert.Equal(1, counts.PendingFriendRequests);
        Assert.Equal(1, counts.UnseenNotifications);
        Assert.Equal(0, counts.ProofsAwaitingVote);
    }

    [Fact]
    public async Task ReadingAChatClearsItsBadge()
    {
        await Client.GetAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(0, (await CountsAsync()).UnreadChats);
    }

    [Fact]
    public async Task SettingsExistOnFirstReadRatherThanFailing()
    {
        var settings = await SettingsAsync();

        // Dark rather than System: dark is the design, not a preference — see
        // UserSettings.Theme.
        Assert.Equal(ThemePreference.Dark, settings.Theme);
        Assert.Equal(LanguagePreference.German, settings.Language);
    }

    /// <summary>Somebody who never opened the screen gets every switch on, and quiet nights.</summary>
    [Fact]
    public async Task EveryNotificationSwitchStartsOn()
    {
        var notifications = (await SettingsAsync()).Notifications;

        Assert.True(notifications.Messages);
        Assert.True(notifications.Friendships);
        Assert.True(notifications.VotesDue);
        Assert.True(notifications.ProofResults);
        Assert.True(notifications.Reactions);
        Assert.True(notifications.GoalUpdates);
        Assert.True(notifications.FriendsAtRisk);
        Assert.True(notifications.Challenge);
        Assert.Equal(new TimeOnly(22, 0), notifications.QuietHoursFrom);
        Assert.Equal(new TimeOnly(7, 0), notifications.QuietHoursTo);
    }

    [Fact]
    public async Task UpdatingSettingsKeepsWhatWasNotSent()
    {
        var response = await Client.PutJsonAsync("/api/settings", new { theme = "Dark" });

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<SettingsDocument>();

        Assert.Equal(ThemePreference.Dark, updated.Theme);

        // A client that only knows about some of the switches must not
        // silently reset the rest.
        Assert.True(updated.Notifications.FriendsAtRisk);
        Assert.Equal(LanguagePreference.German, updated.Language);
    }

    /// <summary>
    /// The screen sends the one switch that was tapped; the others stay where
    /// they were, which is what lets two quick taps both stick.
    /// </summary>
    [Fact]
    public async Task OneSwitchChangesAloneAndSurvivesTheNextRead()
    {
        (await Client.PutJsonAsync("/api/settings", new
        {
            language = "English",
            notifications = new { reactions = false },
        })).EnsureSuccessStatusCode();

        (await Client.PutJsonAsync("/api/settings", new
        {
            notifications = new { challenge = false },
        })).EnsureSuccessStatusCode();

        var settings = await SettingsAsync();

        Assert.Equal(LanguagePreference.English, settings.Language);
        Assert.False(settings.Notifications.Reactions);
        Assert.False(settings.Notifications.Challenge);
        Assert.True(settings.Notifications.Messages);
        Assert.Equal(new TimeOnly(22, 0), settings.Notifications.QuietHoursFrom);
    }

    [Fact]
    public async Task QuietHoursCanBeTurnedOffAndBackOnWithTheirWindow()
    {
        (await Client.PutJsonAsync("/api/settings", new
        {
            notifications = new { quietHoursEnabled = false },
        })).EnsureSuccessStatusCode();

        var off = (await SettingsAsync()).Notifications;
        Assert.Null(off.QuietHoursFrom);
        Assert.Null(off.QuietHoursTo);

        (await Client.PutJsonAsync("/api/settings", new
        {
            notifications = new { quietHoursEnabled = true, quietHoursFrom = "21:30:00" },
        })).EnsureSuccessStatusCode();

        var on = (await SettingsAsync()).Notifications;
        Assert.Equal(new TimeOnly(21, 30), on.QuietHoursFrom);
        Assert.Equal(new TimeOnly(7, 0), on.QuietHoursTo);
    }
}
