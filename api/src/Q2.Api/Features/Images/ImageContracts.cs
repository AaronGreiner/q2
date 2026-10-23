namespace Q2.Api.Features.Images;

/// <summary>
/// An image the caller may now point something at.
/// </summary>
/// <remarks>
/// No URL. The address of an image is <c>/api/images/{id}</c> for every image
/// there will ever be, so sending it would be sending the same string a
/// thousand times — and, worse, it would read as though the server were
/// offering a link that works without a session. It does not: the endpoint
/// checks who is asking (<see cref="ImageService.CanRead"/>).
///
/// The dimensions travel because the client needs them <em>before</em> the
/// bytes arrive: a box reserved at the right aspect ratio is the difference
/// between a photograph appearing and a screen jumping.
/// </remarks>
/// <param name="Kind">A picture, or — only ever for a proof — a clip.</param>
/// <param name="DurationMilliseconds">How long a clip runs, as its headers say. Null for a picture.</param>
public sealed record ImageResponse(
    Guid Id,
    ImagePurpose Purpose,
    int Width,
    int Height,
    int ByteSize,
    DateTimeOffset CreatedAt,
    MediaKind Kind,
    int? DurationMilliseconds)
{
    public static ImageResponse From(StoredImage image) => new(
        image.Id,
        image.Purpose,
        image.Width,
        image.Height,
        image.ByteSize,
        image.CreatedAt,
        image.IsVideo ? MediaKind.Video : MediaKind.Photo,
        image.DurationMilliseconds);
}

/// <summary>How much of their storage allowance somebody has used.</summary>
/// <param name="ByteLimit">The ceiling, so the client can say "3 of 300 MB" without hard-coding it.</param>
/// <param name="VideoBytesUsed">How much of it clips take.</param>
/// <param name="VideoByteLimit">The share of <paramref name="ByteLimit"/> clips may take.</param>
/// <param name="VideoMaxBytes">The largest single clip.</param>
/// <param name="VideoMaxSeconds">The longest a clip may run.</param>
public sealed record ImageQuotaResponse(
    long BytesUsed,
    long ByteLimit,
    int ImageCount,
    int ImageLimit,
    long VideoBytesUsed,
    long VideoByteLimit,
    int VideoMaxBytes,
    int VideoMaxSeconds);
