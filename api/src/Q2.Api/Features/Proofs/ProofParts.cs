using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// What a proof is made of besides its main picture: whether that picture is a
/// clip, the clip's still, and the front camera's picture or clip of who took
/// it.
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
/// The rules about how these fit together are here rather than spread over the
/// service, so a spot check and an ordinary delivery cannot drift apart
/// (docs/adr/0060-video-and-both-cameras.md, docs/adr/0061-both-cameras-in-step.md).
/// Whether each id is somebody's own proof upload, and whether it really is a
/// clip, is the service's question — it needs the database.
/// </remarks>
public sealed record ProofParts(
    MediaKind Media,
    Guid? PosterImageId = null,
    Guid? InsetImageId = null,
    MediaKind InsetMedia = MediaKind.Photo,
    Guid? InsetPosterImageId = null)
{
    /// <summary>A single photograph, which is what every proof was before #124.</summary>
    public static ProofParts Photo { get; } = new(MediaKind.Photo);

    /// <exception cref="DomainValidationException">The parts do not fit together.</exception>
    internal void Validate(Guid imageId, ProofSource source)
    {
        if (!Enum.IsDefined(Media) || !Enum.IsDefined(InsetMedia))
        {
            throw new DomainValidationException(nameof(Media), "That is not a kind of proof.");
        }

        // The still is what a clip shows before it plays, so a clip without
        // one would be a blank tile in every grid; a photograph has nothing to
        // be a still of.
        if (Media == MediaKind.Video && PosterImageId is null)
        {
            throw new DomainValidationException(nameof(PosterImageId), "A video proof needs a still to show before it plays.");
        }

        if (Media == MediaKind.Photo && PosterImageId is not null)
        {
            throw new DomainValidationException(nameof(PosterImageId), "Only a video proof has a still.");
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
            throw new DomainValidationException(nameof(InsetImageId), "Each part of a proof is a picture of its own.");
        }

        // A clip and a second camera both exist only in the app's camera, so
        // neither can arrive as anything but live. The rule is checked here
        // rather than trusted from the client, which would otherwise be able
        // to call a picked file "a picture from both cameras".
        if ((Media == MediaKind.Video || InsetImageId is not null) && source != ProofSource.Live)
        {
            throw new DomainValidationException(
                nameof(ProofPhoto.Source),
                "A video, or a picture from both cameras, is only ever taken live.");
        }
    }
}
