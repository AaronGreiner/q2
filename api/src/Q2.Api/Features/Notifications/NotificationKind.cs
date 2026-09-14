namespace Q2.Api.Features.Notifications;

/// <summary>
/// What a notification is about.
/// </summary>
/// <remarks>
/// One list for every way q2 tells somebody something. The bell, the live
/// update and the push all switch on it, which is what stops them from becoming
/// three products with three vocabularies
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
///
/// What each kind *means* downstream — whether the bell keeps it, which switch
/// governs its push, which part of an open app it changes — is decided once, in
/// <see cref="NotificationRules"/>, and nowhere else.
///
/// Stored as text, like every enum in q2, so a row stays readable and adding a
/// member cannot change what an existing one says.
/// </remarks>
public enum NotificationKind
{
    /// <summary>
    /// Somebody wrote in a conversation. <c>Subject</c> is a group's name,
    /// <c>Excerpt</c> the start of the message.
    /// </summary>
    MessageReceived,

    /// <summary>Somebody asked to be friends. The actor is who asked.</summary>
    FriendRequestReceived,

    /// <summary>
    /// A friendship began because of the other person: they accepted, they
    /// asked back, or they arrived through your invite link.
    /// </summary>
    FriendshipStarted,

    /// <summary>
    /// A friend delivered a photograph and it is waiting for your verdict.
    /// <c>Subject</c> is the goal.
    /// </summary>
    ProofAwaitingVote,

    /// <summary>
    /// Your photograph was believed. <c>Subject</c> is the goal. Never an
    /// actor: whose vote settled it is not something q2 says.
    /// </summary>
    ProofConfirmed,

    /// <summary>
    /// Your photograph was not believed. <c>Amount</c> is how many attempts
    /// are left — one or none. Never an actor: doubt is anonymous
    /// ([0018](../../../../docs/adr/0018-proof-and-vote.md)).
    /// </summary>
    ProofRefused,

    /// <summary>
    /// Somebody reacted to something of yours, or gave it kudos. The
    /// <see cref="NotificationTarget"/> says to what.
    /// </summary>
    ReactionReceived,

    /// <summary>You were put on a goal — which also makes you one of its voters.</summary>
    GoalInvitation,

    /// <summary>A goal you are on was set aside. <c>Amount</c> is for how many days.</summary>
    GoalPaused,

    /// <summary>
    /// Objections ended your pause. Never an actor: objecting is anonymous
    /// ([0020](../../../../docs/adr/0020-pause-and-archive.md)).
    /// </summary>
    PauseLifted,

    /// <summary>
    /// A friend is about to miss a window. <c>Subject</c> is the goal and
    /// <c>Amount</c> how many proofs are still missing.
    /// </summary>
    FriendWindowAtRisk,

    /// <summary>Today's challenge is up. <c>Subject</c> is the prompt.</summary>
    ChallengePublished,
}

/// <summary>What a notification leads to when it is tapped.</summary>
/// <remarks>
/// The client turns this and an id into a route; the server never sends one,
/// for the same reason it never sends a sentence — a route is the client's
/// vocabulary, and a native build would spell it differently.
/// </remarks>
public enum NotificationTarget
{
    /// <summary>Nothing more specific than the bell itself.</summary>
    None,

    /// <summary>A person's profile.</summary>
    Person,

    /// <summary>A goal, and everything on its screen: the photographs, the pause.</summary>
    Goal,

    /// <summary>A conversation.</summary>
    Conversation,

    /// <summary>A day's challenge.</summary>
    Challenge,

    /// <summary>A line of your own history, which is where kudos land.</summary>
    Activity,
}

/// <summary>
/// The switches on the notification screen: one per thing a person can decide
/// they want to be interrupted for.
/// </summary>
/// <remarks>
/// Fewer than there are kinds, on purpose. "Freundschaften" covers a request and
/// an acceptance, "Ergebnisse" both verdicts: a switch per internal kind would
/// be asking people to learn q2's data model in order to be left alone.
/// </remarks>
public enum NotificationSwitch
{
    Messages,
    Friendships,
    VotesDue,
    ProofResults,
    Reactions,
    GoalUpdates,
    FriendsAtRisk,
    Challenge,
}
