namespace Q2.Api.Features.Notifications;

/// <summary>
/// Something that happened that is worth telling somebody, as data.
/// </summary>
/// <remarks>
/// The shape the activity feed already uses — a kind, a subject, an amount —
/// plus who caused it and where it leads. Never a sentence: the client writes
/// that in the reader's language (<see cref="NotificationResponse"/>).
/// </remarks>
/// <param name="ActorPersonId">
/// Who caused it, when saying so is allowed. Null for anything anonymous by
/// design — a verdict, an objection — and for things nobody did, like a prompt
/// going up.
/// </param>
/// <param name="Excerpt">
/// The start of a message. Only ever handed to a device, never kept: the message
/// is already in its conversation, and a second copy of somebody's words in a
/// notification table would be a second place to erase them from.
/// </param>
public sealed record NotificationEvent(
    NotificationKind Kind,
    Guid? ActorPersonId,
    NotificationTarget Target,
    Guid? TargetId,
    string? Subject = null,
    int? Amount = null,
    string? Excerpt = null);

/// <summary>
/// One line in somebody's bell.
/// </summary>
/// <remarks>
/// A row per recipient, because unlike the feed this is addressed: "Jonas hat
/// deine Anfrage angenommen" happened *to you*. Whether you have seen it is not
/// stored here at all — it is one instant on the person
/// (<see cref="Q2.Api.Features.People.Person.NotificationsSeenAt"/>), the way a
/// conversation's unread count comes from one read marker rather than a flag
/// per message.
///
/// Only the kinds <see cref="NotificationRules.IsKept"/> names ever become a
/// row. Kept for <see cref="KeptFor"/> and then deleted by
/// <see cref="NotificationRetentionWorker"/>: the bell is how you find out what
/// you missed, not an archive, and everything in it also lives somewhere it
/// belongs.
/// </remarks>
public sealed class Notification
{
    /// <summary>The same limit a feed entry's subject has.</summary>
    public const int MaxSubjectLength = 120;

    /// <summary>How long a line stays in the bell.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(30);

    // EF Core materialisation only.
    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid recipientPersonId,
        NotificationKind kind,
        Guid? actorPersonId,
        string? subject,
        int? amount,
        NotificationTarget target,
        Guid? targetId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        RecipientPersonId = recipientPersonId;
        Kind = kind;
        ActorPersonId = actorPersonId;
        Subject = subject;
        Amount = amount;
        Target = target;
        TargetId = targetId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Whose bell this is in.</summary>
    public Guid RecipientPersonId { get; private set; }

    public NotificationKind Kind { get; private set; }

    /// <summary>Who caused it. See <see cref="NotificationEvent.ActorPersonId"/>.</summary>
    public Guid? ActorPersonId { get; private set; }

    /// <summary>
    /// A goal's title or a prompt. User content — never logged, never sent to
    /// Sentry (docs/privacy.md).
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>A number the sentence needs: days paused, proofs missing, attempts left.</summary>
    public int? Amount { get; private set; }

    public NotificationTarget Target { get; private set; }

    /// <summary>
    /// What <see cref="Target"/> points at.
    /// </summary>
    /// <remarks>
    /// Not a foreign key, for the reason <c>ActivityEvent.SourceId</c> is not
    /// one: it points at one of several tables, and a line about a goal that
    /// was since deleted is simply a line whose link leads nowhere — the goal's
    /// own screen already answers 404 for that.
    /// </remarks>
    public Guid? TargetId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <exception cref="ArgumentException">The bell does not keep this kind.</exception>
    public static Notification Create(
        Guid id,
        Guid recipientPersonId,
        NotificationEvent notification,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // A programming error rather than a request somebody made: nothing a
        // client sends can choose the kind. Failing loudly beats a bell that
        // quietly starts duplicating the chat list.
        if (!NotificationRules.IsKept(notification.Kind))
        {
            throw new ArgumentException(
                $"{notification.Kind} is delivered but never kept in the bell.",
                nameof(notification));
        }

        return new Notification(
            id,
            recipientPersonId,
            notification.Kind,
            notification.ActorPersonId,
            Shorten(notification.Subject, MaxSubjectLength),
            notification.Amount,
            notification.Target,
            notification.TargetId,
            occurredAt);
    }

    /// <summary>
    /// Text cut to what one line can hold, ending in an ellipsis rather than
    /// in the middle of nowhere. Null for nothing to say.
    /// </summary>
    /// <remarks>
    /// Shortened rather than refused: a goal title and a prompt are already
    /// bounded where they are written, and the one thing a notification must
    /// never do is fail the change that caused it.
    /// </remarks>
    public static string? Shorten(string? text, int maxLength)
    {
        var trimmed = text?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maxLength - 1).TrimEnd(), "…");
    }
}
