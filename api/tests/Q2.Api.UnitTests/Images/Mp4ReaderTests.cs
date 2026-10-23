using Q2.Api.Features.Images;

namespace Q2.Api.UnitTests.Images;

/// <summary>
/// What the server makes of a clip it is sent, without decoding it (#124).
/// </summary>
/// <remarks>
/// The length is the limit that matters, and a recorder hides it in different
/// places depending on how it writes the file — so each place has a test.
/// </remarks>
public sealed class Mp4ReaderTests
{
    [Fact]
    public void ReadsTheSizeAndLengthOfAFinishedMovie()
    {
        var content = ImageFormatReader.Read(Mp4Builder.Movie(width: 1280, height: 720, durationMilliseconds: 4200));

        Assert.NotNull(content);
        Assert.Equal(ImageFormatReader.Mp4, content.ContentType);
        Assert.True(content.IsVideo);
        Assert.Equal(1280, content.Width);
        Assert.Equal(720, content.Height);
        Assert.Equal(4200, content.DurationMilliseconds);
    }

    [Fact]
    public void ReadsTheLengthFromTheFragmentHeaderWhenTheMovieHeaderDoesNotKnowIt()
    {
        var content = Mp4Reader.Read(Mp4Builder.Fragmented(frames: 90, frameDuration: 1000, timescale: 30_000, fragmentHeader: true));

        Assert.Equal(3000, content?.DurationMilliseconds);
    }

    /// <summary>
    /// What <c>MediaRecorder</c> can write: no length anywhere in the header,
    /// only in the runs of samples that follow it.
    /// </summary>
    [Fact]
    public void AddsUpTheRunsWhenNoHeaderKnowsTheLength()
    {
        var content = Mp4Reader.Read(Mp4Builder.Fragmented(width: 640, height: 480, frames: 300, frameDuration: 1000, timescale: 30_000));

        Assert.NotNull(content);
        Assert.Equal(640, content.Width);
        Assert.Equal(480, content.Height);
        Assert.Equal(10_000, content.DurationMilliseconds);
    }

    [Fact]
    public void AcceptsAClipWithoutSound()
    {
        Assert.NotNull(Mp4Reader.Read(Mp4Builder.Movie(audioCodec: null)));
    }

    /// <summary>
    /// Stored, these would fail to play on somebody's phone, and nobody would
    /// find out until a friend tried to vote on them.
    /// </summary>
    [Theory]
    [InlineData("vp09", "mp4a")]
    [InlineData("hvc1", "mp4a")]
    [InlineData("av01", null)]
    [InlineData("avc1", "Opus")]
    public void RefusesACodecThatNotEveryPhoneCanPlay(string video, string? audio)
    {
        Assert.Null(Mp4Reader.Read(Mp4Builder.Movie(videoCodec: video, audioCodec: audio)));
    }

    [Fact]
    public void RefusesAFileWithNoPictureInIt()
    {
        var soundOnly = Mp4Builder.Movie(audioCodec: "mp4a").ToArray();

        // The picture track's handler renamed: a sound file in an MP4 box.
        var handler = IndexOf(soundOnly, "vide"u8);
        "meta"u8.CopyTo(soundOnly.AsSpan(handler));

        Assert.Null(Mp4Reader.Read(soundOnly));
    }

    [Fact]
    public void RefusesAClipWhoseLengthCannotBeFound()
    {
        Assert.Null(Mp4Reader.Read(Mp4Builder.Movie(durationMilliseconds: 0)));
    }

    [Fact]
    public void RefusesAFileThatDoesNotOpenWithItsFileType()
    {
        var movie = Mp4Builder.Movie();

        Assert.Null(Mp4Reader.Read(movie.AsSpan(8)));
        Assert.Null(ImageFormatReader.Read(Mp4Builder.Box("moov", [])));
    }

    /// <summary>
    /// A box that claims more bytes than there are, or fewer than its own
    /// header, must end the walk rather than send it backwards or past the end.
    /// </summary>
    [Theory]
    [InlineData(4u)]
    [InlineData(0xFFFFFF00u)]
    public void RefusesABoxThatLiesAboutItsSize(uint size)
    {
        var movie = Mp4Builder.Movie();
        var moov = IndexOf(movie, "moov"u8) - 4;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(movie.AsSpan(moov, 4), size);

        Assert.Null(Mp4Reader.Read(movie));
    }

    [Fact]
    public void ReportsTheByteLengthItWasGiven()
    {
        var movie = Mp4Builder.Movie();

        Assert.Equal(movie.Length, Mp4Reader.Read(movie)?.ByteSize);
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle) =>
        haystack.AsSpan().IndexOf(needle);
}
