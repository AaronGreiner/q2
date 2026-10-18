using System.Globalization;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Chats;

/// <summary>
/// A place in a thread: when a line happened, and which line it was at that
/// instant.
/// </summary>
/// <remarks>
/// A thread is two lists read in one order. What people wrote is ordered by
/// <c>(SentAt, Id)</c>; what happened to the goal is ordered the way
/// <see cref="GoalTimeline"/> orders it, by kind and then by the row it was
/// read from. At the same instant a line about the goal comes before a message
/// — a photograph arrives before anybody can answer it — which is also how the
/// client lays the two side by side (app/app/utils/chatTimeline.ts).
///
/// A page ends at a position rather than at an instant. Two lines with the same
/// timestamp may then end up on two pages, and each still turns up exactly once
/// and in the order one long thread would have them in, rather than twice or
/// not at all ([#44](https://github.com/AaronGreiner/q2/issues/44)).
/// </remarks>
public readonly record struct ThreadPosition(DateTimeOffset At, bool IsMessage, GoalEventKind Kind, Guid Id)
    : IComparable<ThreadPosition>
{
    private const string MessageLine = "Message";

    public static ThreadPosition Of(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new(message.SentAt, IsMessage: true, default, message.Id);
    }

    public static ThreadPosition Of(GoalEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(entry.At, IsMessage: false, entry.Kind, entry.SourceId);
    }

    public int CompareTo(ThreadPosition other)
    {
        var byTime = At.UtcTicks.CompareTo(other.At.UtcTicks);

        if (byTime != 0)
        {
            return byTime;
        }

        if (IsMessage != other.IsMessage)
        {
            return IsMessage ? 1 : -1;
        }

        // Only lines about the goal have a kind; every message has the default.
        var byKind = Kind.CompareTo(other.Kind);

        return byKind != 0 ? byKind : Id.CompareTo(other.Id);
    }

    /// <summary>
    /// The position as the client carries it: opaque to it, readable in a log.
    /// </summary>
    /// <remarks>
    /// Ticks rather than a formatted instant, because a page boundary has to
    /// be exact to the tick the database stores — a millisecond-precision
    /// timestamp would put two lines a few microseconds apart on the same side.
    /// </remarks>
    public string ToCursor() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{At.UtcTicks}.{(IsMessage ? MessageLine : Kind.ToString())}.{Id:N}");

    /// <exception cref="DomainValidationException">
    /// <paramref name="cursor"/> is not one this API handed out.
    /// </exception>
    public static ThreadPosition Parse(string? cursor, string field)
    {
        var parts = cursor?.Split('.') ?? [];

        if (parts.Length == 3
            && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            && ticks >= DateTimeOffset.MinValue.UtcTicks
            && ticks <= DateTimeOffset.MaxValue.UtcTicks
            && Guid.TryParseExact(parts[2], "N", out var id))
        {
            var at = new DateTimeOffset(ticks, TimeSpan.Zero);

            if (parts[1] == MessageLine)
            {
                return new ThreadPosition(at, IsMessage: true, default, id);
            }

            // One name exactly: Enum.TryParse would also take a number, or
            // several names joined by commas.
            if (Enum.GetNames<GoalEventKind>().Contains(parts[1], StringComparer.Ordinal))
            {
                return new ThreadPosition(at, IsMessage: false, Enum.Parse<GoalEventKind>(parts[1]), id);
            }
        }

        throw new DomainValidationException(field, "That is not a place in a conversation.");
    }
}

/// <summary>
/// One page of a thread, oldest line first, and where the page before it ends.
/// </summary>
/// <remarks>
/// Messages are read from the database a page at a time; the goal's lines are
/// derived from the goal in memory (<see cref="GoalTimeline"/>), so all of them
/// are at hand and only the ones that fall on the page are sent. Cutting both
/// from one ordered sequence is what makes them interleave the same way on
/// every page as they would in one long thread.
/// </remarks>
/// <param name="Older">
/// The oldest line on this page, which the next page is read before. Null when
/// this page begins the thread.
/// </param>
public sealed record ThreadPage(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<GoalEvent> Events,
    ThreadPosition? Older)
{
    /// <summary>
    /// How many lines a page holds. Each may be a photograph, which is a read
    /// of its own on a phone; a screen and a half of them is enough to scroll
    /// through while the next page arrives.
    /// </summary>
    public const int Size = 30;

    /// <summary>
    /// Cuts the page that ends just before <paramref name="before"/>, or the
    /// newest page without one.
    /// </summary>
    /// <param name="messages">
    /// The newest <c>size + 1</c> messages before <paramref name="before"/> —
    /// or all of them, when there are fewer. One more than fits, so that a
    /// full page can tell whether anything is left before it.
    /// </param>
    /// <param name="timeline">The goal's lines, all of them, in any order.</param>
    public static ThreadPage Cut(
        IEnumerable<ChatMessage> messages,
        IEnumerable<GoalEvent> timeline,
        ThreadPosition? before,
        int size = Size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var lines = messages
            .Select(message => new Line(ThreadPosition.Of(message), message, null))
            .Concat(timeline.Select(entry => new Line(ThreadPosition.Of(entry), null, entry)))
            .Where(line => before is not { } end || line.Position.CompareTo(end) < 0)
            .OrderByDescending(line => line.Position)
            .ToList();

        var page = lines.Take(size).Reverse().ToList();

        return new ThreadPage(
            [.. page.Select(line => line.Message).OfType<ChatMessage>()],
            [.. page.Select(line => line.Event).OfType<GoalEvent>()],
            lines.Count > size ? page[0].Position : null);
    }

    private sealed record Line(ThreadPosition Position, ChatMessage? Message, GoalEvent? Event);
}
