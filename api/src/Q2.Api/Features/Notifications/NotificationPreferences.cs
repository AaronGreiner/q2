namespace Q2.Api.Features.Notifications;

/// <summary>
/// What may interrupt somebody — a push to their devices, or a banner in the
/// app they have open — and when nothing may.
/// </summary>
/// <remarks>
/// One value rather than a row of loose booleans on <c>UserSettings</c>: the
/// switches and the quiet hours are one decision about one thing. It is a
/// record, replaced whole — which is how EF Core maps a complex type, and how
/// the screen that edits it sends it.
///
/// **Every switch is on by default.** Somebody who never opened the screen has
/// not said "tell me nothing", they have said nothing, and the defaults are
/// their preference until they change them. Quiet hours are on by default for
/// the opposite reason: a product that has to be told not to buzz at three in
/// the morning has already buzzed at three in the morning
/// ([0023](../../../../docs/adr/0023-web-push.md)).
///
/// All of this governs what may *interrupt*. The bell keeps what it keeps
/// whatever the switches say, the same way the message switch never took a
/// message out of a chat
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
/// </remarks>
public sealed record NotificationPreferences(
    bool Messages,
    bool Friendships,
    bool VotesDue,
    bool ProofResults,
    bool Reactions,
    bool GoalUpdates,
    bool FriendsAtRisk,
    bool Challenge,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo)
{
    /// <summary>What somebody who has never been asked gets.</summary>
    public static NotificationPreferences Default { get; } = new(
        Messages: true,
        Friendships: true,
        VotesDue: true,
        ProofResults: true,
        Reactions: true,
        GoalUpdates: true,
        FriendsAtRisk: true,
        Challenge: true,
        QuietHoursFrom: QuietHours.DefaultFrom,
        QuietHoursTo: QuietHours.DefaultTo);

    /// <summary>Whether this switch is on.</summary>
    public bool Allows(NotificationSwitch key) => key switch
    {
        NotificationSwitch.Messages => Messages,
        NotificationSwitch.Friendships => Friendships,
        NotificationSwitch.VotesDue => VotesDue,
        NotificationSwitch.ProofResults => ProofResults,
        NotificationSwitch.Reactions => Reactions,
        NotificationSwitch.GoalUpdates => GoalUpdates,
        NotificationSwitch.FriendsAtRisk => FriendsAtRisk,
        NotificationSwitch.Challenge => Challenge,

        // Silence for a switch nobody has heard of. Unreachable through the
        // enum, and the safe answer if a stored value ever outlives its member.
        _ => false,
    };

    /// <summary>
    /// The same preferences with a quiet window — both ends of it, or neither.
    /// </summary>
    /// <remarks>
    /// Half a window is not a window, and storing one would leave
    /// <see cref="QuietHours.Covers"/> deciding what half of one means.
    /// </remarks>
    public NotificationPreferences WithQuietHours(TimeOnly? from, TimeOnly? to) =>
        from is not null && to is not null
            ? this with { QuietHoursFrom = from, QuietHoursTo = to }
            : this with { QuietHoursFrom = null, QuietHoursTo = null };
}
