using Q2.Api.Features.Challenges;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Challenges;

/// <summary>
/// The queue that makes a daily editorial shift unnecessary.
/// </summary>
/// <remarks>
/// Two properties are what the worker relies on and what these tests are for:
/// running it twice does nothing the second time, and which prompt a day gets
/// depends on the date rather than on how many passes there have been. Both
/// have to hold after a host has been down for a week.
/// </remarks>
public class ChallengeQueueTests
{
    private static readonly LocalCalendar Berlin = new(
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin") is { } zone ? zone : TimeZoneInfo.Utc);

    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly ChallengePrompt[] Prompts =
    [
        new() { German = "eins", English = "one" },
        new() { German = "zwei", English = "two" },
        new() { German = "drei", English = "three" },
    ];

    [Fact]
    public void ItWritesOneRowPerDayUpToTheHorizon()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        Assert.Equal(7, planned.Count);
        Assert.Equal(Today, planned[0].Day);
        Assert.Equal(Today.AddDays(6), planned[^1].Day);
    }

    /// <summary>
    /// Each day runs from its moment to local midnight (#136): it appears
    /// between 10:00 and 20:00, ends where the next day begins, and no two
    /// days ever overlap.
    /// </summary>
    [Fact]
    public void EachDayAppearsAtItsMomentAndEndsAtMidnight()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        foreach (var day in planned)
        {
            var appears = Berlin.TimeOf(day.PublishedAt);

            Assert.InRange(appears, new TimeOnly(10, 0), new TimeOnly(19, 59));
            Assert.Equal(day.Day, Berlin.DayOf(day.PublishedAt));
            Assert.Equal(Berlin.StartOfDay(day.Day.AddDays(1)), day.ExpiresAt);
            Assert.Equal(day.PublishedAt + ChallengeQueue.MomentLength, day.MomentEndsAt);
        }

        for (var index = 1; index < planned.Count; index++)
        {
            Assert.True(planned[index - 1].ExpiresAt <= planned[index].PublishedAt);
        }
    }

    /// <summary>
    /// The moment is drawn from the date alone, so planning a day twice — or
    /// after a week down — gives it the same one, and the days do not share it.
    /// </summary>
    [Fact]
    public void TheMomentIsTheDatesAndNoOneElses()
    {
        Assert.Equal(ChallengeQueue.MomentFor(Today), ChallengeQueue.MomentFor(Today));

        var year = Enumerable.Range(0, 365).Select(offset => ChallengeQueue.MomentFor(Today.AddDays(offset))).ToList();

        Assert.All(year, moment => Assert.InRange(moment, new TimeOnly(10, 0), new TimeOnly(19, 59)));

        // Spread over the window rather than clumped: every hour gets days.
        Assert.Equal(10, year.Select(moment => moment.Hour).Distinct().Count());
        Assert.True(year.Distinct().Count() > 200);
    }

    [Fact]
    public void DaysThatAlreadyHaveOneAreLeftAlone()
    {
        var planned = ChallengeQueue.Plan(
            Prompts,
            [Today, Today.AddDays(2)],
            Today,
            horizonDays: 4,
            Berlin);

        Assert.Equal([Today.AddDays(1), Today.AddDays(3)], planned.Select(item => item.Day));
    }

    /// <summary>
    /// The idempotence the worker depends on: feed a pass its own output and
    /// there is nothing left to do.
    /// </summary>
    [Fact]
    public void RunningItTwiceQueuesNothingTheSecondTime()
    {
        var first = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        var second = ChallengeQueue.Plan(
            Prompts,
            [.. first.Select(item => item.Day)],
            Today,
            horizonDays: 7,
            Berlin);

        Assert.Empty(second);
    }

    /// <summary>
    /// A week of downtime is caught up in one pass, and the days that were
    /// missed entirely are not invented after the fact — a prompt nobody was
    /// ever offered has no business in anybody's archive.
    /// </summary>
    [Fact]
    public void ItCatchesUpForwardsAndNeverBackwards()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        Assert.DoesNotContain(planned, item => item.Day < Today);
        Assert.Equal(7, planned.Count);
    }

    /// <summary>
    /// Which prompt a day gets is a function of the date. That is what makes
    /// re-planning stable, and what stops an added prompt from rewriting a day
    /// that is already queued.
    /// </summary>
    [Fact]
    public void APromptBelongsToItsDayAndNotToItsTurn()
    {
        var expected = ChallengeQueue.PromptFor(Prompts, Today.AddDays(4));

        var planned = ChallengeQueue.Plan(
            Prompts,
            [Today, Today.AddDays(1), Today.AddDays(2), Today.AddDays(3)],
            Today,
            horizonDays: 5,
            Berlin);

        Assert.Equal(expected, Assert.Single(planned).Prompt);
    }

    [Fact]
    public void ThePromptsRotateSoTheQueueNeverRunsDry()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        Assert.All(planned, item => Assert.Contains(item.Prompt, Prompts));
        Assert.Equal(3, planned.Select(item => item.Prompt).Distinct().Count());
    }

    /// <summary>A prompt with a half missing stops the host from starting, not a queue pass at night.</summary>
    [Fact]
    public void TheConfigurationIsCompleteOnlyWithBothLanguages()
    {
        Assert.True(new ChallengeOptions { Prompts = Prompts }.IsComplete());
        Assert.True(new ChallengeOptions().IsComplete());
        Assert.False(new ChallengeOptions { Prompts = [new() { German = "Zeig deinen Schreibtisch." }] }.IsComplete());
        Assert.False(new ChallengeOptions { Prompts = [new() { English = "Show your desk." }] }.IsComplete());
    }

    /// <summary>No prompts configured is "no daily challenge", not a failure.</summary>
    [Fact]
    public void WithoutPromptsNothingIsQueued()
    {
        Assert.Empty(ChallengeQueue.Plan([], [], Today, horizonDays: 7, Berlin));
        Assert.Empty(ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 0, Berlin));
    }
}
