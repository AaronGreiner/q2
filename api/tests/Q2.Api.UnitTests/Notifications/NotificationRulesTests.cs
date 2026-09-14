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
    public void AnOrdinaryPushGoesOut() =>
        Assert.True(Push(NotificationPreferences.Default, NotificationKind.MessageReceived));

    [Fact]
    public void SomebodyWhoIsLookingIsNotAlsoBuzzed() =>
        Assert.False(Push(NotificationPreferences.Default, NotificationKind.MessageReceived, isWatching: true));

    [Fact]
    public void AMutedConversationDoesNotRing() =>
        Assert.False(Push(NotificationPreferences.Default, NotificationKind.MessageReceived, isMuted: true));

    [Fact]
    public void TheSwitchForThatKindDecidesAndNoOther()
    {
        var noReactions = NotificationPreferences.Default with { Reactions = false };

        Assert.False(Push(noReactions, NotificationKind.ReactionReceived));
        Assert.True(Push(noReactions, NotificationKind.MessageReceived));
    }

    [Fact]
    public void QuietHoursDropAPushInThePersonsOwnNight()
    {
        Assert.False(Push(NotificationPreferences.Default, NotificationKind.FriendshipStarted, new TimeOnly(23, 30)));
        Assert.True(Push(NotificationPreferences.Default with { QuietHoursFrom = null, QuietHoursTo = null }, NotificationKind.FriendshipStarted, new TimeOnly(23, 30)));
    }

    private static bool Push(
        NotificationPreferences preferences,
        NotificationKind kind,
        TimeOnly? at = null,
        bool isWatching = false,
        bool isMuted = false) =>
        NotificationRules.ShouldPush(preferences, kind, at ?? Noon, isWatching, isMuted);
}
