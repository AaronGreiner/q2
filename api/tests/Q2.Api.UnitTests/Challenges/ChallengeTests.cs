using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Challenges;

/// <summary>
/// The rules of the daily challenge that need no database.
/// </summary>
/// <remarks>
/// Two of them are the whole feature: one contribution per person, and nobody
/// sees the room until they are in it. Both are answered by
/// <see cref="Challenge"/> itself, which is why they can be stated here in a
/// line each rather than through a pipeline.
/// </remarks>
public class ChallengeTests
{
    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly DateTimeOffset Noon = Utc.StartOfDay(Today).AddHours(12);

    private static readonly Guid Mara = new("11111111-1111-4111-8111-111111111111");

    private static readonly Guid Jonas = new("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void ItRunsFromLocalMidnightToLocalMidnight()
    {
        var challenge = Build();

        Assert.False(challenge.IsActiveAt(Utc.StartOfDay(Today).AddTicks(-1)));
        Assert.True(challenge.IsActiveAt(Utc.StartOfDay(Today)));
        Assert.True(challenge.IsActiveAt(Noon));

        // Exclusive: the moment the next day starts belongs to the next
        // challenge, and no instant falls between the two.
        Assert.False(challenge.IsActiveAt(Utc.StartOfDay(Today.AddDays(1))));
    }

    [Fact]
    public void APromptHasToBeSomethingSomebodyCanRead()
    {
        Assert.Throws<DomainValidationException>(() => Build(prompt: "  "));
        Assert.Throws<DomainValidationException>(() => Build(prompt: new string('x', Challenge.MaxPromptLength + 1)));

        // Trimmed rather than refused: trailing space in a settings file is
        // not somebody's mistake to be punished for.
        Assert.Equal("Zeig deinen Schreibtisch.", Build(prompt: "  Zeig deinen Schreibtisch.  ").Prompt);
    }

    /// <summary>
    /// The challenge is a moment, not a collection: a second picture replaces
    /// the first instead of adding to a gallery.
    /// </summary>
    [Fact]
    public void ASecondContributionReplacesTheFirst()
    {
        var challenge = Build();

        var first = challenge.Contribute(NewId(1), Mara, NewId(90), capturedInApp: true, Noon);
        var second = challenge.Contribute(NewId(2), Mara, NewId(91), capturedInApp: false, Noon.AddMinutes(5));

        Assert.Same(first, second);
        Assert.Equal(NewId(91), second.ImageId);
        Assert.False(second.CapturedInApp);
        Assert.Equal(Noon.AddMinutes(5), second.CreatedAt);
        Assert.Single(challenge.Entries);
    }

    /// <summary>
    /// The applause belongs to the picture it was given to. Carrying it over
    /// would leave somebody's "stark" standing under a photograph they never
    /// saw.
    /// </summary>
    [Fact]
    public void ReplacingAPictureDropsItsReactions()
    {
        var challenge = Build();
        var entry = challenge.Contribute(NewId(1), Mara, NewId(90), capturedInApp: true, Noon);
        entry.ToggleReaction(NewId(50), Jonas, KudosKind.Applause);

        challenge.Contribute(NewId(2), Mara, NewId(91), capturedInApp: true, Noon.AddMinutes(5));

        Assert.Empty(entry.Reactions);
    }

    [Fact]
    public void NothingIsTakenOnceTheDayIsOver()
    {
        var challenge = Build();

        Assert.Throws<DomainValidationException>(() =>
            challenge.Contribute(NewId(1), Mara, NewId(90), capturedInApp: true, Utc.StartOfDay(Today.AddDays(1))));
    }

    /// <summary>
    /// Reciprocity, in both directions. Without the hurdle the room would be a
    /// few people showing themselves to a silent majority.
    /// </summary>
    [Fact]
    public void TheRoomOpensWithYourOwnContributionAndClosesAgainWithoutIt()
    {
        var challenge = Build();
        challenge.Contribute(NewId(1), Jonas, NewId(90), capturedInApp: true, Noon);

        Assert.False(challenge.RevealsTo(Mara));

        challenge.Contribute(NewId(2), Mara, NewId(91), capturedInApp: true, Noon);
        Assert.True(challenge.RevealsTo(Mara));

        var withdrawn = challenge.Withdraw(Mara);

        Assert.NotNull(withdrawn);
        Assert.Equal(NewId(91), withdrawn.ImageId);
        Assert.False(challenge.RevealsTo(Mara));

        // Somebody else's is untouched by it.
        Assert.Single(challenge.Entries);
    }

    [Fact]
    public void WithdrawingWhatWasNeverThereChangesNothing()
    {
        var challenge = Build();

        Assert.Null(challenge.Withdraw(Mara));
        Assert.Empty(challenge.Entries);
    }

    /// <summary>The same behaviour kudos has: the same kind twice takes it back.</summary>
    [Fact]
    public void AReactionTogglesAndOnlyOneStands()
    {
        var challenge = Build();
        var entry = challenge.Contribute(NewId(1), Mara, NewId(90), capturedInApp: true, Noon);

        Assert.Equal(KudosKind.Fire, entry.ToggleReaction(NewId(50), Jonas, KudosKind.Fire));
        Assert.Equal(KudosKind.Applause, entry.ToggleReaction(NewId(51), Jonas, KudosKind.Applause));
        Assert.Single(entry.Reactions);

        Assert.Null(entry.ToggleReaction(NewId(52), Jonas, KudosKind.Applause));
        Assert.Empty(entry.Reactions);
    }

    private static Challenge Build(string prompt = "Zeig deinen Schreibtisch.") =>
        Challenge.Create(
            NewId(1),
            Today,
            prompt,
            Utc.StartOfDay(Today),
            Utc.StartOfDay(Today.AddDays(1)));

    private static Guid NewId(int index) => new($"33333333-3333-4333-8333-{index:D12}");
}
