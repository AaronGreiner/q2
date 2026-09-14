using Q2.Api.Features.Notifications;

namespace Q2.Api.UnitTests.Notifications;

/// <summary>
/// How the bell turns rows into lines, and what a row may be.
/// </summary>
[Trait("Category", "Unit")]
public class InboxLinesTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Me = new("11111111-0000-4000-8000-000000000001");
    private static readonly Guid Lena = new("11111111-0000-4000-8000-000000000002");
    private static readonly Guid Jonas = new("11111111-0000-4000-8000-000000000003");
    private static readonly Guid Goal = new("22222222-0000-4000-8000-000000000001");
    private static readonly Guid OtherGoal = new("22222222-0000-4000-8000-000000000002");

    private static int _sequence;

    [Fact]
    public void PeopleReactingToTheSameThingAreOneLineNamingTheLatest()
    {
        var lines = InboxLines.Group(
            [
                Reaction(Jonas, Goal, minutesAgo: 5),
                Reaction(Lena, Goal, minutesAgo: 10),
            ],
            seenBefore: DateTimeOffset.MinValue);

        var line = Assert.Single(lines);

        Assert.Equal(Jonas, line.Latest.ActorPersonId);
        Assert.Equal([Jonas, Lena], line.People);
        Assert.True(line.IsNew);
    }

    [Fact]
    public void OnePersonReactingTwiceIsOnePerson()
    {
        var lines = InboxLines.Group(
            [
                Reaction(Lena, Goal, minutesAgo: 5),
                Reaction(Lena, Goal, minutesAgo: 9),
            ],
            seenBefore: DateTimeOffset.MinValue);

        Assert.Equal([Lena], Assert.Single(lines).People);
    }

    [Fact]
    public void ReactionsToDifferentThingsStayApart()
    {
        var lines = InboxLines.Group(
            [
                Reaction(Lena, Goal, minutesAgo: 5),
                Reaction(Lena, OtherGoal, minutesAgo: 9),
            ],
            seenBefore: DateTimeOffset.MinValue);

        Assert.Equal(2, lines.Count);
    }

    /// <summary>
    /// "und 4 weitere" must not count people somebody has already been told
    /// about as if they were news.
    /// </summary>
    [Fact]
    public void WhatIsNewIsNeverMergedWithWhatWasSeen()
    {
        var lines = InboxLines.Group(
            [
                Reaction(Jonas, Goal, minutesAgo: 5),
                Reaction(Lena, Goal, minutesAgo: 60),
            ],
            seenBefore: Now.AddMinutes(-30));

        Assert.Collection(
            lines,
            line => Assert.True(line.IsNew),
            line => Assert.False(line.IsNew));
    }

    [Fact]
    public void EverythingButAReactionIsALineOfItsOwn()
    {
        var lines = InboxLines.Group(
            [
                Line(NotificationKind.FriendshipStarted, Lena, NotificationTarget.Person, Lena, minutesAgo: 5),
                Line(NotificationKind.FriendshipStarted, Jonas, NotificationTarget.Person, Jonas, minutesAgo: 6),
                Line(NotificationKind.ProofConfirmed, null, NotificationTarget.Goal, Goal, minutesAgo: 7),
            ],
            seenBefore: DateTimeOffset.MinValue);

        Assert.Equal(3, lines.Count);
        Assert.Empty(lines[2].People);
    }

    [Fact]
    public void AKindTheBellDoesNotKeepCannotBecomeALine()
    {
        var message = new NotificationEvent(NotificationKind.MessageReceived, Lena, NotificationTarget.Conversation, Goal);

        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Me, message, Now));
    }

    [Fact]
    public void ALongSubjectIsShortenedRatherThanRefused()
    {
        var line = Notification.Create(
            Guid.NewGuid(),
            Me,
            new NotificationEvent(NotificationKind.GoalInvitation, Lena, NotificationTarget.Goal, Goal, new string('x', 400)),
            Now);

        Assert.Equal(Notification.MaxSubjectLength, line.Subject!.Length);
        Assert.EndsWith("…", line.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingToSayIsNull() =>
        Assert.Null(Notification.Shorten("   ", Notification.MaxSubjectLength));

    private static Notification Reaction(Guid actor, Guid target, int minutesAgo) =>
        Line(NotificationKind.ReactionReceived, actor, NotificationTarget.Goal, target, minutesAgo);

    private static Notification Line(
        NotificationKind kind,
        Guid? actor,
        NotificationTarget target,
        Guid targetId,
        int minutesAgo) =>
        Notification.Create(
            new Guid($"33333333-0000-4000-8000-{Interlocked.Increment(ref _sequence):D12}"),
            Me,
            new NotificationEvent(kind, actor, target, targetId),
            Now.AddMinutes(-minutesAgo));
}
