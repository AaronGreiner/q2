using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// What a goal's conversation shows between its messages, read off the goal.
/// </summary>
/// <remarks>
/// Pure: every instant and every id is passed in, and nothing is stored — the
/// timeline is whatever the windows, photographs and pauses already say.
/// </remarks>
public class GoalTimelineTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    private static readonly Guid Friend = new("22222222-2222-4222-8222-222222222222");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static readonly DateTimeOffset Noon = Utc.StartOfDay(Today).AddHours(12);

    [Fact]
    public void ANewGoalBeginsWithItsCreationByItsOwner()
    {
        var entry = Assert.Single(GoalTimeline.For(Build(), Noon));

        Assert.Equal(GoalEventKind.Created, entry.Kind);
        Assert.Equal(Created, entry.At);
        Assert.Equal(Owner, entry.ActorPersonId);
    }

    [Fact]
    public void KeptWindowsCarryTheStreakTheyReachedAndAMissEndsIt()
    {
        var goal = Build();
        Keep(goal, Today.AddDays(-4));
        Keep(goal, Today.AddDays(-3));
        Miss(goal, Today.AddDays(-2));
        Keep(goal, Today.AddDays(-1));

        var windows = GoalTimeline.For(goal, Noon)
            .Where(entry => entry.Kind is GoalEventKind.WindowDone or GoalEventKind.WindowMissed)
            .ToList();

        Assert.Equal(
            [GoalEventKind.WindowDone, GoalEventKind.WindowDone, GoalEventKind.WindowMissed, GoalEventKind.WindowDone],
            windows.Select(entry => entry.Kind));
        Assert.Equal([1, 2, null, 1], windows.Select(entry => entry.Streak));

        // The last streak on the timeline is the streak the goal shows.
        Assert.Equal(goal.Streak, windows[^1].Streak);

        // Nobody did a window's outcome; time or a vote decided it.
        Assert.All(windows, entry => Assert.Null(entry.ActorPersonId));
    }

    [Fact]
    public void APhotographComesWithItselfAndBeforeTheWindowItKept()
    {
        var goal = Build();
        Open(goal, Today);

        var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), capturedInApp: true, Noon)!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Noon.AddHours(1));

        var timeline = GoalTimeline.For(goal, Noon.AddHours(2));

        Assert.Equal(
            [GoalEventKind.Created, GoalEventKind.ProofDelivered, GoalEventKind.WindowDone],
            timeline.Select(entry => entry.Kind));
        Assert.Same(proof, timeline[1].Proof);
        Assert.Equal(Owner, timeline[1].ActorPersonId);
    }

    [Fact]
    public void AMissedWindowSaysHowFarItGot()
    {
        var goal = Build(GoalSchedule.TimesPer(3, QuotaPeriod.Week));
        var window = GoalSchedule.TimesPer(3, QuotaPeriod.Week).FirstWindow(Today.AddDays(-7), null);
        var instance = goal.OpenWindow(Guid.CreateVersion7(), window, Utc.StartOfDay(window.Start), Utc.EndOfDay(window.End))!;
        var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), capturedInApp: true, Utc.StartOfDay(window.Start).AddHours(9))!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Utc.StartOfDay(window.Start).AddHours(10));
        instance.Miss(Utc.EndOfDay(window.End).AddTicks(1));

        var missed = GoalTimeline.For(goal, Noon).Single(entry => entry.Kind == GoalEventKind.WindowMissed);

        Assert.Equal(1, missed.ConfirmedProofs);
        Assert.Equal(3, missed.RequiredProofs);
    }

    [Fact]
    public void APauseSaysUntilWhenAndItsEndHasNobodyToName()
    {
        var goal = Build(friends: 2);
        var pause = goal.RequestPause(Guid.CreateVersion7(), "Grippe, seit Freitag im Bett.", 3, Today, Utc.EndOfDay, Noon);

        goal.VetoPause(Guid.CreateVersion7(), goal.Participants[0].PersonId, Noon.AddHours(1));
        goal.VetoPause(Guid.CreateVersion7(), goal.Participants[1].PersonId, Noon.AddHours(2));

        var timeline = GoalTimeline.For(goal, Noon.AddHours(3));
        var started = timeline.Single(entry => entry.Kind == GoalEventKind.PauseStarted);
        var lifted = timeline.Single(entry => entry.Kind == GoalEventKind.PauseOverturned);

        Assert.Equal(pause.EndsOn, started.Until);
        Assert.Equal(Owner, started.ActorPersonId);

        // Objections are anonymous, and so is what they add up to.
        Assert.Null(lifted.ActorPersonId);
        Assert.NotEqual(started.Key, lifted.Key);
    }

    [Fact]
    public void APauseThatRanOutIsOverEvenBeforeAnybodyWroteItDown()
    {
        var goal = Build();
        goal.RequestPause(Guid.CreateVersion7(), "Urlaub, ohne Laufschuhe.", 1, Today, Utc.EndOfDay, Noon);

        var during = GoalTimeline.For(goal, Noon.AddHours(1));
        var after = GoalTimeline.For(goal, Utc.StartOfDay(Today.AddDays(2)));

        Assert.DoesNotContain(during, entry => entry.Kind == GoalEventKind.PauseEnded);
        Assert.Contains(after, entry => entry.Kind == GoalEventKind.PauseEnded);
    }

    [Theory]
    [InlineData(true, GoalEventKind.Completed)]
    [InlineData(false, GoalEventKind.Stopped)]
    public void AClosedGoalEndsWithHowItEnded(bool completed, GoalEventKind expected)
    {
        var goal = Build();
        goal.Close(completed, Noon);

        Assert.Equal(expected, GoalTimeline.For(goal, Noon.AddHours(1))[^1].Kind);
    }

    [Fact]
    public void KeysAreUniqueAndTheOrderIsByTime()
    {
        var goal = Build(friends: 2);
        Keep(goal, Today.AddDays(-2));
        Miss(goal, Today.AddDays(-1));
        goal.RequestPause(Guid.CreateVersion7(), "Grippe, seit Freitag im Bett.", 1, Today, Utc.EndOfDay, Noon);
        goal.Close(completed: false, Noon.AddDays(3));

        var timeline = GoalTimeline.For(goal, Noon.AddDays(4));

        Assert.Equal(timeline.Count, timeline.Select(entry => entry.Key).Distinct().Count());
        Assert.Equal(timeline.OrderBy(entry => entry.At).Select(entry => entry.At), timeline.Select(entry => entry.At));
    }

    private static Goal Build(GoalSchedule? schedule = null, int friends = 1)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Jeden Tag lesen",
            null,
            null,
            schedule ?? GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            Created);

        goal.AddParticipant(Guid.CreateVersion7(), Friend);

        for (var extra = 1; extra < friends; extra++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        return goal;
    }

    private static GoalInstance Open(Goal goal, DateOnly day)
    {
        var window = GoalSchedule.EveryNDays(1).FirstWindow(day, day);

        return goal.OpenWindow(Guid.CreateVersion7(), window, Utc.StartOfDay(window.Start), Utc.EndOfDay(window.End))!;
    }

    private static void Keep(Goal goal, DateOnly day)
    {
        Open(goal, day);

        var at = Utc.StartOfDay(day).AddHours(9);
        var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), capturedInApp: true, at)!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, at.AddHours(1));
    }

    private static void Miss(Goal goal, DateOnly day) =>
        Open(goal, day).Miss(Utc.EndOfDay(day).AddTicks(1));
}
