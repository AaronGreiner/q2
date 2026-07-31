using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Chats;

/// <summary>
/// One message in a conversation.
/// </summary>
/// <remarks>
/// The text is the most personal thing q2 stores. It is never logged, never
/// attached to a Sentry event and never included in an error message
/// (docs/privacy.md).
/// </remarks>
public sealed class ChatMessage
{
    public const int MaxTextLength = 2000;

    private readonly List<MessageReaction> _reactions = [];

    // EF Core materialisation only.
    private ChatMessage()
    {
        Text = string.Empty;
    }

    private ChatMessage(Guid id, Guid conversationId, Guid senderPersonId, string text, DateTimeOffset sentAt)
    {
        Id = id;
        ConversationId = conversationId;
        SenderPersonId = senderPersonId;
        Text = text;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderPersonId { get; private set; }

    public string Text { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public IReadOnlyList<MessageReaction> Reactions => _reactions;

    /// <exception cref="DomainValidationException">The text is empty or too long.</exception>
    internal static ChatMessage Create(Guid id, Guid conversationId, Guid senderPersonId, string text, DateTimeOffset sentAt)
    {
        var normalised = text?.Trim() ?? string.Empty;

        if (normalised.Length == 0)
        {
            throw new DomainValidationException(nameof(Text), "A message cannot be empty.");
        }

        if (normalised.Length > MaxTextLength)
        {
            throw new DomainValidationException(
                nameof(Text),
                $"A message may be at most {MaxTextLength} characters long.");
        }

        return new ChatMessage(id, conversationId, senderPersonId, normalised, sentAt);
    }

    /// <summary>
    /// Adds <paramref name="emoji"/> from this person, or removes it if they
    /// had already reacted with it. Returns the new state.
    /// </summary>
    public bool ToggleReaction(Guid id, Guid personId, string emoji)
    {
        if (!MessageReactions.IsAllowed(emoji))
        {
            throw new DomainValidationException(nameof(Reactions), "That reaction is not available.");
        }

        var existing = _reactions.FirstOrDefault(r => r.PersonId == personId && r.Emoji == emoji);

        if (existing is not null)
        {
            _reactions.Remove(existing);
            return false;
        }

        _reactions.Add(new MessageReaction(id, Id, personId, emoji));
        return true;
    }
}

/// <summary>One person reacting to one message.</summary>
public sealed class MessageReaction
{
    // EF Core materialisation only.
    private MessageReaction()
    {
        Emoji = string.Empty;
    }

    internal MessageReaction(Guid id, Guid messageId, Guid personId, string emoji)
    {
        Id = id;
        MessageId = messageId;
        PersonId = personId;
        Emoji = emoji;
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid PersonId { get; private set; }

    public string Emoji { get; private set; }
}

/// <summary>
/// The reactions a message can carry.
/// </summary>
/// <remarks>
/// A closed set, so the column has a known width, the frontend can render every
/// value it will ever see, and nobody can store arbitrary text in what looks
/// like a one-tap control.
/// </remarks>
public static class MessageReactions
{
    public const string Clap = "\U0001F44F";
    public const string Fire = "\U0001F525";
    public const string Heart = "❤️";

    public static readonly IReadOnlyList<string> All = [Clap, Fire, Heart];

    public static bool IsAllowed(string? emoji) => emoji is not null && All.Contains(emoji);
}
