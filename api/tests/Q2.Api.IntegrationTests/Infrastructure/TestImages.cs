namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Byte arrays that <see cref="Q2.Api.Features.Images.ImageFormatReader"/>
/// recognises, for tests that need to upload something.
/// </summary>
/// <remarks>
/// Headers plus padding, not real pictures. Nothing in q2 decodes an image —
/// the server reads the dimensions out of the header and stores the bytes
/// unchanged — so a real photograph in the repository would be several hundred
/// kilobytes buying nothing but a slower test run.
///
/// The trade is deliberate and has one consequence worth knowing: these files
/// cannot be opened in a viewer. A test that needs to see a picture with its
/// own eyes belongs in the E2E suite, against a browser.
/// </remarks>
public static class TestImages
{
    public static byte[] Png(int width, int height, int padding = 0)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // IHDR: length 13, then the type, then the two dimensions.
        bytes.AddRange([0x00, 0x00, 0x00, 0x0D]);
        bytes.AddRange("IHDR"u8);
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange([0x08, 0x06, 0x00, 0x00, 0x00]);
        bytes.AddRange(new byte[padding]);

        return [.. bytes];
    }

    public static byte[] Jpeg(int width, int height, int padding = 0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        // SOF0: length 17, 8-bit precision, height then width, components.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([(byte)(height >> 8), (byte)height]);
        bytes.AddRange([(byte)(width >> 8), (byte)width]);
        bytes.AddRange(new byte[10]);
        bytes.AddRange([0xFF, 0xD9]);
        bytes.AddRange(new byte[padding]);

        return [.. bytes];
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
