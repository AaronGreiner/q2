using Q2.Api.Features.Notifications;

namespace Q2.Api.UnitTests.Notifications;

/// <summary>
/// The decisions every notification is put through, one rule at a time.
/// </summary>
/// <remarks>
/// None of the rules falls through to a quiet default, so the first test here
/// is the one that notices a new <see cref="NotificationKind"/> nobody made a
/// decision about — before a person does.
/// </remarks>
[Trait("Category", "Unit")]
public class NotificationRulesTests
{
    private static readonly TimeOnly Noon = new(12, 0);

    public static TheoryData<NotificationKind> EveryKind => [.. Enum.GetValues<NotificationKind>()];

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void EveryKindHasADecisionInEveryRule(NotificationKind kind)
    {
        var exception = Record.Exception(() =>
        {
            _ = NotificationRules.IsKept(kind);
            _ = NotificationRules.SwitchFor(kind);
            _ = NotificationRules.AreaFor(kind, NotificationTarget.None);
        });

        Assert.Null(exception);
    }

    /// <summary>One place per event, and one count.</summary>
    [Theory]
    [InlineData(NotificationKind.MessageReceived)]
    [InlineData(NotificationKind.FriendRequestReceived)]
    [InlineData(NotificationKind.ProofAwaitingVote)]
    [InlineData(NotificationKind.ChallengePublished)]
    public void WhatAlreadyHasAPlaceWithItsOwnCountIsNotKeptInTheBell(NotificationKind kind) =>
        Assert.False(NotificationRules.IsKept(kind));

    [Theory]
    [InlineData(NotificationKind.FriendshipStarted)]
    [InlineData(NotificationKind.ProofConfirmed)]
    [InlineData(NotificationKind.ProofRefused)]
    [InlineData(NotificationKind.ReactionReceived)]
    [InlineData(NotificationKind.GoalInvitation)]
    [InlineData(NotificationKind.GoalPaused)]
    [InlineData(NotificationKind.PauseLifted)]
    [InlineData(NotificationKind.FriendWindowAtRisk)]
    public void EverythingThatHadNoHomeBeforeIsKept(NotificationKind kind) =>
        Assert.True(NotificationRules.IsKept(kind));

    [Fact]
    public void EverySwitchOnTheScreenGovernsSomething()
    {
        var governed = Enum.GetValues<NotificationKind>().Select(NotificationRules.SwitchFor).ToHashSet();

        // A switch with nothing behind it is exactly what this rework removed.
        Assert.All(Enum.GetValues<NotificationSwitch>(), key => Assert.Contains(key, governed));
    }

    [Fact]
    public void OneSwitchCoversBothHalvesOfAFriendshipAndBothVerdicts()
    {
        Assert.Equal(
            NotificationRules.SwitchFor(NotificationKind.FriendRequestReceived),
            NotificationRules.SwitchFor(NotificationKind.FriendshipStarted));

        Assert.Equal(
            NotificationRules.SwitchFor(NotificationKind.ProofConfirmed),
            NotificationRules.SwitchFor(NotificationKind.ProofRefused));
    }

    [Theory]
    [InlineData(NotificationTarget.Conversation, LiveArea.Chats)]
    [InlineData(NotificationTarget.Goal, LiveArea.Goals)]
    [InlineData(NotificationTarget.Challenge, LiveArea.Challenge)]
    [InlineData(NotificationTarget.Activity, LiveArea.Feed)]
    [InlineData(NotificationTarget.None, LiveArea.Notifications)]
    public void AReactionChangesWhateverItWasOn(NotificationTarget target, LiveArea area) =>
        Assert.Equal(area, NotificationRules.AreaFor(NotificationKind.ReactionReceived, target));

    [Fact]
    public void SomebodyWhoIsNotLookingIsPushed() =>
        Assert.Equal(Interruption.Push, Decide(NotificationPreferences.Default, NotificationKind.MessageReceived));

    /// <summary>
    /// An open app already shows it, and a phone vibrating beside the laptop
    /// that has just shown it is the same thing said twice.
    /// </summary>
    [Fact]
    public void SomebodyWhoIsLookingGetsABannerInsteadOfABuzz() =>
        Assert.Equal(
            Interruption.Banner,
            Decide(NotificationPreferences.Default, NotificationKind.MessageReceived, isWatching: true));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMutedConversationInterruptsNobody(bool isWatching) =>
        Assert.Equal(
            Interruption.None,
            Decide(NotificationPreferences.Default, NotificationKind.MessageReceived, isWatching: isWatching, isMuted: true));

    [Theory]
    [InlineData(false, Interruption.Push)]
    [InlineData(true, Interruption.Banner)]
    public void TheSwitchForThatKindDecidesAndNoOther(bool isWatching, Interruption allowed)
    {
        var noReactions = NotificationPreferences.Default with { Reactions = false };

        Assert.Equal(Interruption.None, Decide(noReactions, NotificationKind.ReactionReceived, isWatching: isWatching));
        Assert.Equal(allowed, Decide(noReactions, NotificationKind.MessageReceived, isWatching: isWatching));
    }

    [Theory]
    [InlineData(false, Interruption.Push)]
    [InlineData(true, Interruption.Banner)]
    public void QuietHoursKeepThePersonsOwnNightQuiet(bool isWatching, Interruption withoutThem)
    {
        var lateAtNight = new TimeOnly(23, 30);
        var noQuietHours = NotificationPreferences.Default with { QuietHoursFrom = null, QuietHoursTo = null };

        Assert.Equal(
            Interruption.None,
            Decide(NotificationPreferences.Default, NotificationKind.FriendshipStarted, lateAtNight, isWatching));
        Assert.Equal(withoutThem, Decide(noQuietHours, NotificationKind.FriendshipStarted, lateAtNight, isWatching));
    }

    /// <summary>
    /// A banner is the push somebody would have had if they had put the app
    /// away: for every kind and everything that can stop one, the one is allowed
    /// exactly when the other would have been — and never both.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryKind))]
    public void ABannerIsThePushForSomebodyWhoIsLooking(NotificationKind kind)
    {
        var everythingOff = new NotificationPreferences(
            Messages: false,
            Friendships: false,
            VotesDue: false,
            ProofResults: false,
            Reactions: false,
            GoalUpdates: false,
            FriendsAtRisk: false,
            Challenge: false,
            QuietHoursFrom: null,
            QuietHoursTo: null);

        foreach (var preferences in new[] { NotificationPreferences.Default, everythingOff })
        {
            foreach (var at in new[] { Noon, new TimeOnly(23, 30) })
            {
                foreach (var isMuted in new[] { false, true })
                {
                    var away = Decide(preferences, kind, at, isWatching: false, isMuted);
                    var looking = Decide(preferences, kind, at, isWatching: true, isMuted);

                    Assert.NotEqual(Interruption.Banner, away);
                    Assert.NotEqual(Interruption.Push, looking);
                    Assert.Equal(away == Interruption.Push, looking == Interruption.Banner);
                }
            }
        }
    }

    private static Interruption Decide(
        NotificationPreferences preferences,
        NotificationKind kind,
        TimeOnly? at = null,
        bool isWatching = false,
        bool isMuted = false) =>
        NotificationRules.InterruptionFor(preferences, kind, at ?? Noon, isWatching, isMuted);
}
