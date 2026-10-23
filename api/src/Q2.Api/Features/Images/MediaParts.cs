using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Images;

/// <summary>
/// What a picture taken with q2's camera is made of besides its main part:
/// whether that is a clip, the clip's still, and the front camera's picture or
/// clip of who took it.
/// </summary>
/// <param name="Media">Whether the main picture is a photograph or a clip.</param>
/// <param name="PosterImageId">For a clip, and only for one: a still from it.</param>
/// <param name="InsetImageId">The front camera's picture or clip, when both cameras were used.</param>
/// <param name="InsetMedia">
/// Whether <paramref name="InsetImageId"/> is a photograph or a clip. A clip
/// only where both cameras filmed at once, beside the rear camera's clip (#151).
/// </param>
/// <param name="InsetPosterImageId">For a front clip, and only for one: a still from it.</param>
/// <remarks>
/// A proof's since #124 and #125, and since #152 everything the camera takes
/// but a profile picture: a challenge contribution, a chat photograph and the
/// photograph on a Don't's logged use. The rules about how the parts fit
/// together are here rather than in each service, so the four cannot drift
/// apart (docs/adr/0060-video-and-both-cameras.md,
/// docs/adr/0061-both-cameras-in-step.md, docs/adr/0062-the-full-camera-everywhere.md).
/// Whether each id is somebody's own upload, and whether it really is a clip,
/// is <see cref="ImageService.ResolvePartsAsync"/>' question — it needs the
/// database.
/// </remarks>
public sealed record MediaParts(
    MediaKind Media,
    Guid? PosterImageId = null,
    Guid? InsetImageId = null,
    MediaKind InsetMedia = MediaKind.Photo,
    Guid? InsetPosterImageId = null)
{
    /// <summary>A single photograph, which is what every picture was before #124.</summary>
    public static MediaParts Photo { get; } = new(MediaKind.Photo);

    /// <summary>Every part besides the main one that is there.</summary>
    public IEnumerable<Guid> OtherIds => new[] { PosterImageId, InsetImageId, InsetPosterImageId }.OfType<Guid>();

    /// <summary>What a front part is, as stored: nothing without one.</summary>
    public MediaKind? StoredInsetMedia => InsetImageId is null ? null : InsetMedia;

    /// <param name="imageId">The main part.</param>
    /// <param name="live">
    /// Whether the app says its camera took it. Null where nothing records the
    /// route — a chat photograph, a logged use — and nothing is labelled with it.
    /// </param>
    /// <exception cref="DomainValidationException">The parts do not fit together.</exception>
    public void Validate(Guid imageId, bool? live)
    {
        if (!Enum.IsDefined(Media) || !Enum.IsDefined(InsetMedia))
        {
            throw new DomainValidationException(nameof(Media), "That is not a kind of picture.");
        }

        // The still is what a clip shows before it plays, so a clip without
        // one would be a blank tile in every grid; a photograph has nothing to
        // be a still of.
        if (Media == MediaKind.Video && PosterImageId is null)
        {
            throw new DomainValidationException(nameof(PosterImageId), "A video needs a still to show before it plays.");
        }

        if (Media == MediaKind.Photo && PosterImageId is not null)
        {
            throw new DomainValidationException(nameof(PosterImageId), "Only a video has a still.");
        }

        // A front clip is filmed at the same moment as the rear one and played
        // in step with it, so it only exists beside a clip — and, like the
        // rear clip, it needs a still for the tiles where nothing plays.
        if (InsetMedia == MediaKind.Video)
        {
            if (InsetImageId is null || Media != MediaKind.Video)
            {
                throw new DomainValidationException(nameof(InsetMedia), "A front clip is only ever filmed beside a clip.");
            }

            if (InsetPosterImageId is null)
            {
                throw new DomainValidationException(nameof(InsetPosterImageId), "A front clip needs a still to show before it plays.");
            }
        }
        else if (InsetPosterImageId is not null)
        {
            throw new DomainValidationException(nameof(InsetPosterImageId), "Only a front clip has a still.");
        }

        Guid?[] others = [PosterImageId, InsetImageId, InsetPosterImageId];

        if (others.Contains(Guid.Empty))
        {
            throw new DomainValidationException(nameof(InsetImageId), "An image id must not be empty.");
        }

        var all = others.OfType<Guid>().Prepend(imageId).ToList();

        if (all.Distinct().Count() != all.Count)
        {
            throw new DomainValidationException(nameof(InsetImageId), "Each part is a picture of its own.");
        }

        // A clip and a second camera both exist only in the app's camera, so
        // where the route is recorded, neither can arrive as anything but live.
        // The rule is checked here rather than trusted from the client, which
        // would otherwise be able to call a picked file "a picture from both
        // cameras".
        if (live == false && (Media == MediaKind.Video || InsetImageId is not null))
        {
            throw new DomainValidationException("Source", "A video, or a picture from both cameras, is only ever taken live.");
        }
    }
}
