namespace Q2.Api.Features.Images;

/// <summary>Whether something stored is a still picture or a clip.</summary>
/// <remarks>
/// Persisted as text where it is stored at all, like every other enum here. On
/// <see cref="StoredImage"/> it is not stored: the media type read out of the
/// bytes already says it, and a second column would be a second thing that
/// could disagree.
/// </remarks>
public enum MediaKind
{
    Photo,

    /// <summary>A short MP4 clip, only ever a proof (docs/adr/0060-video-and-both-cameras.md).</summary>
    Video,
}
