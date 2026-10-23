using System.Buffers.Binary;

namespace Q2.Api.Features.Images;

/// <summary>
/// Reads what an MP4 clip is — its picture size and how long it runs — from
/// the boxes it is made of, without decoding a single frame.
/// </summary>
/// <remarks>
/// The same posture as <see cref="ImageFormatReader"/>, for the same reasons:
/// the <c>Content-Type</c> header is the caller's claim, and the server has to
/// know what it is storing before it stores it. There is no ffmpeg and no
/// transcoding (docs/adr/0060-video-and-both-cameras.md), so this walks the
/// ISO base media boxes and nothing more.
///
/// **Only what every q2 client can play is accepted:** an H.264 picture
/// (<c>avc1</c> or <c>avc3</c>) and, if there is sound, AAC (<c>mp4a</c>). A
/// clip in another codec would be stored perfectly well and then fail to play
/// on somebody's iPhone, which is worse than refusing it up front.
///
/// **A recorder writes fragmented MP4.** <c>MediaRecorder</c> in Chromium and
/// Safari writes the header before it knows how long the clip will be, so the
/// movie header may say zero. The length is then taken from the fragment
/// header (<c>mehd</c>), and failing that by adding up the sample durations in
/// every fragment's run (<c>trun</c>) — still header reading, a few bytes per
/// sample. A clip whose length cannot be found at all is refused: "too long" is
/// a limit, and a limit that cannot be checked is not one.
///
/// Every read is bounds-checked and every loop advances by at least one box
/// header, so a malformed file is a refusal, never a stalled request.
/// </remarks>
public static class Mp4Reader
{
    /// <summary>How deep boxes may nest before the file is treated as malformed.</summary>
    private const int MaxDepth = 8;

    /// <summary>
    /// Returns the clip's size and length, or null when these bytes are not an
    /// MP4 clip q2 accepts.
    /// </summary>
    public static ImageContent? Read(ReadOnlySpan<byte> bytes)
    {
        // An MP4 file opens with its file-type box. Without it, whatever this
        // is, it is not something a browser will play as video/mp4.
        if (bytes.Length < 16 || !bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return null;
        }

        var movie = new Movie();

        if (!Walk(bytes, 0, bytes.Length, 0, movie))
        {
            return null;
        }

        var video = movie.Tracks.FirstOrDefault(track => track.IsVideo);

        if (video is null
            || video.Codec is not ("avc1" or "avc3")
            || video.Width <= 0
            || video.Height <= 0)
        {
            return null;
        }

        // Sound is optional; if there is a sound track, it has to be AAC.
        if (movie.Tracks.Any(track => track.IsAudio && track.Codec != "mp4a"))
        {
            return null;
        }

        var duration = DurationOf(movie, video);

        return duration is > 0 and <= int.MaxValue
            ? new ImageContent(ImageFormatReader.Mp4, video.Width, video.Height, bytes.Length, (int)duration)
            : null;
    }

    /// <summary>The clip's length in milliseconds, from the first header that knows it.</summary>
    private static long? DurationOf(Movie movie, Track video)
    {
        // The movie header, when the recorder went back and filled it in.
        // All ones means "unknown" in the specification.
        if (movie.Timescale > 0 && movie.Duration is > 0 and not uint.MaxValue and not ulong.MaxValue)
        {
            return Milliseconds(movie.Duration, movie.Timescale);
        }

        // The fragment header: the whole length of a fragmented movie, when
        // the writer knew it.
        if (movie.Timescale > 0 && movie.FragmentDuration > 0)
        {
            return Milliseconds(movie.FragmentDuration, movie.Timescale);
        }

        // The picture track's own runs, added up.
        return video.Timescale > 0 && video.FragmentSampleDuration > 0
            ? Milliseconds(video.FragmentSampleDuration, video.Timescale)
            : null;
    }

    private static long Milliseconds(ulong units, uint timescale) =>
        (long)UInt128.Min((UInt128)units * 1000 / timescale, long.MaxValue);

    /// <summary>
    /// Walks the boxes between <paramref name="start"/> and <paramref name="end"/>,
    /// recording what matters into <paramref name="movie"/>. False when the
    /// structure is malformed.
    /// </summary>
    private static bool Walk(ReadOnlySpan<byte> bytes, int start, int end, int depth, Movie movie, Track? track = null, Fragment? fragment = null)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        var position = start;

        while (position + 8 <= end)
        {
            var size = (long)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(position, 4));
            var type = bytes.Slice(position + 4, 4);
            var header = 8;

            if (size == 1)
            {
                // A 64-bit size follows the type.
                if (position + 16 > end)
                {
                    return false;
                }

                size = (long)Math.Min(BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(position + 8, 8)), long.MaxValue);
                header = 16;
            }
            else if (size == 0)
            {
                // "Runs to the end of the file" — only the last box may say so.
                size = end - position;
            }

            if (size < header || position + size > end)
            {
                // A recorder stopped mid-write leaves a last box that claims
                // more than is there. Everything before it is still valid,
                // and the media data is never read anyway.
                return depth == 0 && type.SequenceEqual("mdat"u8) && movie.Tracks.Count > 0;
            }

            var body = bytes.Slice(position + header, (int)size - header);

            if (!Visit(bytes, type, position + header, (int)size - header, body, depth, movie, ref track, ref fragment))
            {
                return false;
            }

            position += (int)size;
        }

        return true;
    }

    private static bool Visit(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> type,
        int bodyStart,
        int bodyLength,
        ReadOnlySpan<byte> body,
        int depth,
        Movie movie,
        ref Track? track,
        ref Fragment? fragment)
    {
        var bodyEnd = bodyStart + bodyLength;

        // Containers whose children say what the movie is.
        if (type.SequenceEqual("moov"u8) || type.SequenceEqual("mvex"u8)
            || type.SequenceEqual("mdia"u8) || type.SequenceEqual("minf"u8) || type.SequenceEqual("stbl"u8))
        {
            return Walk(bytes, bodyStart, bodyEnd, depth + 1, movie, track, fragment);
        }

        if (type.SequenceEqual("trak"u8))
        {
            var added = new Track();
            movie.Tracks.Add(added);
            return Walk(bytes, bodyStart, bodyEnd, depth + 1, movie, added, fragment);
        }

        if (type.SequenceEqual("moof"u8))
        {
            return Walk(bytes, bodyStart, bodyEnd, depth + 1, movie, track, fragment);
        }

        if (type.SequenceEqual("traf"u8))
        {
            return Walk(bytes, bodyStart, bodyEnd, depth + 1, movie, track, new Fragment());
        }

        if (type.SequenceEqual("mvhd"u8))
        {
            return ReadMovieHeader(body, movie);
        }

        if (type.SequenceEqual("mehd"u8))
        {
            if (body.Length < 8)
            {
                return false;
            }

            movie.FragmentDuration = body[0] == 1 && body.Length >= 12
                ? BinaryPrimitives.ReadUInt64BigEndian(body.Slice(4, 8))
                : BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4, 4));
            return true;
        }

        if (type.SequenceEqual("trex"u8))
        {
            // version/flags(4) track_ID(4) description(4) duration(4) …
            if (body.Length < 16)
            {
                return false;
            }

            movie.DefaultDurations[BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4, 4))] =
                BinaryPrimitives.ReadUInt32BigEndian(body.Slice(12, 4));
            return true;
        }

        if (track is not null)
        {
            if (type.SequenceEqual("tkhd"u8))
            {
                // version/flags(4), then two times of 4 or 8 bytes, then the id.
                var at = body.Length > 0 && body[0] == 1 ? 20 : 12;

                if (body.Length < at + 4)
                {
                    return false;
                }

                track.Id = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(at, 4));
                return true;
            }

            if (type.SequenceEqual("mdhd"u8))
            {
                var at = body.Length > 0 && body[0] == 1 ? 20 : 12;

                if (body.Length < at + 4)
                {
                    return false;
                }

                track.Timescale = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(at, 4));
                return true;
            }

            if (type.SequenceEqual("hdlr"u8))
            {
                // version/flags(4) pre_defined(4) handler_type(4).
                if (body.Length < 12)
                {
                    return false;
                }

                track.Handler = System.Text.Encoding.ASCII.GetString(body.Slice(8, 4));
                return true;
            }

            if (type.SequenceEqual("stsd"u8))
            {
                return ReadSampleDescription(body, track);
            }
        }

        if (fragment is not null)
        {
            if (type.SequenceEqual("tfhd"u8))
            {
                return ReadFragmentHeader(body, fragment);
            }

            if (type.SequenceEqual("trun"u8))
            {
                return ReadTrackRun(body, fragment, movie);
            }
        }

        // Everything else — the media itself, edit lists, metadata — says
        // nothing about what the clip is, and is skipped unread.
        return true;
    }

    private static bool ReadMovieHeader(ReadOnlySpan<byte> body, Movie movie)
    {
        if (body.Length < 20)
        {
            return false;
        }

        if (body[0] == 1)
        {
            // version/flags(4) creation(8) modification(8) timescale(4) duration(8).
            if (body.Length < 32)
            {
                return false;
            }

            movie.Timescale = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(20, 4));
            movie.Duration = BinaryPrimitives.ReadUInt64BigEndian(body.Slice(24, 8));
            return true;
        }

        // version/flags(4) creation(4) modification(4) timescale(4) duration(4).
        movie.Timescale = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(12, 4));
        movie.Duration = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(16, 4));
        return true;
    }

    /// <summary>
    /// The first sample entry names the codec; for a picture track it also
    /// carries the frame size.
    /// </summary>
    private static bool ReadSampleDescription(ReadOnlySpan<byte> body, Track track)
    {
        // version/flags(4) entry_count(4), then the entries, each a box.
        if (body.Length < 16)
        {
            return false;
        }

        var entrySize = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(8, 4));

        if (entrySize < 8 || 8 + entrySize > body.Length)
        {
            return false;
        }

        var entry = body.Slice(8, (int)entrySize);
        track.Codec = System.Text.Encoding.ASCII.GetString(entry.Slice(4, 4));

        // A visual sample entry: header(8) reserved(6) reference(2)
        // pre_defined/reserved(16), then width(2) and height(2).
        if (track.IsVideo && entry.Length >= 36)
        {
            track.Width = BinaryPrimitives.ReadUInt16BigEndian(entry.Slice(32, 2));
            track.Height = BinaryPrimitives.ReadUInt16BigEndian(entry.Slice(34, 2));
        }

        return true;
    }

    private static bool ReadFragmentHeader(ReadOnlySpan<byte> body, Fragment fragment)
    {
        if (body.Length < 8)
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadUInt32BigEndian(body[..4]) & 0x00FFFFFF;
        fragment.TrackId = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4, 4));

        var at = 8;

        if ((flags & 0x01) != 0)
        {
            at += 8; // base data offset
        }

        if ((flags & 0x02) != 0)
        {
            at += 4; // sample description index
        }

        if ((flags & 0x08) != 0)
        {
            if (body.Length < at + 4)
            {
                return false;
            }

            fragment.DefaultDuration = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(at, 4));
        }

        return true;
    }

    /// <summary>Adds one run's sample durations to the track it belongs to.</summary>
    private static bool ReadTrackRun(ReadOnlySpan<byte> body, Fragment fragment, Movie movie)
    {
        if (body.Length < 8)
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadUInt32BigEndian(body[..4]) & 0x00FFFFFF;
        var count = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4, 4));
        var at = 8;

        if ((flags & 0x001) != 0)
        {
            at += 4; // data offset
        }

        if ((flags & 0x004) != 0)
        {
            at += 4; // first sample flags
        }

        var hasDuration = (flags & 0x100) != 0;
        var stride = (hasDuration ? 4 : 0)
            + ((flags & 0x200) != 0 ? 4 : 0)
            + ((flags & 0x400) != 0 ? 4 : 0)
            + ((flags & 0x800) != 0 ? 4 : 0);

        if (stride > 0 && (long)count * stride > body.Length - at)
        {
            return false;
        }

        var track = movie.Tracks.FirstOrDefault(candidate => candidate.Id == fragment.TrackId);

        if (track is null)
        {
            return true;
        }

        if (!hasDuration)
        {
            var each = fragment.DefaultDuration
                ?? (movie.DefaultDurations.TryGetValue(fragment.TrackId, out var fallback) ? fallback : 0);
            track.FragmentSampleDuration += (ulong)count * each;
            return true;
        }

        for (var sample = 0; sample < count; sample++)
        {
            track.FragmentSampleDuration += BinaryPrimitives.ReadUInt32BigEndian(body.Slice(at + (sample * stride), 4));
        }

        return true;
    }

    private sealed class Movie
    {
        public uint Timescale { get; set; }

        public ulong Duration { get; set; }

        public ulong FragmentDuration { get; set; }

        public List<Track> Tracks { get; } = [];

        /// <summary>Per track, the sample duration a fragment falls back to.</summary>
        public Dictionary<uint, uint> DefaultDurations { get; } = [];
    }

    private sealed class Track
    {
        public uint Id { get; set; }

        public uint Timescale { get; set; }

        public string Handler { get; set; } = string.Empty;

        public string Codec { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public ulong FragmentSampleDuration { get; set; }

        public bool IsVideo => Handler == "vide";

        public bool IsAudio => Handler == "soun";
    }

    private sealed class Fragment
    {
        public uint TrackId { get; set; }

        public uint? DefaultDuration { get; set; }
    }
}
