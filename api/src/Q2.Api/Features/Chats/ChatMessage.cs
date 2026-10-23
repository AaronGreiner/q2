using Q2.Api.Features.Images;
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

    /// <summary>
    /// How long after sending the words may still be corrected (#25).
    /// </summary>
    /// <remarks>
    /// Long enough for a typo noticed on rereading, short enough that an answer
    /// cannot be rewritten under the replies it got. Deleting has no such
    /// limit: taking something back is always the sender's right.
    /// </remarks>
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    private readonly List<MessageReaction> _reactions = [];

    // EF Core materialisation only.
    private ChatMessage()
    {
        Text = string.Empty;
    }

    private ChatMessage(Guid id, Guid conversationId, Guid senderPersonId, string text, Guid? imageId, DateTimeOffset sentAt, MediaParts parts)
    {
        Id = id;
        ConversationId = conversationId;
        SenderPersonId = senderPersonId;
        Text = text;
        ImageId = imageId;
        SentAt = sentAt;
        Take(parts);
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderPersonId { get; private set; }

    /// <summary>The words. Empty when the message is only a photograph.</summary>
    public string Text { get; private set; }

    /// <summary>
    /// The photograph or clip sent with it, uploaded beforehand as
    /// <see cref="Images.ImagePurpose.ChatPhoto"/>.
    /// </summary>
    public Guid? ImageId { get; private set; }

    /// <summary>Whether <see cref="ImageId"/> is a photograph or a clip (#152); a photograph where there is none.</summary>
    public MediaKind Media { get; private set; }

    /// <summary>For a clip: a still from it, shown before it plays and on a tile.</summary>
    public Guid? PosterImageId { get; private set; }

    /// <summary>The front camera's picture or clip, when both cameras were used.</summary>
    public Guid? InsetImageId { get; private set; }

    /// <summary>Whether <see cref="InsetImageId"/> is a photograph or a clip. Null without one.</summary>
    public MediaKind? InsetMedia { get; private set; }

    /// <summary>For a front clip: a still from it.</summary>
    public Guid? InsetPosterImageId { get; private set; }

    /// <summary>The parts beside the main picture, as <see cref="MediaParts"/> checks them.</summary>
    public MediaParts Parts => new(Media, PosterImageId, InsetImageId, InsetMedia ?? MediaKind.Photo, InsetPosterImageId);

    /// <summary>Every picture it carries: the main one and its parts. Empty without one.</summary>
    public IEnumerable<Guid> PictureIds => ImageId is { } main ? Parts.OtherIds.Prepend(main) : [];

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>When the words were last corrected, or null if they never were.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    /// When its sender took it back (#25), or null. A deleted message keeps its
    /// row and its place in the thread, so the others see that something was
    /// said and withdrawn; its words, photograph and reactions are gone.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public IReadOnlyList<MessageReaction> Reactions => _reactions;

    /// <summary>
    /// Whether <paramref name="personId"/> may still correct the words: their
    /// own message, not taken back, with words to correct, inside
    /// <see cref="EditWindow"/>.
    /// </summary>
    public bool CanBeEditedBy(Guid personId, DateTimeOffset now) =>
        personId == SenderPersonId && !IsDeleted && Text.Length > 0 && now - SentAt <= EditWindow;

    /// <summary>
    /// Corrects the words. Returns false when they are the same as before, so
    /// nothing is marked as edited that was not.
    /// </summary>
    /// <exception cref="AccessDeniedException">It is somebody else's message.</exception>
    /// <exception cref="DomainValidationException">
    /// It was taken back, it is a photograph without words, the window has
    /// passed, or the new words are empty or too long.
    /// </exception>
    public bool Edit(Guid personId, string? text, DateTimeOffset now)
    {
        if (personId != SenderPersonId)
        {
            throw new AccessDeniedException("Only its sender can change a message.");
        }

        if (IsDeleted || Text.Length == 0)
        {
            throw new DomainValidationException(nameof(Text), "There are no words here to change.");
        }

        if (now - SentAt > EditWindow)
        {
            throw new DomainValidationException(
                nameof(Text),
                $"A message can only be changed within {EditWindow.TotalMinutes} minutes of sending it.",
                new(FieldErrorCodes.TooLateToEdit, Max: (int)EditWindow.TotalMinutes));
        }

        var normalised = text?.Trim() ?? string.Empty;

        if (normalised.Length == 0)
        {
            throw new DomainValidationException(nameof(Text), "A message cannot be empty.", new(FieldErrorCodes.Required));
        }

        if (normalised.Length > MaxTextLength)
        {
            throw new DomainValidationException(
                nameof(Text),
                $"A message may be at most {MaxTextLength} characters long.",
                new(FieldErrorCodes.TooLong, Max: MaxTextLength));
        }

        if (normalised == Text)
        {
            return false;
        }

        Text = normalised;
        EditedAt = now;
        return true;
    }

    /// <summary>
    /// Takes the message back: the words, the photograph and the reactions go,
    /// the place in the thread stays. Returns the photograph that has to go
    /// with it, or null — and nothing at all when it was already taken back.
    /// </summary>
    /// <summary>
    /// Takes the message back, and returns every picture it carried — the
    /// photograph or clip and whatever came with it — for the caller to remove.
    /// </summary>
    /// <exception cref="AccessDeniedException">It is somebody else's message.</exception>
    public IReadOnlyList<Guid> Delete(Guid personId, DateTimeOffset now)
    {
        if (personId != SenderPersonId)
        {
            throw new AccessDeniedException("Only its sender can delete a message.");
        }

        if (IsDeleted)
        {
            return [];
        }

        IReadOnlyList<Guid> photos = [.. PictureIds];

        Text = string.Empty;
        ImageId = null;
        Take(MediaParts.Photo);
        DeletedAt = now;
        _reactions.Clear();

        return photos;
    }

    private void Take(MediaParts parts)
    {
        Media = parts.Media;
        PosterImageId = parts.PosterImageId;
        InsetImageId = parts.InsetImageId;
        InsetMedia = parts.StoredInsetMedia;
        InsetPosterImageId = parts.InsetPosterImageId;
    }

    /// <exception cref="DomainValidationException">
    /// There is neither text nor a photograph, or the text is too long.
    /// </exception>
    internal static ChatMessage Create(
        Guid id,
        Guid conversationId,
        Guid senderPersonId,
        string text,
        Guid? imageId,
        DateTimeOffset sentAt,
        MediaParts? parts = null)
    {
        var normalised = text?.Trim() ?? string.Empty;

        if (imageId == Guid.Empty)
        {
            imageId = null;
        }

        // Nothing records whether a chat photograph came from the camera, and
        // nothing labels it, so only the shape of the parts is checked.
        parts = imageId is null ? MediaParts.Photo : parts ?? MediaParts.Photo;
        if (imageId is { } main)
        {
            parts.Validate(main, live: null);
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

        return new ChatMessage(id, conversationId, senderPersonId, normalised, imageId, sentAt, parts);
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
