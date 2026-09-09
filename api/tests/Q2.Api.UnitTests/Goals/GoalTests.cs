using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// The rules a goal enforces about itself.
/// </summary>
/// <remarks>
/// Pure unit tests: no database, no HTTP, no clock. Every instant and every id
/// is passed in, which is exactly what makes them possible.
/// </remarks>
public class GoalTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Whoever it belongs to. Fixed, so a failure names the same id twice.</summary>
    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);


    /// <summary>
    /// Delivers a photograph and has it believed — the only way a window moves
    /// now that a proof is a picture other people vote on.
    /// </summary>
    /// <remarks>
    /// Written out rather than hidden behind a fixture: these tests are about
    /// what a window does with a *confirmed* proof, and the two steps are
    /// exactly the two things that have to happen for one to count.
    /// </remarks>
    private static bool Deliver(Goal goal, DateTimeOffset now)
    {
        var proof = goal.SubmitProof(Guid.NewGuid(), Guid.NewGuid(), capturedInApp: true, now);

        return proof is not null && goal.ApplyProofOutcome(proof, VotingResult.Confirmed, now);
    }

    [Fact]
    public void ATitleIsRequiredAndIsTrimmed()
    {
        var goal = Build(title: "  Read every day  ");

        Assert.Equal("Read every day", goal.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTitleIsRejected(string title)
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(title: title));

        Assert.Contains(nameof(Goal.Title), error.Errors.Keys);
    }

    [Fact]
    public void ATitleLongerThanTheLimitIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Build(title: new string('a', Goal.MaxTitleLength + 1)));

        Assert.Contains(nameof(Goal.Title), error.Errors.Keys);
    }

    [Fact]
    public void AnUnknownIconIsRejectedAndAMissingOneFallsBack()
    {
        Assert.Throws<DomainValidationException>(() => Build(icon: "not-an-icon"));
        Assert.Equal(GoalIcons.Default, Build(icon: null).Icon);
    }

    [Fact]
    public void AGoalNeedsSomebodyItBelongsTo()
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), Guid.Empty, "Anything", null, "target",
            GoalSchedule.EveryNDays(1), false, null, null, Created));

        Assert.Contains(nameof(Goal.OwnerPersonId), error.Errors.Keys);
    }

    /// <summary>
    /// A repeating goal derives every day it is due, so a target date on one
    /// would be a second answer to the same question.
    /// </summary>
    [Fact]
    public void ATargetDateOnlyStaysOnAOneOff()
    {
        var due = Today.AddDays(10);

        Assert.Equal(due, Build(schedule: GoalSchedule.Once(), targetDate: due).TargetDate);
        Assert.Null(Build(schedule: GoalSchedule.EveryNDays(1), targetDate: due).TargetDate);
    }

    [Fact]
    public void OnlyOneWindowIsEverOpen()
    {
        var goal = Build();
        var first = Open(goal, Today);

        Assert.NotNull(first);
        Assert.Null(Open(goal, Today.AddDays(1)));
        Assert.Single(goal.Instances);
    }

    /// <summary>
    /// The property that makes the maintenance job safe to run twice: the same
    /// window cannot be opened again even after the first one has closed.
    /// </summary>
    [Fact]
    public void TheSameWindowIsNeverOpenedTwice()
    {
        var goal = Build();
        var window = Open(goal, Today)!;
        window.Miss(Utc.EndOfDay(Today).AddTicks(1));

        Assert.Null(Open(goal, Today));
        Assert.Single(goal.Instances);
    }

    [Fact]
    public void AWindowCloseWhenItsLastProofArrives()
    {
        var goal = Build(schedule: GoalSchedule.TimesPer(3, QuotaPeriod.Week));
        var window = Open(goal, Today, GoalSchedule.TimesPer(3, QuotaPeriod.Week))!;

        Assert.True(Deliver(goal, Created));
        Assert.Equal(GoalInstanceStatus.Open, window.Status);
        Assert.Equal(2, window.RemainingProofs);

        Assert.True(Deliver(goal, Created));
        Assert.True(Deliver(goal, Created));

        Assert.Equal(GoalInstanceStatus.Done, window.Status);
        Assert.Equal(0, window.RemainingProofs);

        // And nothing more goes into a window that has closed.
        Assert.False(Deliver(goal, Created));
    }

    [Fact]
    public void AOneOffIsFinishedWhenItsOnlyWindowIs()
    {
        var goal = Build(schedule: GoalSchedule.Once(), targetDate: Today);
        Open(goal, Today, GoalSchedule.Once());

        Assert.True(Deliver(goal, Created));
        Assert.Equal(GoalStatus.Completed, goal.Status);
    }

    [Fact]
    public void ARepeatingGoalStaysActiveWhenAWindowCloses()
    {
        var goal = Build();
        Open(goal, Today);

        Assert.True(Deliver(goal, Created));
        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    /// <summary>
    /// The chain, and what breaks it. This is the number the whole product
    /// hangs on, so it is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void TheStreakCountsBackAndStopsAtAMiss()
    {
        var goal = Build();

        Deliver(goal, Today.AddDays(-4));
        Deliver(goal, Today.AddDays(-3));
        Miss(goal, Today.AddDays(-2));
        Deliver(goal, Today.AddDays(-1));

        Assert.Equal(1, goal.Streak);

        Deliver(goal, Today);
        Assert.Equal(2, goal.Streak);
    }

    /// <summary>
    /// A window that is still running has not failed. Counting it would be a
    /// claim about the rest of the day.
    /// </summary>
    [Fact]
    public void AnOpenWindowNeitherAddsToTheStreakNorBreaksIt()
    {
        var goal = Build();
        Deliver(goal, Today.AddDays(-1));
        Open(goal, Today);

        Assert.Equal(1, goal.Streak);
    }

    [Fact]
    public void TheBalanceCountsBothOutcomes()
    {
        var goal = Build();
        Deliver(goal, Today.AddDays(-3));
        Deliver(goal, Today.AddDays(-2));
        Miss(goal, Today.AddDays(-1));

        Assert.Equal((2, 1), goal.Balance);
    }

    [Fact]
    public void AGoalIsOverdueOnceItsOpenWindowHasExpired()
    {
        var goal = Build();
        Open(goal, Today);

        Assert.False(goal.IsOverdueAt(Utc.EndOfDay(Today)));
        Assert.True(goal.IsOverdueAt(Utc.EndOfDay(Today).AddTicks(1)));
    }

    [Fact]
    public void ParticipantsAreAddedOnceAndNeverIncludeTheOwner()
    {
        var goal = Build();
        var friend = Guid.CreateVersion7();

        goal.AddParticipant(Guid.CreateVersion7(), friend);
        goal.AddParticipant(Guid.CreateVersion7(), friend);
        goal.AddParticipant(Guid.CreateVersion7(), Owner);

        Assert.Single(goal.Participants);
        Assert.True(goal.IsVisibleTo(friend));
        Assert.True(goal.IsVisibleTo(Owner));
        Assert.False(goal.IsVisibleTo(Guid.CreateVersion7()));
    }

    [Fact]
    public void AGoalCannotTakeMoreParticipantsThanItsLimit()
    {
        var goal = Build();

        for (var index = 0; index < Goal.MaxParticipants; index++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        Assert.Throws<DomainValidationException>(
            () => goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7()));
    }

    [Fact]
    public void AnArchivedGoalOpensNoFurtherWindows()
    {
        var goal = Build();
        goal.Close(completed: false, Created);

        Assert.Null(Open(goal, Today));
    }

    private static Goal Build(
        string title = "Walk 8.000 steps a day",
        string? icon = "target",
        GoalSchedule? schedule = null,
        DateOnly? targetDate = null) =>
        Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            title,
            null,
            icon,
            schedule ?? GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate,
            Created);

    private static GoalInstance? Open(Goal goal, DateOnly day, GoalSchedule? schedule = null)
    {
        var window = (schedule ?? GoalSchedule.EveryNDays(1)).FirstWindow(day, day);

        return goal.OpenWindow(
            Guid.CreateVersion7(),
            window,
            Utc.StartOfDay(window.Start),
            Utc.EndOfDay(window.End));
    }

    private static void Deliver(Goal goal, DateOnly day)
    {
        Open(goal, day);
        Deliver(goal, Utc.EndOfDay(day));
    }

    private static void Miss(Goal goal, DateOnly day) =>
        Open(goal, day)!.Miss(Utc.EndOfDay(day).AddTicks(1));
}
