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
    /// That set is a question about the goal rather than the image, so it is
    /// answered in <see cref="ImageService.CanReadAsync"/> and not here.
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

    /// <summary>
    /// A photograph sent in a conversation. Readable by the people in that
    /// conversation.
    /// </summary>
    /// <remarks>
    /// Like a proof, the audience is a question about what points at the
    /// picture — the message, and the conversation it is in — so it is answered
    /// in <see cref="ImageService.CanReadAsync"/>. Somebody who leaves a group
    /// stops being able to open its pictures, exactly as they stop being able
    /// to open its thread.
    /// </remarks>
    ChatPhoto,
}

/// <summary>
/// One picture — or, for a proof, one short clip — somebody uploaded: what it
/// is, who it belongs to, and how big.
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
/// A clip is a row here too, with <see cref="ContentType"/> <c>video/mp4</c>
/// and a <see cref="DurationMilliseconds"/>: the same seam, the same session
/// check and the same erasure as a photograph, rather than a second store that
/// would need all three again (docs/adr/0060-video-and-both-cameras.md). Only a
/// proof may be one.
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

    /// <summary>How much storage one person may use, in bytes, pictures and clips together.</summary>
    /// <remarks>
    /// It was 100 MB while everything was a photograph. Clips get a share of
    /// their own on top (<see cref="MaxVideoBytesPerPerson"/>), so pictures keep
    /// at least the 100 MB they always had however many clips somebody records.
    /// </remarks>
    public const long MaxBytesPerPerson = 300L * 1024 * 1024;

    /// <summary>How much of <see cref="MaxBytesPerPerson"/> clips may take.</summary>
    public const long MaxVideoBytesPerPerson = 200L * 1024 * 1024;

    /// <summary>The largest single clip, in bytes.</summary>
    /// <remarks>
    /// The app records at 2.5 Mbit/s, which is about 3.5 MB for the longest
    /// clip it allows. Twelve leaves room for a phone that ignores the bitrate it
    /// was asked for, and is still a limit rather than a formality.
    /// </remarks>
    public const int MaxVideoBytes = 12 * 1024 * 1024;

    /// <summary>The longest a clip may run, as the app records it.</summary>
    public const int MaxVideoSeconds = 10;

    /// <summary>The longest a clip may run as the server checks it, in milliseconds.</summary>
    /// <remarks>
    /// A second longer than <see cref="MaxVideoSeconds"/>: a recorder told to
    /// stop at ten seconds finishes the frame it is on and writes the last
    /// fragment afterwards, so a clip stopped exactly at the limit runs a moment
    /// past it. What this refuses is a clip that was never stopped at all.
    /// </remarks>
    public const int MaxVideoMilliseconds = (MaxVideoSeconds + 1) * 1000;

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
        int? durationMilliseconds,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerPersonId = ownerPersonId;
        Purpose = purpose;
        ContentType = contentType;
        ByteSize = byteSize;
        Width = width;
        Height = height;
        DurationMilliseconds = durationMilliseconds;
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

    /// <summary>How long a clip runs, read from its headers. Null for a picture.</summary>
    public int? DurationMilliseconds { get; private set; }

    /// <summary>Whether this is a clip rather than a still picture.</summary>
    public bool IsVideo => ContentType == ImageFormatReader.Mp4;

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

        if (content.IsVideo)
        {
            // A clip comes from q2's camera, which records one wherever it
            // takes more than a profile picture (#124, #152). An avatar that
            // moves would be a different product.
            if (!MayBeVideo(purpose))
            {
                errors[nameof(Purpose)] = ["A profile picture cannot be a video."];
            }

            if (content.ByteSize is <= 0 or > MaxVideoBytes)
            {
                errors[nameof(ByteSize)] = [$"A video may be at most {MaxVideoBytes} bytes."];
            }

            if (content.DurationMilliseconds is not (> 0 and <= MaxVideoMilliseconds))
            {
                errors[nameof(DurationMilliseconds)] = [$"A video may be at most {MaxVideoSeconds} seconds long."];
            }
        }
        else if (content.ByteSize is <= 0 or > MaxBytes)
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
            content.IsVideo ? content.DurationMilliseconds : null,
            createdAt);
    }

    /// <summary>
    /// Whether an upload for <paramref name="purpose"/> may be a clip: whatever
    /// the camera takes with its clip mode — a proof (and a Don't's logged use,
    /// which is uploaded as one), a challenge contribution and a chat
    /// photograph — but never a profile picture.
    /// </summary>
    public static bool MayBeVideo(ImagePurpose purpose) => purpose switch
    {
        ImagePurpose.Proof or ImagePurpose.ChallengeEntry or ImagePurpose.ChatPhoto => true,
        _ => false,
    };
}
