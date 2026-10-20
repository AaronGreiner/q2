using System.Text.Json;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Whether this deployment sends notifications, and the key to subscribe with.
/// </summary>
/// <remarks>
/// <paramref name="PublicKey"/> is null when push is not configured, and the
/// settings screen says so rather than offering a switch that would do nothing.
/// A deployment without VAPID keys is a supported state, not a broken one.
/// </remarks>
public sealed record PushKeyResponse(bool IsAvailable, string? PublicKey);

/// <summary>Request body for registering a browser.</summary>
/// <remarks>
/// The three values a <c>PushSubscription</c> carries in the browser, flattened.
/// Nullable so an empty body produces field errors rather than a binding
/// failure, like every other request in this API.
/// </remarks>
public sealed record SubscribeRequest(
    string? Endpoint = null,
    string? PublicKey = null,
    string? AuthSecret = null);

/// <summary>Request body for forgetting one.</summary>
public sealed record UnsubscribeRequest(string? Endpoint = null);

/// <summary>
/// One thing that happened to you: a line in the bell, and the whole of what a
/// push carries to a device.
/// </summary>
/// <remarks>
/// One shape for both, on purpose. The service worker composes the words on a
/// lock screen with the very function the bell uses, from the very same fields,
/// so the two cannot say the same event in two different ways
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)). And it is a
/// documented response, so the worker's copy of it is generated from this
/// contract rather than written out by hand beside it.
///
/// No sentence and no route, ever: the client writes both, in the reader's
/// language and in its own vocabulary.
/// </remarks>
/// <param name="Id">The bell's row. Null in a push, which is kept nowhere.</param>
/// <param name="Actor">
/// Who caused it. Null when that is anonymous by design — a verdict, an
/// objection — and when nobody did, as with a prompt going up.
/// </param>
/// <param name="Subject">A goal's title, a prompt, or a group's name.</param>
/// <param name="Excerpt">The start of a message. Only ever in a push; the bell keeps no words.</param>
/// <param name="Amount">
/// A number the sentence needs — days, proofs, attempts. For a line of
/// reactions, how many different people reacted.
/// </param>
/// <param name="FocusId">
/// Where inside the target a tap leads, when that is narrower than the target:
/// the photograph a reaction or verdict is about, or the goal of a line of
/// your history that got kudos (#112).
/// </param>
/// <param name="IsNew">Arrived after the bell was last opened. Always true in a push.</param>
/// <param name="Detail">A code the client words: for a refused photograph, the reason most doubters gave (#111).</param>
/// <param name="Language">
/// The language the recipient chose in q2. Only ever in a push: the service
/// worker writes the sentence without the app around it and cannot ask the
/// account, and the browser's language is not the choice (#39). Null in the
/// bell and in a banner, which the open app words in the language it shows.
/// </param>
public sealed record NotificationResponse(
    Guid? Id,
    NotificationKind Kind,
    PersonSummary? Actor,
    string? Subject,
    string? Excerpt,
    int? Amount,
    NotificationTarget Target,
    Guid? TargetId,
    Guid? FocusId,
    DateTimeOffset OccurredAt,
    bool IsNew,
    LanguagePreference? Language = null,
    string? Detail = null)
{
    /// <summary>How much of a message a lock screen gets.</summary>
    public const int MaxExcerptLength = 140;

    /// <param name="language">The reader's, for a subject q2 holds in both (#105).</param>
    public static NotificationResponse ForBell(
        Notification line,
        PersonSummary? actor,
        int? amount,
        bool isNew,
        LanguagePreference language)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new NotificationResponse(
            line.Id,
            line.Kind,
            actor,
            line.SubjectIn(language),
            Excerpt: null,
            amount,
            line.Target,
            line.TargetId,
            line.FocusId,
            line.OccurredAt,
            isNew,
            Detail: line.Detail);
    }

    public static NotificationResponse ForDevice(NotificationEvent notification, PersonSummary? actor, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationResponse(
            Id: null,
            notification.Kind,
            actor,
            Notification.Shorten(notification.Subject, Notification.MaxSubjectLength),
            Notification.Shorten(notification.Excerpt, MaxExcerptLength),
            notification.Amount,
            notification.Target,
            notification.TargetId,
            notification.FocusId,
            occurredAt,
            IsNew: true,
            Detail: notification.Detail);
    }

    /// <summary>
    /// This payload for a reader of <paramref name="language"/>: q2's own words
    /// in theirs, where it has them in both (#105). Everything a person wrote
    /// stays as it was written.
    /// </summary>
    public NotificationResponse For(LanguagePreference language, string? englishSubject) =>
        language == LanguagePreference.English && Notification.Shorten(englishSubject, Notification.MaxSubjectLength) is { } english
            ? this with { Subject = english }
            : this;

    /// <summary>The bytes that are encrypted to a device — the same JSON the bell is read as.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, PushJson.Options);
}

/// <summary>Every number the app puts on a badge, in one read.</summary>
/// <param name="UnreadChats">Conversations with something unread in them.</param>
/// <param name="PendingFriendRequests">People waiting for an answer from you.</param>
/// <param name="UnseenNotifications">Lines in the bell that arrived since it was last opened.</param>
/// <param name="ProofsAwaitingVote">Friends' photographs waiting for your verdict.</param>
public sealed record CountsResponse(
    int UnreadChats,
    int PendingFriendRequests,
    int UnseenNotifications,
    int ProofsAwaitingVote);
