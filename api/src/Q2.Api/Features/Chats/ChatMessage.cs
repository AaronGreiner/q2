using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Chats;

/// <summary>
/// One message in a conversation: words, a photograph, or both.
/// </summary>
/// <remarks>
/// The text is the most personal thing q2 stores. It is never logged, never
/// attached to a Sentry event and never included in an error message
/// (docs/privacy.md).
///
/// A photograph is referenced by <see cref="ImageId"/> without a foreign key,
/// the same way a proof references its picture: the image row records that a
/// file exists and who may read it (docs/adr/0017-image-storage.md), and adding
/// a key to this table would make SQLite rebuild it. Whoever removes messages
/// with photographs removes the pictures as well
/// (<see cref="Images.ImageService.RemoveChatPhotosAsync"/>).
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

    private ChatMessage(Guid id, Guid conversationId, Guid senderPersonId, string text, Guid? imageId, DateTimeOffset sentAt)
    {
        Id = id;
        ConversationId = conversationId;
        SenderPersonId = senderPersonId;
        Text = text;
        ImageId = imageId;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderPersonId { get; private set; }

    /// <summary>The words. Empty when the message is only a photograph.</summary>
    public string Text { get; private set; }

    /// <summary>
    /// The photograph sent with it, uploaded beforehand as
    /// <see cref="Images.ImagePurpose.ChatPhoto"/>.
    /// </summary>
    public Guid? ImageId { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public IReadOnlyList<MessageReaction> Reactions => _reactions;

    /// <exception cref="DomainValidationException">
    /// There is neither text nor a photograph, or the text is too long.
    /// </exception>
    internal static ChatMessage Create(
        Guid id,
        Guid conversationId,
        Guid senderPersonId,
        string text,
        Guid? imageId,
        DateTimeOffset sentAt)
    {
        var normalised = text?.Trim() ?? string.Empty;

        if (imageId == Guid.Empty)
        {
            imageId = null;
        }

        if (normalised.Length == 0 && imageId is null)
        {
            throw new DomainValidationException(nameof(Text), "A message cannot be empty.");
        }

        if (normalised.Length > MaxTextLength)
        {
            throw new DomainValidationException(
                nameof(Text),
                $"A message may be at most {MaxTextLength} characters long.");
        }

        return new ChatMessage(id, conversationId, senderPersonId, normalised, imageId, sentAt);
    }

    /// <summary>
    /// Adds <paramref name="kind"/> from this person, or removes it if they
    /// had already reacted with it. Returns the new state.
    /// </summary>
    public bool ToggleReaction(Guid id, Guid personId, KudosKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainValidationException(nameof(Reactions), "That reaction is not available.");
        }

        var existing = _reactions.FirstOrDefault(r => r.PersonId == personId && r.Kind == kind);

        if (existing is not null)
        {
            _reactions.Remove(existing);
            return false;
        }

        _reactions.Add(new MessageReaction(id, Id, personId, kind));
        return true;
    }
}

/// <summary>One person reacting to one message.</summary>
public sealed class MessageReaction
{
    // EF Core materialisation only.
    private MessageReaction()
    {
    }

    internal MessageReaction(Guid id, Guid messageId, Guid personId, KudosKind kind)
    {
        Id = id;
        MessageId = messageId;
        PersonId = personId;
        Kind = kind;
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid PersonId { get; private set; }

    public KudosKind Kind { get; private set; }
}

/// <summary>
/// The three ways one person tells another they saw it.
/// </summary>
/// <remarks>
/// This is what q2 is named after, and there are three of them rather than one
/// because "well done" has more than one register: <see cref="Fire"/> is for
/// something impressive, <see cref="Strong"/> for something hard, and
/// <see cref="Applause"/> for something finished. They are counted together as
/// kudos on a profile — the split is expression, not accounting.
///
/// Stored as a name rather than as the emoji it used to be. An emoji is a
/// picture with a language and a platform behind it: 👏 is a different drawing
/// on Android, has no accessible name we control, and cannot be styled. A name
/// renders as whichever icon the design says today, reads out as a word in both
/// languages, and survives the interface being redrawn.
/// </remarks>
public enum KudosKind
{
    /// <summary>Impressive.</summary>
    Fire,

    /// <summary>That looked hard.</summary>
    Strong,

    /// <summary>Finished — well done.</summary>
    Applause,
}
