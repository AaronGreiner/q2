using System.Buffers.Binary;
using System.Text;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// MP4 files that <see cref="Q2.Api.Features.Images.Mp4Reader"/> accepts, for
/// tests that need to upload a clip.
/// </summary>
/// <remarks>
/// Boxes plus padding, like <see cref="TestImages"/>: nothing in q2 decodes a
/// clip, so a real one would only make the run slower. The unit tests'
/// <c>Mp4Builder</c> writes the fragmented variants; this writes the plain one
/// a test needs to get a clip stored.
/// </remarks>
public static class TestVideos
{
    public static byte[] Mp4(int durationMilliseconds = 4000, string videoCodec = "avc1", int padding = 256) =>
        Concat(
            Box("ftyp", Concat(Ascii("isom"), U32(512), Ascii("isomiso2avc1mp41"))),
            Box("moov", Concat(
                FullBox("mvhd", Concat(U32(0), U32(0), U32(1000), U32((uint)durationMilliseconds), new byte[80])),
                Box("trak", Concat(
                    FullBox("tkhd", Concat(U32(0), U32(0), U32(1), U32(0), U32(0), new byte[60])),
                    Box("mdia", Concat(
                        FullBox("mdhd", Concat(U32(0), U32(0), U32(1000), U32(0), new byte[4])),
                        FullBox("hdlr", Concat(U32(0), Ascii("vide"), new byte[13])),
                        Box("minf", Box("stbl", FullBox("stsd", Concat(U32(1), VisualEntry(videoCodec, 1280, 720))))))))))),
            Box("mdat", new byte[padding]));

    private static byte[] VisualEntry(string codec, int width, int height)
    {
        var body = new byte[78];
        body[7] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(24, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(26, 2), (ushort)height);
        return Box(codec, body);
    }

    private static byte[] Box(string type, byte[] body) => Concat(U32((uint)(8 + body.Length)), Ascii(type), body);

    private static byte[] FullBox(string type, byte[] body) => Box(type, Concat(U32(0), body));

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
