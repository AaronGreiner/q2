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

    private static readonly string[] Prompts = ["eins", "zwei", "drei"];

    [Fact]
    public void ItWritesOneRowPerDayUpToTheHorizon()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 7, Berlin);

        Assert.Equal(7, planned.Count);
        Assert.Equal(Today, planned[0].Day);
        Assert.Equal(Today.AddDays(6), planned[^1].Day);
    }

    /// <summary>
    /// Each day runs from local midnight to local midnight, and the end of one
    /// is exactly the start of the next — so no instant falls between two
    /// challenges and none is covered by both.
    /// </summary>
    [Fact]
    public void TheDaysTileWithoutOverlapping()
    {
        var planned = ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 3, Berlin);

        Assert.Equal(Berlin.StartOfDay(Today), planned[0].PublishedAt);

        for (var index = 1; index < planned.Count; index++)
        {
            Assert.Equal(planned[index - 1].ExpiresAt, planned[index].PublishedAt);
        }
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

    /// <summary>No prompts configured is "no daily challenge", not a failure.</summary>
    [Fact]
    public void WithoutPromptsNothingIsQueued()
    {
        Assert.Empty(ChallengeQueue.Plan([], [], Today, horizonDays: 7, Berlin));
        Assert.Empty(ChallengeQueue.Plan(Prompts, [], Today, horizonDays: 0, Berlin));
    }
}
