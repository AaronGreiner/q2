using System.Buffers.Binary;

namespace Q2.Api.Features.Images;

/// <summary>
/// What an upload actually is: its media type and its pixel dimensions, read
/// out of the bytes themselves.
/// </summary>
/// <param name="ContentType">The media type, as it will be served back.</param>
public sealed record ImageContent(string ContentType, int Width, int Height, int ByteSize);

/// <summary>
/// Reads the format and the dimensions of an upload from its first bytes.
/// </summary>
/// <remarks>
/// This exists because "the browser shrinks the picture before uploading" is
/// not a check — it is a hope about somebody else's code. The server has to
/// know what it just stored, and it has to know it without trusting the
/// <c>Content-Type</c> header or the file name, both of which the caller wrote.
///
/// Two formats, deliberately. Every upload q2 makes goes through a canvas and
/// comes out as JPEG (<c>app/app/utils/images.ts</c>), so JPEG is the one that
/// matters; PNG is accepted because a screenshot picked from disk is often one.
/// A third parser — WebP has three different headers of its own — would be code
/// nothing in the product produces.
///
/// It is a header reader, not a decoder. Nothing here allocates per pixel, so a
/// "decompression bomb" is a rejected width, not a stalled server. The trade is
/// that q2 stores exactly what it was sent rather than re-encoding it, which is
/// also why <see cref="StoredImage.MaxBytes"/> has to be a real limit rather
/// than a formality.
/// </remarks>
public static class ImageFormatReader
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    /// <summary>The media types an upload may be, for the client to send.</summary>
    public static readonly IReadOnlyList<string> AcceptedContentTypes = [Jpeg, Png];

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Returns what these bytes are, or null when they are not an image q2
    /// accepts.
    /// </summary>
    public static ImageContent? Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(PngSignature))
        {
            return ReadPng(bytes);
        }

        // FF D8 is the start-of-image marker; every JPEG begins with it.
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return ReadJpeg(bytes);
        }

        return null;
    }

    /// <summary>
    /// PNG puts its dimensions in the IHDR chunk, which the specification
    /// requires to be the first one — so they are always at a fixed offset.
    /// </summary>
    private static ImageContent? ReadPng(ReadOnlySpan<byte> bytes)
    {
        // 8 signature + 4 length + 4 type + 4 width + 4 height.
        if (bytes.Length < 24 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));

        return width is 0 or > int.MaxValue || height is 0 or > int.MaxValue
            ? null
            : new ImageContent(Png, (int)width, (int)height, bytes.Length);
    }

    /// <summary>
    /// JPEG has no fixed offset: the dimensions live in a start-of-frame
    /// segment somewhere after an arbitrary number of others, so the segments
    /// have to be walked.
    /// </summary>
    private static ImageContent? ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        var position = 2;

        while (position + 3 < bytes.Length)
        {
            // Segments are separated by fill bytes of 0xFF, any number of them.
            if (bytes[position] != 0xFF)
            {
                position++;
                continue;
            }

            var marker = bytes[position + 1];
            position += 2;

            // Standalone markers: padding, restart markers, and the two that
            // carry no length field of their own.
            if (marker is 0xFF or 0x01 or 0xD8 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            // End of image, or the start of entropy-coded data — past either,
            // there is no frame header left to find.
            if (marker is 0xD9 or 0xDA)
            {
                return null;
            }

            if (position + 1 >= bytes.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position, 2));

            // The length counts itself; anything below two is malformed and
            // would make this loop stand still.
            if (length < 2)
            {
                return null;
            }

            // Start-of-frame, in all its flavours — baseline, progressive,
            // lossless, arithmetic. The four excluded values in these ranges
            // (C4, C8, CC) are not frames: Huffman tables and JPEG extensions.
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                // length(2) + precision(1) + height(2) + width(2).
                if (position + 7 > bytes.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position + 5, 2));

                return width == 0 || height == 0
                    ? null
                    : new ImageContent(Jpeg, width, height, bytes.Length);
            }

            position += length;
        }

        return null;
    }
}
