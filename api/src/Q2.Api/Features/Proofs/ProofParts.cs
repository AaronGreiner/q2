using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// What a proof is made of besides its main picture: whether that picture is a
/// clip, the clip's still, and the front camera's picture of who took it.
/// </summary>
/// <param name="Media">Whether the main picture is a photograph or a clip.</param>
/// <param name="PosterImageId">For a clip, and only for one: a still from it.</param>
/// <param name="InsetImageId">The front camera's picture, when both cameras were used.</param>
/// <remarks>
/// The rules about how these fit together are here rather than spread over the
/// service, so a spot check and an ordinary delivery cannot drift apart
/// (docs/adr/0060-video-and-both-cameras.md). Whether each id is somebody's own
/// proof upload, and whether it really is a clip, is the service's question —
/// it needs the database.
/// </remarks>
public sealed record ProofParts(MediaKind Media, Guid? PosterImageId = null, Guid? InsetImageId = null)
{
    /// <summary>A single photograph, which is what every proof was before #124.</summary>
    public static ProofParts Photo { get; } = new(MediaKind.Photo);

    /// <exception cref="DomainValidationException">The parts do not fit together.</exception>
    internal void Validate(Guid imageId, ProofSource source)
    {
        if (!Enum.IsDefined(Media))
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

        if (PosterImageId == Guid.Empty || InsetImageId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(InsetImageId), "An image id must not be empty.");
        }

        if (PosterImageId == imageId || InsetImageId == imageId
            || (InsetImageId is not null && InsetImageId == PosterImageId))
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
