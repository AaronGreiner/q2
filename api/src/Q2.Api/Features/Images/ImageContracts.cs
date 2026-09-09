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
public sealed record ImageResponse(
    Guid Id,
    ImagePurpose Purpose,
    int Width,
    int Height,
    int ByteSize,
    DateTimeOffset CreatedAt)
{
    public static ImageResponse From(StoredImage image) => new(
        image.Id,
        image.Purpose,
        image.Width,
        image.Height,
        image.ByteSize,
        image.CreatedAt);
}

/// <summary>How much of their storage allowance somebody has used.</summary>
/// <param name="ByteLimit">The ceiling, so the client can say "3 of 100 MB" without hard-coding it.</param>
public sealed record ImageQuotaResponse(long BytesUsed, long ByteLimit, int ImageCount, int ImageLimit);
