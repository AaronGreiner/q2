using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Chats;

/// <summary>What a conversation is: two people, a group, or a goal's own thread.</summary>
public enum ConversationKind
{
    /// <summary>Two people. The name and avatar come from the other one.</summary>
    Direct,

    /// <summary>Three or more, with a name of its own.</summary>
    Group,

    /// <summary>
    /// A goal's own thread: its owner and everybody invited to check it. The
    /// name and the icon come from the goal.
    /// </summary>
    Goal,
}

/// <summary>
/// A chat — the place encouragement actually happens.
/// </summary>
/// <remarks>
/// A direct conversation deliberately has no <see cref="Title"/>: naming it
/// would mean storing "Jonas Weber" a second time and having it go stale the
/// day he changes his name. The name is derived from the other participant when
/// the conversation is read. A goal's conversation has no title for the same
/// reason — it is called whatever the goal is called.
///
/// Every goal that somebody else checks has exactly one conversation of its
/// own, opened with the goal and gone with it. That is where its photographs
/// arrive, where they are voted on and where its windows are seen to be kept
/// or missed; a free conversation is never about a goal
/// ([0027](../../../../docs/adr/0027-goal-conversations.md)).
/// </remarks>
public sealed class Conversation
{
    public const int MaxTitleLength = 80;
    public const int MaxIconLength = 24;

    /// <summary>
    /// Including yourself. The same bound as a goal's team, because a group
    /// chat and a group goal are the same set of people seen twice.
    /// </summary>
    public const int MaxParticipants = 20;

    private readonly List<ConversationParticipant> _participants = [];
    private readonly List<ChatMessage> _messages = [];

    // EF Core materialisation only.
    private Conversation()
    {
    }

    private Conversation(Guid id, ConversationKind kind, string? title, string? icon, Guid? goalId, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        Title = title;
        Icon = icon;
        GoalId = goalId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public ConversationKind Kind { get; private set; }

    /// <summary>The group's name. Null for a direct conversation.</summary>
    public string? Title { get; private set; }

    /// <summary>
    /// The group's avatar: one of <see cref="ConversationIcons"/>, never a
    /// free-form name. Null for a direct conversation, which draws the other
    /// person instead.
    /// </summary>
    public string? Icon { get; private set; }

    /// <summary>
    /// The goal this is the conversation of. Set for <see cref="ConversationKind.Goal"/>
    /// and for nothing else.
    /// </summary>
    public Guid? GoalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ConversationParticipant> Participants => _participants;

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public static Conversation CreateDirect(Guid id, DateTimeOffset createdAt) =>
        new(id, ConversationKind.Direct, null, null, null, createdAt);

    /// <summary>
    /// Opens the conversation of a goal. Its members are the goal's: the owner
    /// and everybody invited, added by the caller.
    /// </summary>
    /// <exception cref="DomainValidationException">There is no goal.</exception>
    public static Conversation CreateForGoal(Guid id, Guid goalId, DateTimeOffset createdAt)
    {
        if (goalId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(GoalId), "A goal's conversation needs its goal.");
        }

        return new Conversation(id, ConversationKind.Goal, null, null, goalId, createdAt);
    }

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Conversation CreateGroup(Guid id, string title, string? icon, DateTimeOffset createdAt)
    {
        var normalisedTitle = title?.Trim() ?? string.Empty;
        var normalisedIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();

        if (normalisedTitle.Length == 0)
        {
            throw new DomainValidationException(nameof(Title), "A group conversation needs a name.");
        }

        if (normalisedTitle.Length > MaxTitleLength)
        {
            throw new DomainValidationException(
                nameof(Title),
                $"A name may be at most {MaxTitleLength} characters long.");
        }

        if (normalisedIcon is not null && !ConversationIcons.IsValid(normalisedIcon))
        {
            throw new DomainValidationException(
                nameof(Icon),
                "That group icon is not one of the available icons.");
        }

        return new Conversation(id, ConversationKind.Group, normalisedTitle, normalisedIcon, null, createdAt);
    }

    /// <exception cref="DomainValidationException">The conversation is full.</exception>
    public void AddParticipant(Guid id, Guid personId, DateTimeOffset? lastReadAt = null)
    {
        if (_participants.Any(p => p.PersonId == personId))
        {
            return;
        }

        if (_participants.Count >= MaxParticipants)
        {
            throw new DomainValidationException(
                nameof(Participants),
                $"A conversation may have at most {MaxParticipants} people in it.");
        }

        _participants.Add(new ConversationParticipant(id, Id, personId, lastReadAt));
    }

    /// <summary>True when this person is in the conversation.</summary>
    public bool Includes(Guid personId) => _participants.Any(p => p.PersonId == personId);

    /// <summary>
    /// Takes somebody out of a group.
    /// </summary>
    /// <remarks>
    /// Their messages stay. A thread with somebody's replies removed is a
    /// rewritten conversation, and leaving a group is not a request to edit
    /// what everybody else remembers of it — which is also why the sender of a
    /// message is looked up by id rather than through the participant row.
    /// </remarks>
    /// <exception cref="DomainValidationException">
    /// A direct conversation or a goal's conversation cannot be left.
    /// </exception>
    public void RemoveParticipant(Guid personId)
    {
        if (Kind == ConversationKind.Direct)
        {
            throw new DomainValidationException(
                nameof(Participants),
                "A direct conversation cannot be left; it is between the two of you.");
        }

        // Who is in a goal's conversation is decided by the goal. Leaving it
        // would quietly take somebody's vote off the goal — and when the last
        // friend left, the goal would go back to confirming its own photographs.
        if (Kind == ConversationKind.Goal)
        {
            throw new DomainValidationException(
                nameof(Participants),
                "A goal's conversation cannot be left; it can be muted.");
        }

        var participant = _participants.FirstOrDefault(p => p.PersonId == personId);

        if (participant is not null)
        {
            _participants.Remove(participant);
        }
    }

    /// <exception cref="DomainValidationException">The sender is not in this conversation.</exception>
    public ChatMessage AddMessage(Guid id, Guid senderPersonId, string text, DateTimeOffset sentAt)
    {
        if (_participants.Count > 0 && _participants.All(p => p.PersonId != senderPersonId))
        {
            throw new DomainValidationException(
                nameof(Messages),
                "Only a participant can write in this conversation.");
        }

        var message = ChatMessage.Create(id, Id, senderPersonId, text, sentAt);
        _messages.Add(message);
        return message;
    }

    /// <summary>
    /// How many messages from other people <paramref name="personId"/> has not
    /// seen. Derived from their read marker, so it can never drift from the
    /// messages themselves.
    /// </summary>
    public int UnreadCountFor(Guid personId)
    {
        var participant = _participants.FirstOrDefault(p => p.PersonId == personId);
        if (participant is null)
        {
            return 0;
        }

        return _messages.Count(m =>
            m.SenderPersonId != personId
            && (participant.LastReadAt is null || m.SentAt > participant.LastReadAt));
    }

    /// <summary>Marks everything up to <paramref name="readAt"/> as seen.</summary>
    public void MarkRead(Guid personId, DateTimeOffset readAt) =>
        _participants.FirstOrDefault(p => p.PersonId == personId)?.MarkRead(readAt);

    /// <summary>Whether this person has muted the conversation.</summary>
    public bool IsMutedFor(Guid personId) =>
        _participants.FirstOrDefault(p => p.PersonId == personId)?.MutedAt is not null;

    /// <summary>
    /// Mutes the conversation for one person, or lets it ring again.
    /// </summary>
    /// <remarks>
    /// Muting stops a conversation ringing on somebody's devices and does
    /// nothing else. It still counts as unread and still rises to the top of
    /// the list: somebody who muted a lively group still wants to find what was
    /// said when they look — they only did not want to be told about every line
    /// of it as it arrived.
    /// </remarks>
    public void SetMuted(Guid personId, bool muted, DateTimeOffset now) =>
        _participants.FirstOrDefault(p => p.PersonId == personId)?.SetMuted(muted, now);
}

/// <summary>Somebody taking part in a conversation, and how far they have read.</summary>
public sealed class ConversationParticipant
{
    // EF Core materialisation only.
    private ConversationParticipant()
    {
    }

    internal ConversationParticipant(Guid id, Guid conversationId, Guid personId, DateTimeOffset? lastReadAt)
    {
        Id = id;
        ConversationId = conversationId;
        PersonId = personId;
        LastReadAt = lastReadAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid PersonId { get; private set; }

    /// <summary>When this person last opened the thread. Null means never.</summary>
    public DateTimeOffset? LastReadAt { get; private set; }

    /// <summary>
    /// When this person muted the thread, or null while it may ring.
    /// </summary>
    /// <remarks>
    /// A moment rather than a flag: it costs nothing, and "since when" is the
    /// first question anybody asks about a mute that seems to be stuck.
    /// </remarks>
    public DateTimeOffset? MutedAt { get; private set; }

    internal void SetMuted(bool muted, DateTimeOffset now) =>
        MutedAt = muted ? MutedAt ?? now : null;

    internal void MarkRead(DateTimeOffset readAt)
    {
        if (LastReadAt is null || readAt > LastReadAt)
        {
            LastReadAt = readAt;
        }
    }
}

/// <summary>
/// The avatars a group conversation can be given.
/// </summary>
/// <remarks>
/// A closed list of icon names, mirrored in the frontend's bundled icon set —
/// an icon in one list but not the other renders as nothing at all. The same
/// arrangement as <see cref="Q2.Api.Features.Goals.GoalIcons"/>, and for the
/// same reason: the client draws what the server allows, and neither gets to
/// invent a name.
/// </remarks>
public static class ConversationIcons
{
    public const string Default = "message-circle";

    public static readonly IReadOnlyList<string> All =
    [
        "message-circle",
        "sunrise",
        "book-open",
        "footprints",
        "sprout",
        "target",
        "hand-heart",
        "party-popper",
    ];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
