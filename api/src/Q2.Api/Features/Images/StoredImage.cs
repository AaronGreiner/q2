using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Images;

/// <summary>What an image was uploaded for.</summary>
/// <remarks>
/// Not decoration: the purpose is what decides who may read the bytes back
/// (<see cref="ImageService.CanRead"/>). An image whose purpose says nothing
/// about its audience is readable by its owner and by nobody else, which is why
/// a new purpose has to be added here <em>and</em> answered there.
///
/// Stored as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum ImagePurpose
{
    /// <summary>
    /// Somebody's profile picture. Readable by anyone signed in — the same
    /// reach the initials-on-a-colour avatar has always had.
    /// </summary>
    Avatar,

    /// <summary>
    /// A photograph delivered against a goal's window. Readable by the people
    /// that goal is shared with.
    /// </summary>
    /// <remarks>
    /// The audience arrives with stage 4, together with the vote it hangs off.
    /// Until then the rule is the safe one: only its owner can see it.
    /// </remarks>
    Proof,

    /// <summary>
    /// A contribution to the daily challenge. Readable by the people whose room
    /// it is actually in.
    /// </summary>
    /// <remarks>
    /// The narrowest audience of the three, and the only one that depends on
    /// what the <em>viewer</em> has done: a friend of the author sees it once
    /// they have contributed to the same challenge themselves, and not before.
    /// That is the reciprocity rule (<see cref="Q2.Api.Features.Challenges.Challenge.RevealsTo"/>),
    /// and it is answered here as well as in the room so that having the id is
    /// never the same thing as being allowed to look.
    /// </remarks>
    ChallengeEntry,
}

/// <summary>
/// One picture somebody uploaded: what it is, who it belongs to, and how big.
/// </summary>
/// <remarks>
/// The bytes are not here. They live behind <see cref="IImageStore"/>, and this
/// row is the record that says a file exists, who may read it and what it
/// costs them against their quota. Splitting them is what lets the storage move
/// to an S3-compatible bucket later without touching a single query
/// (docs/adr/0017-image-storage.md).
///
/// <see cref="ContentType"/>, <see cref="Width"/> and <see cref="Height"/> are
/// what <see cref="ImageFormatReader"/> found in the bytes, never what the
/// upload claimed. A client that says "image/png" over a zip archive is not a
/// hypothetical; it is the first thing anybody tries.
///
/// There is no storage-key column. The key is derived from
/// <see cref="Id"/> (<see cref="FileSystemImageStore"/>), and a second copy of
/// that fact would be a second thing that can be wrong.
/// </remarks>
public sealed class StoredImage
{
    /// <summary>
    /// The largest single upload, in bytes.
    /// </summary>
    /// <remarks>
    /// Four megabytes is generous for something the browser has already scaled
    /// to at most <see cref="MaxDimension"/> pixels — a photograph that size
    /// leaves the canvas at well under one. It is the cap for a client that did
    /// not scale, or did not want to.
    /// </remarks>
    public const int MaxBytes = 4 * 1024 * 1024;

    /// <summary>The longest edge an upload may have.</summary>
    /// <remarks>
    /// A phone screen is 390 points wide and the largest thing q2 ever draws is
    /// a full-width proof photograph, so 2048 is already three times what any
    /// display needs. Above it, storage is being spent on pixels nobody sees.
    /// </remarks>
    public const int MaxDimension = 2048;

    /// <summary>The shortest edge an upload may have.</summary>
    /// <remarks>
    /// A 1×1 pixel is not a photograph. This is the floor that stops the
    /// per-person byte quota from being sidestepped with a million tiny rows.
    /// </remarks>
    public const int MinDimension = 32;

    /// <summary>How much storage one person may use, in bytes.</summary>
    public const long MaxBytesPerPerson = 100L * 1024 * 1024;

    /// <summary>How many images one person may keep.</summary>
    /// <remarks>
    /// A second ceiling beside <see cref="MaxBytesPerPerson"/> because they cap
    /// different failures: bytes bound the disk, count bounds the row count and
    /// the directory. Either alone leaves the other open.
    /// </remarks>
    public const int MaxImagesPerPerson = 500;

    // EF Core materialisation only.
    private StoredImage()
    {
        ContentType = string.Empty;
    }

    private StoredImage(
        Guid id,
        Guid ownerPersonId,
        ImagePurpose purpose,
        string contentType,
        int byteSize,
        int width,
        int height,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerPersonId = ownerPersonId;
        Purpose = purpose;
        ContentType = contentType;
        ByteSize = byteSize;
        Width = width;
        Height = height;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Who uploaded it. The only person who may ever delete it.</summary>
    public Guid OwnerPersonId { get; private set; }

    public ImagePurpose Purpose { get; private set; }

    /// <summary>The media type read out of the bytes, not the one declared.</summary>
    public string ContentType { get; private set; }

    public int ByteSize { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Records an upload whose bytes have already been read and understood.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// The image is too large, too small, or of a kind q2 does not accept.
    /// </exception>
    public static StoredImage Create(
        Guid id,
        Guid ownerPersonId,
        ImagePurpose purpose,
        ImageContent content,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(content);

        var errors = new Dictionary<string, string[]>();

        if (ownerPersonId == Guid.Empty)
        {
            errors[nameof(OwnerPersonId)] = ["An image needs somebody it belongs to."];
        }

        if (content.ByteSize is <= 0 or > MaxBytes)
        {
            errors[nameof(ByteSize)] = [$"An image may be at most {MaxBytes} bytes."];
        }

        if (content.Width > MaxDimension || content.Height > MaxDimension)
        {
            errors[nameof(Width)] = [$"An image may be at most {MaxDimension} pixels on its longest edge."];
        }

        if (content.Width < MinDimension || content.Height < MinDimension)
        {
            errors[nameof(Width)] = [$"An image must be at least {MinDimension} pixels on each edge."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new StoredImage(
            id,
            ownerPersonId,
            purpose,
            content.ContentType,
            content.ByteSize,
            content.Width,
            content.Height,
            createdAt);
    }
}
