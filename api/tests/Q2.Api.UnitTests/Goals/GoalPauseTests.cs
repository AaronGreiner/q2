using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// The way out, and what it costs.
/// </summary>
/// <remarks>
/// Pure unit tests over <see cref="Goal"/> and <see cref="PauseRules"/>: every
/// instant, every day and every id is passed in, so nothing here depends on
/// when it runs.
/// </remarks>
public class GoalPauseTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static readonly DateTimeOffset Noon = Utc.StartOfDay(Today).AddHours(12);

    private const string Reason = "Grippe, seit Freitag im Bett.";

    [Fact]
    public void APausedGoalHasNoOpenWindow()
    {
        var goal = Build();
        Open(goal, Today);

        goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);

        Assert.Null(goal.CurrentInstance);
        Assert.Equal(GoalInstanceStatus.Paused, Assert.Single(goal.Instances).Status);
    }

    /// <summary>
    /// The point of the whole feature: the window that was set aside counts in
    /// neither half of the balance and does not break the chain.
    /// </summary>
    [Fact]
    public void ASuspendedWindowIsNeitherDoneNorMissed()
    {
        var goal = Build();
        Deliver(goal, Today.AddDays(-2));
        Deliver(goal, Today.AddDays(-1));
        Open(goal, Today);

        goal.RequestPause(Guid.CreateVersion7(), Reason, 2, Today, Utc.EndOfDay, Noon);

        Assert.Equal((2, 0), goal.Balance);
        Assert.Equal(2, goal.Streak);
    }

    [Fact]
    public void APauseCoversWholeLocalDaysFromToday()
    {
        var goal = Build();

        var pause = goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);

        Assert.Equal(Today, pause.StartsOn);
        Assert.Equal(Today.AddDays(2), pause.EndsOn);
        Assert.Equal(3, pause.Days);
        Assert.Equal(Utc.EndOfDay(Today.AddDays(2)), pause.EndsAt);
        Assert.True(pause.IsActiveAt(Noon));
        Assert.False(pause.IsActiveAt(Utc.StartOfDay(Today.AddDays(3))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void APauseIsBetweenOneAndSevenDays(int days)
    {
        var goal = Build();

        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), Reason, days, Today, Utc.EndOfDay, Noon));
    }

    [Fact]
    public void APauseNeedsMoreThanAShrug()
    {
        var goal = Build();

        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), "krank", 2, Today, Utc.EndOfDay, Noon));
    }

    [Fact]
    public void OnlyOnePauseRunsAtATime()
    {
        var goal = Build();
        goal.RequestPause(Guid.CreateVersion7(), Reason, 2, Today, Utc.EndOfDay, Noon);

        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), Reason, 2, Today, Utc.EndOfDay, Noon));
    }

    /// <summary>
    /// Scarcity is the brake that works without anybody accusing anybody, so
    /// this is the test that matters most in the file.
    /// </summary>
    [Fact]
    public void TwoPausesAMonthAndNoMore()
    {
        var goal = Build();

        goal.RequestPause(Guid.CreateVersion7(), Reason, 1, Today, Utc.EndOfDay, Noon);
        Assert.Equal(1, goal.RemainingPauses(Today));

        var second = Today.AddDays(3);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 1, second, Utc.EndOfDay, Utc.StartOfDay(second));
        Assert.Equal(0, goal.RemainingPauses(second));

        var third = Today.AddDays(6);
        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), Reason, 1, third, Utc.EndOfDay, Utc.StartOfDay(third)));

        // The next month starts over. The allowance is a monthly one, not a
        // lifetime one.
        var nextMonth = new DateOnly(2026, 7, 1);
        Assert.Equal(PauseRules.MaxPerMonth, goal.RemainingPauses(nextMonth));
    }

    [Fact]
    public void APauseCannotInterruptARunningVote()
    {
        var goal = Build();
        Open(goal, Today);
        goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), capturedInApp: true, Noon);

        // A goal with participants keeps the vote open, which is the case this
        // rule is about.
        Assert.NotNull(goal.CurrentInstance!.PendingProof);
        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), Reason, 2, Today, Utc.EndOfDay, Noon));
    }

    [Fact]
    public void AStoppedGoalCannotBePaused()
    {
        var goal = Build();
        goal.Close(completed: true, Noon);

        Assert.Throws<DomainValidationException>(
            () => goal.RequestPause(Guid.CreateVersion7(), Reason, 2, Today, Utc.EndOfDay, Noon));
    }

    [Fact]
    public void OneObjectionNeverLiftsAPause()
    {
        var goal = Build(participants: 3);
        Open(goal, Today);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);

        var pause = goal.VetoPause(Guid.CreateVersion7(), goal.Participants[0].PersonId, Noon);

        Assert.NotNull(pause);
        Assert.Equal(PauseStatus.Active, pause.Status);
        Assert.Equal(GoalInstanceStatus.Paused, Assert.Single(goal.Instances).Status);
    }

    /// <summary>
    /// Two objections carry, and the window comes back exactly as it was — with
    /// the deadline it always had.
    /// </summary>
    [Fact]
    public void TwoObjectionsPutTheWindowBack()
    {
        var goal = Build(participants: 3);
        Open(goal, Today);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);

        goal.VetoPause(Guid.CreateVersion7(), goal.Participants[0].PersonId, Noon);
        var pause = goal.VetoPause(Guid.CreateVersion7(), goal.Participants[1].PersonId, Noon);

        Assert.Equal(PauseStatus.Overturned, pause!.Status);
        Assert.NotNull(goal.CurrentInstance);
        Assert.Equal(Today, goal.CurrentInstance!.DueOn);
        Assert.Null(goal.ActivePauseAt(Noon));
    }

    [Fact]
    public void AnObjectionCanBeTakenBack()
    {
        var goal = Build(participants: 3);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);
        var voter = goal.Participants[0].PersonId;

        goal.VetoPause(Guid.CreateVersion7(), voter, Noon);
        var pause = goal.VetoPause(Guid.CreateVersion7(), voter, Noon);

        Assert.Equal(0, pause!.VetoCount);
        Assert.Equal(PauseStatus.Active, pause.Status);
    }

    /// <summary>
    /// An overturned pause still costs one of the two. Otherwise the allowance
    /// could be worked around by simply asking again.
    /// </summary>
    [Fact]
    public void AnOverturnedPauseStillCountsAgainstTheAllowance()
    {
        var goal = Build(participants: 3);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 3, Today, Utc.EndOfDay, Noon);

        goal.VetoPause(Guid.CreateVersion7(), goal.Participants[0].PersonId, Noon);
        goal.VetoPause(Guid.CreateVersion7(), goal.Participants[1].PersonId, Noon);

        Assert.Equal(1, goal.RemainingPauses(Today));
    }

    [Fact]
    public void EndingAPauseEarlyDoesNotHandBackTheDeadline()
    {
        var goal = Build();
        Open(goal, Today);
        goal.RequestPause(Guid.CreateVersion7(), Reason, 5, Today, Utc.EndOfDay, Noon);

        Assert.True(goal.EndPause(Today.AddDays(1), Utc.StartOfDay(Today.AddDays(1))));

        Assert.Null(goal.ActivePauseAt(Utc.StartOfDay(Today.AddDays(1))));
        Assert.Equal(GoalInstanceStatus.Paused, Assert.Single(goal.Instances).Status);
    }

    [Fact]
    public void AnExpiredPauseClosesItselfOnce()
    {
        var goal = Build();
        goal.RequestPause(Guid.CreateVersion7(), Reason, 1, Today, Utc.EndOfDay, Noon);

        var after = Utc.StartOfDay(Today.AddDays(1));

        Assert.True(goal.ExpirePauses(after));
        Assert.False(goal.ExpirePauses(after));
    }

    /// <summary>
    /// Two, and more than a third of the people invited — the same construction
    /// as doubting a photograph, so one person can never carry it alone.
    /// </summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(6, 3)]
    [InlineData(9, 4)]
    public void ObjectionsRequiredIsTwoOrMoreThanAThird(int participants, int expected) =>
        Assert.Equal(expected, PauseRules.VetoesRequired(participants));

    private static Goal Build(int participants = 0)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Kalt duschen",
            null,
            "droplet",
            GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            Created);

        for (var index = 0; index < participants; index++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        return goal;
    }

    private static GoalInstance? Open(Goal goal, DateOnly day)
    {
        var window = GoalSchedule.EveryNDays(1).FirstWindow(day, day);

        return goal.OpenWindow(
            Guid.CreateVersion7(),
            window,
            Utc.StartOfDay(window.Start),
            Utc.EndOfDay(window.End));
    }

    private static void Deliver(Goal goal, DateOnly day)
    {
        var instance = Open(goal, day)!;
        var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), true, Utc.EndOfDay(day))!;

        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Utc.EndOfDay(day));
        Assert.Equal(GoalInstanceStatus.Done, instance.Status);
    }
}
