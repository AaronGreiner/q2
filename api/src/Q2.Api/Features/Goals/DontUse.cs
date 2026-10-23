using Q2.Api.Features.Images;

namespace Q2.Api.Features.Goals;

/// <summary>
/// One use a Don't's owner logged: "Heute 1 Bier".
/// </summary>
/// <remarks>
/// A row rather than a counter on the window, because each one is a line in the
/// goal's conversation (derived by <see cref="GoalTimeline"/>, never written as
/// a message) and may carry a photograph. Nobody votes on it and nobody is
/// notified of it: the owner said it themselves, and a confession that sets off
/// a round of pings is one people stop making.
/// </remarks>
public sealed class DontUse
{
    /// <summary>
    /// How long after logging a use its owner may take it back (#110).
    /// </summary>
    /// <remarks>
    /// For a slipped thumb, not for second thoughts: long enough for the
    /// "Rückgängig" the app shows for a few seconds to reach the server over a
    /// slow connection, short enough that the count stays an honest record.
    /// </remarks>
    public static readonly TimeSpan UndoPeriod = TimeSpan.FromSeconds(30);

    // EF Core materialisation only.
    private DontUse()
    {
    }

    internal DontUse(Guid id, Guid goalInstanceId, Guid? imageId, DateTimeOffset loggedAt, MediaParts parts)
    {
        Id = id;
        GoalInstanceId = goalInstanceId;
        ImageId = imageId;
        LoggedAt = loggedAt;
        Take(imageId is null ? MediaParts.Photo : parts);
    }

    public Guid Id { get; private set; }

    public Guid GoalInstanceId { get; private set; }

    /// <summary>An optional photograph or clip, read by the goal's audience like a proof.</summary>
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

    public DateTimeOffset LoggedAt { get; private set; }

    private void Take(MediaParts parts)
    {
        Media = parts.Media;
        PosterImageId = parts.PosterImageId;
        InsetImageId = parts.InsetImageId;
        InsetMedia = parts.StoredInsetMedia;
        InsetPosterImageId = parts.InsetPosterImageId;
    }
}
