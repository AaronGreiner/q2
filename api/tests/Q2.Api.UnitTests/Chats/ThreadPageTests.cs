using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Chats;

/// <summary>
/// How a thread is cut into pages: messages and the goal's lines along one
/// cursor, so that walking back page by page shows exactly the thread one long
/// read would have shown (#44).
/// </summary>
public class ThreadPageTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _me = Guid.CreateVersion7();
    private readonly Conversation _conversation;

    public ThreadPageTests()
    {
        _conversation = Conversation.CreateDirect(Guid.CreateVersion7(), Start);
        _conversation.AddParticipant(Guid.CreateVersion7(), _me);
    }

    [Fact]
    public void AThreadThatFitsOnOnePageHasNothingBeforeIt()
    {
        var messages = Write(3);

        var page = ThreadPage.Cut(Newest(messages, null, 5), [], before: null, size: 5);

        Assert.Equal(messages.Select(m => m.Id), page.Messages.Select(m => m.Id));
        Assert.Null(page.Older);
    }

    [Fact]
    public void AFullPageThatIsAllThereIsHasNothingBeforeIt()
    {
        var messages = Write(5);

        Assert.Null(ThreadPage.Cut(Newest(messages, null, 5), [], before: null, size: 5).Older);
    }

    [Fact]
    public void TheNewestPageEndsWithTheNewestLineAndPointsAtItsOldest()
    {
        var messages = Write(12);

        var page = ThreadPage.Cut(Newest(messages, null, 5), [], before: null, size: 5);

        Assert.Equal(messages.TakeLast(5).Select(m => m.Id), page.Messages.Select(m => m.Id));
        Assert.Equal(ThreadPosition.Of(messages[^5]), page.Older);
    }

    [Fact]
    public void WalkingBackPageByPageShowsEveryLineOnceInTheOrderOfOneLongThread()
    {
        // A day of a goal's conversation: lines and messages interleaved, two
        // of them at the very instant of a line about the goal, and two
        // messages sharing an instant with each other.
        var messages = new List<ChatMessage>();
        var timeline = new List<GoalEvent>
        {
            Line(GoalEventKind.Created, 0),
            Line(GoalEventKind.ProofDelivered, 10),
            Line(GoalEventKind.WindowDone, 10),
            Line(GoalEventKind.PauseStarted, 25),
            Line(GoalEventKind.PauseEnded, 40),
        };

        foreach (var minute in new[] { 1, 2, 10, 10, 11, 25, 26, 30, 30, 30, 41 })
        {
            messages.Add(_conversation.AddMessage(Guid.CreateVersion7(), _me, $"at {minute}", Start.AddMinutes(minute)));
        }

        var expected = ThreadPage.Cut(messages, timeline, before: null, size: int.MaxValue);
        Assert.Null(expected.Older);

        foreach (var size in new[] { 1, 2, 3, 4, 7 })
        {
            var walked = Walk(messages, timeline, size);

            Assert.Equal(Keys(expected.Messages, expected.Events), walked);
        }
    }

    [Fact]
    public void AtTheSameInstantALineAboutTheGoalComesBeforeAMessage()
    {
        // A photograph arrives before anybody can answer it — which is also
        // the order the client lays them out in at an equal timestamp.
        var message = _conversation.AddMessage(Guid.CreateVersion7(), _me, "Stark!", Start);
        var delivered = Line(GoalEventKind.ProofDelivered, 0);

        var newest = ThreadPage.Cut([message], [delivered], before: null, size: 1);

        // The message is the newest line, alone on its page; the photograph is
        // the whole of the page before it, and nothing is left after that.
        Assert.Equal([message.Id], newest.Messages.Select(m => m.Id));
        Assert.Empty(newest.Events);
        Assert.Equal(ThreadPosition.Of(message), newest.Older);

        var older = ThreadPage.Cut(Newest([message], newest.Older, 1), [delivered], newest.Older, size: 1);

        Assert.Empty(older.Messages);
        Assert.Equal([delivered.Key], older.Events.Select(entry => entry.Key));
        Assert.Null(older.Older);
    }

    [Fact]
    public void MessagesAtTheSameInstantAreSplitByTheirId()
    {
        var first = _conversation.AddMessage(new Guid("00000000-0000-7000-8000-000000000001"), _me, "a", Start);
        var second = _conversation.AddMessage(new Guid("00000000-0000-7000-8000-000000000002"), _me, "b", Start);

        var newest = ThreadPage.Cut(Newest([first, second], null, 1), [], before: null, size: 1);
        var older = ThreadPage.Cut(Newest([first, second], newest.Older, 1), [], newest.Older, size: 1);

        Assert.Equal([second.Id], newest.Messages.Select(m => m.Id));
        Assert.Equal([first.Id], older.Messages.Select(m => m.Id));
        Assert.Null(older.Older);
    }

    [Fact]
    public void LinesAboutTheGoalKeepTheTimelinesOwnOrderAtOneInstant()
    {
        // Kept on delivery: the photograph, then the window it kept.
        var kept = Line(GoalEventKind.WindowDone, 0);
        var delivered = Line(GoalEventKind.ProofDelivered, 0);

        var page = ThreadPage.Cut([], [kept, delivered], before: null);

        Assert.Equal([delivered.Key, kept.Key], page.Events.Select(entry => entry.Key));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACursorReadsBackAsThePlaceItWasMadeFrom(bool isMessage)
    {
        var position = new ThreadPosition(
            Start.AddTicks(1234567),
            isMessage,
            isMessage ? default : GoalEventKind.WindowDone,
            Guid.CreateVersion7());

        Assert.Equal(position, ThreadPosition.Parse(position.ToCursor(), "before"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("1.Message")]
    [InlineData("-1.Message.0198a1b2c3d47e5f8a9b0c1d2e3f4a5b")]
    [InlineData("1.Message.not-a-guid")]
    [InlineData("1.Lunch.0198a1b2c3d47e5f8a9b0c1d2e3f4a5b")]
    [InlineData("1.2.0198a1b2c3d47e5f8a9b0c1d2e3f4a5b")]
    [InlineData("1.Created,WindowDone.0198a1b2c3d47e5f8a9b0c1d2e3f4a5b")]
    [InlineData("99999999999999999999.Message.0198a1b2c3d47e5f8a9b0c1d2e3f4a5b")]
    public void ACursorThisApiNeverHandedOutIsAValidationProblem(string? cursor)
    {
        var error = Assert.Throws<DomainValidationException>(() => ThreadPosition.Parse(cursor, "before"));

        Assert.Contains("before", error.Errors.Keys);
    }

    private List<ChatMessage> Write(int count) =>
    [
        .. Enumerable.Range(0, count)
            .Select(minute => _conversation.AddMessage(Guid.CreateVersion7(), _me, $"{minute}", Start.AddMinutes(minute))),
    ];

    private static GoalEvent Line(GoalEventKind kind, int minute) =>
        new(kind, Start.AddMinutes(minute), Guid.CreateVersion7(), null);

    /// <summary>What the database is asked for: one more than a page, newest first.</summary>
    private static List<ChatMessage> Newest(IEnumerable<ChatMessage> messages, ThreadPosition? before, int size) =>
    [
        .. messages
            .Where(message => before is not { } end || ThreadPosition.Of(message).CompareTo(end) < 0)
            .OrderByDescending(message => ThreadPosition.Of(message))
            .Take(size + 1),
    ];

    private static List<string> Walk(IReadOnlyList<ChatMessage> messages, IReadOnlyList<GoalEvent> timeline, int size)
    {
        var pages = new List<ThreadPage>();
        ThreadPosition? before = null;

        do
        {
            var page = ThreadPage.Cut(Newest(messages, before, size), timeline, before, size);

            Assert.InRange(page.Messages.Count + page.Events.Count, 1, size);
            pages.Insert(0, page);
            before = page.Older;
        }
        while (before is not null);

        return [.. pages.SelectMany(page => Keys(page.Messages, page.Events))];
    }

    /// <summary>One thread's lines in order, as the client merges a page's two lists.</summary>
    private static List<string> Keys(IReadOnlyList<ChatMessage> messages, IReadOnlyList<GoalEvent> events) =>
    [
        .. messages.Select(message => (Position: ThreadPosition.Of(message), Key: $"message:{message.Id}"))
            .Concat(events.Select(entry => (Position: ThreadPosition.Of(entry), Key: $"event:{entry.Key}")))
            .OrderBy(line => line.Position)
            .Select(line => line.Key),
    ];
}
