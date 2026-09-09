using System.Text;
using Q2.Api.Features.Images;

namespace Q2.Api.UnitTests.Images;

/// <summary>
/// The reader is the only thing standing between "the client said image/jpeg"
/// and what actually gets stored, so these tests are written as bytes rather
/// than as fixtures: what a header looks like is the subject.
/// </summary>
public sealed class ImageFormatReaderTests
{
    [Fact]
    public void ReadsThePngHeader()
    {
        var content = ImageFormatReader.Read(Png(640, 480));

        Assert.NotNull(content);
        Assert.Equal(ImageFormatReader.Png, content.ContentType);
        Assert.Equal(640, content.Width);
        Assert.Equal(480, content.Height);
    }

    [Fact]
    public void ReadsTheJpegHeader()
    {
        var content = ImageFormatReader.Read(Jpeg(1200, 900));

        Assert.NotNull(content);
        Assert.Equal(ImageFormatReader.Jpeg, content.ContentType);
        Assert.Equal(1200, content.Width);
        Assert.Equal(900, content.Height);
    }

    [Fact]
    public void ReadsAJpegWhoseFrameSitsBehindOtherSegments()
    {
        // A photograph from a phone carries JFIF and Exif blocks before the
        // frame header, which is exactly the case a fixed offset would miss.
        var content = ImageFormatReader.Read(Jpeg(300, 200, withMetadataSegments: true));

        Assert.NotNull(content);
        Assert.Equal(300, content.Width);
        Assert.Equal(200, content.Height);
    }

    [Fact]
    public void ReportsTheByteLengthItWasGiven()
    {
        var bytes = Png(64, 64);

        Assert.Equal(bytes.Length, ImageFormatReader.Read(bytes)!.ByteSize);
    }

    [Theory]
    [InlineData("not an image at all")]
    [InlineData("GIF89a")]
    [InlineData("%PDF-1.7")]
    [InlineData("PK")]
    public void RejectsAnythingThatIsNotAnAcceptedImage(string text)
    {
        Assert.Null(ImageFormatReader.Read(Encoding.ASCII.GetBytes(text)));
    }

    [Fact]
    public void RejectsAnEmptyBody()
    {
        Assert.Null(ImageFormatReader.Read([]));
    }

    [Fact]
    public void RejectsAPngSignatureWithNothingBehindIt()
    {
        // The signature is what a content sniffer would stop at; the dimensions
        // are what the server actually needs, so a truncated file is not an
        // image as far as q2 is concerned.
        Assert.Null(ImageFormatReader.Read(Png(10, 10).AsSpan(0, 12)));
    }

    [Fact]
    public void RejectsAJpegThatNeverDeclaresAFrame()
    {
        // Start of image, straight to end of image: syntactically a JPEG, with
        // no dimensions anywhere in it.
        Assert.Null(ImageFormatReader.Read([0xFF, 0xD8, 0xFF, 0xD9]));
    }

    [Fact]
    public void RejectsAZeroSizedImage()
    {
        Assert.Null(ImageFormatReader.Read(Png(0, 0)));
    }

    [Fact]
    public void DoesNotLoopOnAMalformedSegmentLength()
    {
        // A declared length of zero would leave the segment walk standing on the
        // same byte forever if it were trusted.
        Assert.Null(ImageFormatReader.Read([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0x00, 0x00]));
    }

    private static byte[] Png(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        bytes.AddRange([0x00, 0x00, 0x00, 0x0D]);
        bytes.AddRange("IHDR"u8);
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));

        // Bit depth, colour type, compression, filter, interlace — unread by
        // the header reader, present so the bytes are a real IHDR.
        bytes.AddRange([0x08, 0x06, 0x00, 0x00, 0x00]);

        return [.. bytes];
    }

    private static byte[] Jpeg(int width, int height, bool withMetadataSegments = false)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (withMetadataSegments)
        {
            // APP0/JFIF and APP1/Exif, each carrying a length and some payload.
            bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
            bytes.AddRange("JFIF"u8);
            bytes.AddRange(new byte[10]);

            bytes.AddRange([0xFF, 0xE1, 0x00, 0x08]);
            bytes.AddRange(new byte[6]);
        }

        // SOF0: length 17, 8-bit precision, height, width, three components.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([(byte)(height >> 8), (byte)height]);
        bytes.AddRange([(byte)(width >> 8), (byte)width]);
        bytes.AddRange(new byte[10]);

        bytes.AddRange([0xFF, 0xD9]);

        return [.. bytes];
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
