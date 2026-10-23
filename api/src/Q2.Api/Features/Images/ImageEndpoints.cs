using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Images;

/// <summary>
/// HTTP surface for uploading, reading back and deleting pictures.
/// </summary>
/// <remarks>
/// Three decisions are worth writing down, because each of them is the sort of
/// thing that looks arbitrary a year later.
///
/// **The upload is a raw body, not a multipart form.** A browser can be made to
/// submit a cross-site form without any script at all, and such a form may send
/// <c>multipart/form-data</c> — so a multipart upload endpoint sitting behind a
/// cookie session is a CSRF target and needs an antiforgery token to be safe. A
/// form can never send <c>Content-Type: image/jpeg</c>; only a scripted request
/// can, and a scripted cross-origin request is already stopped by the CORS
/// allow-list in <c>ApiRegistration</c>. So the shape of the request is the
/// defence, with nothing to remember and no token to plumb through.
///
/// **Nothing is cached, and the response says so.** Every image in q2 belongs
/// to somebody, and a shared device is the ordinary case for a phone. The
/// service worker only ever precaches build output
/// (docs/adr/0012-installable-pwa.md), and <c>no-store</c> is what keeps the
/// browser's own cache — and any proxy in between — from holding a copy of a
/// photograph after the person who could see it has signed out.
///
/// **Reading an image that is not yours answers 404, not 403.** Saying "you may
/// not see this" confirms the picture exists.
/// </remarks>
public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var images = endpoints.MapGroup("/api/images").WithTags("Images").RequireAuthorization();

        images.MapPost("/", Upload)
            .WithName("UploadImage")
            .WithSummary("Stores an image, or a proof's video clip. The body is the raw file; its media type must be image/jpeg, image/png or video/mp4.")
            /*
             * byte[], not IFormFile: the body is the file itself. Declaring it
             * as a form file would also be untrue in a way that bites — ASP.NET
             * Core attaches its antiforgery requirement to form uploads, and
             * this application registers no antiforgery services because it
             * accepts no forms.
             *
             * Naming the two media types here is also what makes anything else
             * — a cross-site form's multipart body included — a 415 before a
             * single byte is read.
             */
            .Accepts<byte[]>(ImageFormatReader.Jpeg, ImageFormatReader.Png, ImageFormatReader.Mp4)
            .Produces<ImageResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        images.MapGet("/quota", GetQuota)
            .WithName("GetImageQuota")
            .WithSummary("Returns how much image storage you have used.")
            .Produces<ImageQuotaResponse>();

        images.MapGet("/{id:guid}", GetImage)
            .WithName("GetImage")
            .WithSummary("Returns the bytes of an image you are allowed to see.")
            .Produces<IResult>(StatusCodes.Status200OK, ImageFormatReader.Jpeg, ImageFormatReader.Png, ImageFormatReader.Mp4)
            .Produces(StatusCodes.Status206PartialContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        images.MapDelete("/{id:guid}", DeleteImage)
            .WithName("DeleteImage")
            .WithSummary("Deletes one of your images.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Created<ImageResponse>, ValidationProblem, ProblemHttpResult>> Upload(
        ImageService images,
        HttpRequest request,
        [FromQuery] ImagePurpose? purpose,
        CancellationToken cancellationToken)
    {
        if (!IsAcceptedContentType(request.ContentType))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ContentType"] =
                    [$"Send the file as the request body with one of: {string.Join(", ", ImageFormatReader.AcceptedContentTypes)}."],
            });
        }

        // A clip is allowed more bytes than a picture. Which limit applies is
        // taken from the header only to size the read; what the bytes turn out
        // to be is decided by reading them, and a "video" that is really a
        // picture is held to the picture's limit by StoredImage.
        var maximum = IsVideo(request.ContentType) ? StoredImage.MaxVideoBytes : StoredImage.MaxBytes;

        // The declared length is checked first so an oversized upload is
        // refused before its bytes are on the wire, and again while reading
        // because a chunked request declares nothing at all.
        if (request.ContentLength > maximum)
        {
            return TooLarge(maximum);
        }

        var bytes = await ReadBoundedAsync(request.Body, maximum, cancellationToken);

        if (bytes is null)
        {
            return TooLarge(maximum);
        }

        var created = await images.UploadAsync(purpose ?? ImagePurpose.Avatar, bytes.Value, cancellationToken);
        return TypedResults.Created($"/api/images/{created.Id}", created);
    }

    private static async Task<IResult> GetImage(
        ImageService images,
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (image, content) = await images.OpenAsync(id, cancellationToken);

        // Set before the body starts: once bytes are flowing the headers are
        // already gone.
        context.Response.Headers.CacheControl = "private, no-store";

        // The bytes are unmodified from the upload and could be anything a
        // decoder disagrees about, so they are never treated as a document:
        // no sniffing, and nothing is ever rendered as a top-level navigation.
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.ContentDisposition = "inline";

        // Ranges for a clip only. Safari will not play a <video> from a server
        // that does not answer them (206 Partial Content), and seeking needs
        // them everywhere; a photograph is always fetched whole, so for one it
        // stays off, as it always was.
        return Results.Stream(content, image.ContentType, enableRangeProcessing: image.IsVideo);
    }

    private static async Task<Ok<ImageQuotaResponse>> GetQuota(
        ImageService images,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await images.GetOwnQuotaAsync(cancellationToken));

    private static async Task<NoContent> DeleteImage(
        ImageService images,
        Guid id,
        CancellationToken cancellationToken)
    {
        await images.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static bool IsAcceptedContentType(string? contentType) =>
        contentType is not null
        && ImageFormatReader.AcceptedContentTypes.Any(accepted =>
            contentType.StartsWith(accepted, StringComparison.OrdinalIgnoreCase));

    private static bool IsVideo(string? contentType) =>
        contentType?.StartsWith(ImageFormatReader.Mp4, StringComparison.OrdinalIgnoreCase) == true;

    private static ProblemHttpResult TooLarge(int maximum) => TypedResults.Problem(
        detail: $"An upload of this kind may be at most {maximum} bytes.",
        statusCode: StatusCodes.Status413PayloadTooLarge);

    /// <summary>
    /// Reads the whole body, or returns null once it exceeds
    /// <paramref name="maximum"/>.
    /// </summary>
    /// <remarks>
    /// <c>CopyToAsync</c> into a <see cref="MemoryStream"/> would be shorter and
    /// would also let a sender of unknown length decide how much memory this
    /// process uses. The cap is checked while reading rather than after,
    /// because "after" is too late by exactly the amount that matters.
    /// </remarks>
    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedAsync(
        Stream body,
        int maximum,
        CancellationToken cancellationToken)
    {
        // One byte of headroom, so "exactly at the limit" and "over it" are
        // distinguishable rather than both looking full.
        var buffer = new byte[maximum + 1];
        var read = 0;

        while (read < buffer.Length)
        {
            var count = await body.ReadAsync(buffer.AsMemory(read), cancellationToken);

            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return read > maximum ? null : buffer.AsMemory(0, read);
    }
}
