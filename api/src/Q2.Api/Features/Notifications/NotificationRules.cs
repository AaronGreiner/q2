namespace Q2.Api.Features.Notifications;

/// <summary>
/// The questions every notification answers, each in exactly one place.
/// </summary>
/// <remarks>
/// Pure, like <see cref="QuietHours"/>: no database and no clock, so every rule
/// is tested on its own. Switch expressions rather than lookup tables, and none
/// of them falls through to a quiet default — a new <see cref="NotificationKind"/>
/// that nobody made a decision about throws, and the unit test that walks every
/// member finds it before a person does.
/// </remarks>
public static class NotificationRules
{
    /// <summary>Whether the bell keeps a line for this kind.</summary>
    /// <remarks>
    /// **One place per event, and one count.** Four kinds already have a home
    /// with a number of its own — a message in the chat list, a request on the
    /// search screen, a vote on the start screen's banner, the challenge on its
    /// banner — and a second row in the bell would be a second thing to clear
    /// for the same event. Everything else had no home at all until the bell
    /// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
    /// </remarks>
    public static bool IsKept(NotificationKind kind) => kind switch
    {
        NotificationKind.MessageReceived => false,
        NotificationKind.FriendRequestReceived => false,
        NotificationKind.ProofAwaitingVote => false,
        NotificationKind.ChallengePublished => false,

        NotificationKind.FriendshipStarted => true,
        NotificationKind.ProofConfirmed => true,
        NotificationKind.ProofRefused => true,
        NotificationKind.ReactionReceived => true,
        NotificationKind.GoalInvitation => true,
        NotificationKind.GoalPaused => true,
        NotificationKind.PauseLifted => true,
        NotificationKind.FriendWindowAtRisk => true,

        _ => throw Undecided(kind),
    };

    /// <summary>Which switch decides whether this kind may reach a device.</summary>
    public static NotificationSwitch SwitchFor(NotificationKind kind) => kind switch
    {
        NotificationKind.MessageReceived => NotificationSwitch.Messages,
        NotificationKind.FriendRequestReceived => NotificationSwitch.Friendships,
        NotificationKind.FriendshipStarted => NotificationSwitch.Friendships,
        NotificationKind.ProofAwaitingVote => NotificationSwitch.VotesDue,
        NotificationKind.ProofConfirmed => NotificationSwitch.ProofResults,
        NotificationKind.ProofRefused => NotificationSwitch.ProofResults,
        NotificationKind.ReactionReceived => NotificationSwitch.Reactions,
        NotificationKind.GoalInvitation => NotificationSwitch.GoalUpdates,
        NotificationKind.GoalPaused => NotificationSwitch.GoalUpdates,
        NotificationKind.PauseLifted => NotificationSwitch.GoalUpdates,
        NotificationKind.FriendWindowAtRisk => NotificationSwitch.FriendsAtRisk,
        NotificationKind.ChallengePublished => NotificationSwitch.Challenge,

        _ => throw Undecided(kind),
    };

    /// <summary>Which part of an open app this kind changes.</summary>
    /// <remarks>
    /// A reaction changes whatever it was on. A warning about a friend changes
    /// nothing but the bell, which the client reads again whenever the count of
    /// what is new moves.
    /// </remarks>
    public static LiveArea AreaFor(NotificationKind kind, NotificationTarget target) => kind switch
    {
        NotificationKind.MessageReceived => LiveArea.Chats,
        NotificationKind.FriendRequestReceived => LiveArea.Friends,
        NotificationKind.FriendshipStarted => LiveArea.Friends,
        NotificationKind.ProofAwaitingVote => LiveArea.Proofs,
        NotificationKind.ProofConfirmed => LiveArea.Goals,
        NotificationKind.ProofRefused => LiveArea.Goals,
        NotificationKind.GoalInvitation => LiveArea.Goals,
        NotificationKind.GoalPaused => LiveArea.Goals,
        NotificationKind.PauseLifted => LiveArea.Goals,
        NotificationKind.FriendWindowAtRisk => LiveArea.Notifications,
        NotificationKind.ChallengePublished => LiveArea.Challenge,

        NotificationKind.ReactionReceived => target switch
        {
            NotificationTarget.Conversation => LiveArea.Chats,
            NotificationTarget.Goal => LiveArea.Goals,
            NotificationTarget.Challenge => LiveArea.Challenge,
            NotificationTarget.Activity => LiveArea.Feed,
            _ => LiveArea.Notifications,
        },

        _ => throw Undecided(kind),
    };

    /// <summary>Whether, and how, this kind may interrupt this person now.</summary>
    /// <remarks>
    /// Everything that can stop an interruption, as one rule:
    ///
    /// 1. A conversation they have muted.
    /// 2. Their switch for this kind.
    /// 3. Their quiet hours, in their own zone — which drop rather than hold
    ///    ([0023](../../../../docs/adr/0023-web-push.md)).
    ///
    /// Whatever is left reaches them one way and never two. **Somebody who is
    /// looking gets a banner in the app instead of a buzz**: a phone vibrating
    /// beside the laptop that has just shown it is the same thing said twice.
    /// Somebody who is not looking gets the push. A banner is therefore exactly
    /// the push somebody would have had if they had put the app away — the same
    /// switch, the same quiet hours, the same mute
    /// ([0025](../../../../docs/adr/0025-banners-in-the-open-app.md)).
    ///
    /// The bell and the live update are deliberately not on this list. A switch
    /// decides what may *interrupt* somebody, not what they are allowed to find
    /// when they look — the message switch never removed a message from a chat
    /// either.
    /// </remarks>
    public static Interruption InterruptionFor(
        NotificationPreferences preferences,
        NotificationKind kind,
        TimeOnly localTime,
        bool isWatching,
        bool isMuted)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (isMuted
            || !preferences.Allows(SwitchFor(kind))
            || QuietHours.Covers(preferences.QuietHoursFrom, preferences.QuietHoursTo, localTime))
        {
            return Interruption.None;
        }

        return isWatching ? Interruption.Banner : Interruption.Push;
    }

    private static ArgumentOutOfRangeException Undecided(NotificationKind kind) =>
        new(nameof(kind), kind, "Every kind of notification needs a decision here.");
}

/// <summary>The one way a notification may interrupt somebody, if any.</summary>
/// <remarks>
/// One value per person, decided once by
/// <see cref="NotificationRules.InterruptionFor"/> — never a banner and a push
/// for the same event.
/// </remarks>
public enum Interruption
{
    /// <summary>Nothing interrupts them. The bell, the badges and an open screen still move.</summary>
    None,

    /// <summary>A banner at the top of the app they are looking at.</summary>
    Banner,

    /// <summary>A push to their devices.</summary>
    Push,
}
