using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Chats;

/// <summary>Whether a conversation is between two people or among a group.</summary>
public enum ConversationKind
{
    /// <summary>Two people. The name and avatar come from the other one.</summary>
    Direct,

    /// <summary>Three or more, with a name of its own.</summary>
    Group,
}

/// <summary>
/// A chat — the place encouragement actually happens.
/// </summary>
/// <remarks>
/// A direct conversation deliberately has no <see cref="Title"/>: naming it
/// would mean storing "Jonas Weber" a second time and having it go stale the
/// day he changes his name. The name is derived from the other participant when
/// the conversation is read.
///
/// A conversation may be pinned to a goal, which is what puts the shared
/// progress bar and the "Anfeuern" button at the top of the thread.
/// </remarks>
public sealed class Conversation
{
    public const int MaxTitleLength = 80;

    private readonly List<ConversationParticipant> _participants = [];
    private readonly List<ChatMessage> _messages = [];

    // EF Core materialisation only.
    private Conversation()
    {
    }

    private Conversation(Guid id, ConversationKind kind, string? title, string? emoji, Guid? goalId, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        Title = title;
        Emoji = emoji;
        GoalId = goalId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public ConversationKind Kind { get; private set; }

    /// <summary>The group's name. Null for a direct conversation.</summary>
    public string? Title { get; private set; }

    /// <summary>The group's avatar. Null for a direct conversation.</summary>
    public string? Emoji { get; private set; }

    /// <summary>The goal this thread is about, when it is about one.</summary>
    public Guid? GoalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ConversationParticipant> Participants => _participants;

    public IReadOnlyList<ChatMessage> Messages => _messages;

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Conversation CreateDirect(Guid id, Guid? goalId, DateTimeOffset createdAt) =>
        new(id, ConversationKind.Direct, null, null, goalId, createdAt);

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Conversation CreateGroup(Guid id, string title, string? emoji, Guid? goalId, DateTimeOffset createdAt)
    {
        var normalisedTitle = title?.Trim() ?? string.Empty;

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

        return new Conversation(id, ConversationKind.Group, normalisedTitle, emoji, goalId, createdAt);
    }

    public void AddParticipant(Guid id, Guid personId, DateTimeOffset? lastReadAt = null)
    {
        if (_participants.Any(p => p.PersonId == personId))
        {
            return;
        }

        _participants.Add(new ConversationParticipant(id, Id, personId, lastReadAt));
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

    internal void MarkRead(DateTimeOffset readAt)
    {
        if (LastReadAt is null || readAt > LastReadAt)
        {
            LastReadAt = readAt;
        }
    }
}
