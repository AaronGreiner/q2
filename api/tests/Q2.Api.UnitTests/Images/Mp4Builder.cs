using System.Buffers.Binary;
using System.Text;

namespace Q2.Api.UnitTests.Images;

/// <summary>
/// Writes the boxes of an MP4 file, for tests of what the reader makes of them.
/// </summary>
/// <remarks>
/// Headers plus a little media data, not a playable clip — like the picture
/// headers in <see cref="ImageFormatReaderTests"/>. What a recorder writes is
/// the subject: a movie header that knows the length, or a fragmented file
/// whose length has to be added up from its runs.
/// </remarks>
internal static class Mp4Builder
{
    /// <summary>
    /// A clip as a file writer finishes it: the movie header says how long it is.
    /// </summary>
    public static byte[] Movie(
        int width = 1280,
        int height = 720,
        int durationMilliseconds = 4000,
        string videoCodec = "avc1",
        string? audioCodec = "mp4a") =>
        Concat(
            FileType(),
            Box("moov", Concat(
                MovieHeader(timescale: 1000, duration: (uint)durationMilliseconds),
                Track(1, "vide", videoCodec, width, height),
                audioCodec is null ? [] : Track(2, "soun", audioCodec, 0, 0))),
            Box("mdat", new byte[64]));

    /// <summary>
    /// A clip as <c>MediaRecorder</c> writes it: the header is written first and
    /// says nothing about the length, which is in the fragments.
    /// </summary>
    /// <param name="fragmentHeader">Whether the writer also put the whole length in a fragment header.</param>
    public static byte[] Fragmented(
        int width = 640,
        int height = 480,
        int frames = 90,
        uint frameDuration = 1000,
        uint timescale = 30_000,
        bool fragmentHeader = false)
    {
        var half = frames / 2;

        return Concat(
            FileType(),
            Box("moov", Concat(
                MovieHeader(timescale: 1000, duration: 0),
                Track(1, "vide", "avc1", width, height, timescale),
                Box("mvex", Concat(
                    fragmentHeader ? FullBox("mehd", U32((uint)((long)frames * frameDuration * 1000 / timescale))) : [],
                    FullBox("trex", Concat(U32(1), U32(1), U32(frameDuration), U32(0), U32(0))))))),

            // One fragment with a duration per sample, one relying on the
            // track's default, so both ways of writing a run are read.
            Box("moof", Box("traf", Concat(
                FullBox("tfhd", U32(1)),
                FullBox("trun", Concat(U32((uint)half), Concat([.. Enumerable.Repeat(U32(frameDuration), half)])), flags: 0x100)))),
            Box("mdat", new byte[32]),
            Box("moof", Box("traf", Concat(
                FullBox("tfhd", U32(1)),
                FullBox("trun", U32((uint)(frames - half)))))),
            Box("mdat", new byte[32]));
    }

    public static byte[] FileType() =>
        Box("ftyp", Concat(Encoding.ASCII.GetBytes("isom"), U32(512), Encoding.ASCII.GetBytes("isomiso2avc1mp41")));

    public static byte[] Box(string type, byte[] body) =>
        Concat(U32((uint)(8 + body.Length)), Encoding.ASCII.GetBytes(type), body);

    private static byte[] FullBox(string type, byte[] body, uint flags = 0) =>
        Box(type, Concat(U32(flags), body));

    private static byte[] MovieHeader(uint timescale, uint duration) =>
        FullBox("mvhd", Concat(U32(0), U32(0), U32(timescale), U32(duration), new byte[80]));

    private static byte[] Track(uint id, string handler, string codec, int width, int height, uint timescale = 1000) =>
        Box("trak", Concat(
            FullBox("tkhd", Concat(U32(0), U32(0), U32(id), U32(0), U32(0), new byte[60])),
            Box("mdia", Concat(
                FullBox("mdhd", Concat(U32(0), U32(0), U32(timescale), U32(0), new byte[4])),
                FullBox("hdlr", Concat(U32(0), Encoding.ASCII.GetBytes(handler), new byte[13])),
                Box("minf", Box("stbl", FullBox("stsd", Concat(U32(1), SampleEntry(codec, width, height)))))))));

    /// <summary>reserved(6) reference(2) pre_defined/reserved(16) width(2) height(2), then the rest.</summary>
    private static byte[] SampleEntry(string codec, int width, int height)
    {
        var body = new byte[78];
        body[7] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(24, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(26, 2), (ushort)height);
        return Box(codec, body);
    }

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
