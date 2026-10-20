using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// A spot check on a Don't (docs/adr/0038-spot-checks.md), on a fixed clock in
/// UTC: drawn inside the allowed hours and outside quiet hours, at most once a
/// day, open for three minutes, and decided — or voided — when they are up.
/// </summary>
public class SpotCheckTests
{
    private static readonly Guid Owner = new("33333333-3333-4333-8333-333333333333");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Monday = new(2026, 6, 1);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static readonly NotificationPreferences Quiet = NotificationPreferences.Default;

    private static IIdGenerator Ids { get; } = new SequentialIdGenerator();

    // -- drawing the moment -----------------------------------------------

    [Fact]
    public void AMomentIsAlwaysBetweenEightAndTenOutsideQuietHoursAndStillAhead()
    {
        var early = At(Monday, 0, 5);

        for (var i = 0; i < 300; i++)
        {
            var moment = SpotChecks.MomentFor(Monday, Utc, new TimeOnly(12, 0), new TimeOnly(14, 0), early, Guid.CreateVersion7())!.Value;
            var time = TimeOnly.FromDateTime(moment.UtcDateTime);

            Assert.InRange(time, new TimeOnly(8, 0), new TimeOnly(21, 57));
            Assert.False(time >= new TimeOnly(12, 0) && time < new TimeOnly(14, 0), $"{time} is in the quiet hours");
            Assert.True(moment > early);
        }
    }

    [Fact]
    public void TheSameSeedDrawsTheSameMoment()
    {
        var seed = Guid.CreateVersion7();

        Assert.Equal(
            SpotChecks.MomentFor(Monday, Utc, null, null, At(Monday, 0, 5), seed),
            SpotChecks.MomentFor(Monday, Utc, null, null, At(Monday, 0, 5), seed));
    }

    [Fact]
    public void ADayWithNoAllowedMinuteLeftDrawsNothing()
    {
        Assert.Null(SpotChecks.MomentFor(Monday, Utc, null, null, At(Monday, 21, 58), Guid.CreateVersion7()));

        // Quiet all day long.
        Assert.Null(SpotChecks.MomentFor(Monday, Utc, new TimeOnly(0, 0), new TimeOnly(23, 59), At(Monday, 0, 5), Guid.CreateVersion7()));
    }

    // -- at most one a day ------------------------------------------------

    [Fact]
    public void ADayGetsOneCheckAndOnlyOne()
    {
        var goal = SpotCheckDont();
        GoalMaintenance.Advance(goal, Utc, At(Monday, 0, 5), Ids);

        Assert.True(SpotChecks.Draw(goal, Utc, Quiet, At(Monday, 0, 5), Ids));
        Assert.False(SpotChecks.Draw(goal, Utc, Quiet, At(Monday, 0, 15), Ids));

        Assert.Single(goal.CurrentInstance!.SpotChecks);
    }

    [Fact]
    public void AWeeklyWindowGetsOneCheckEachDay()
    {
        var goal = SpotCheckDont(QuotaPeriod.Week);

        for (var day = 0; day < 3; day++)
        {
            GoalMaintenance.Advance(goal, Utc, At(Monday.AddDays(day), 0, 5), Ids);
            Assert.True(SpotChecks.Draw(goal, Utc, Quiet, At(Monday.AddDays(day), 0, 5), Ids));
        }

        Assert.Equal(3, goal.CurrentInstance!.SpotChecks.Select(check => check.Day).Distinct().Count());
    }

    [Theory]
    [InlineData(DontProofMode.HonestOnly)]
    [InlineData(DontProofMode.ProofAtEnd)]
    public void OnlyASpotCheckDontIsChecked(DontProofMode mode)
    {
        var goal = SpotCheckDont(mode: mode);
        GoalMaintenance.Advance(goal, Utc, At(Monday, 0, 5), Ids);

        Assert.False(SpotChecks.Draw(goal, Utc, Quiet, At(Monday, 0, 5), Ids));
    }

    [Fact]
    public void APausedDontDrawsNoCheck()
    {
        var goal = SpotCheckDont();
        GoalMaintenance.Advance(goal, Utc, At(Monday, 0, 5), Ids);
        goal.RequestPause(Ids.NewId(), "Krank im Bett", 2, Monday, Utc.EndOfDay, At(Monday, 0, 6));

        Assert.False(SpotChecks.Draw(goal, Utc, Quiet, At(Monday, 0, 7), Ids));
    }

    [Fact]
    public void ARelapsedPeriodDrawsNoMoreChecks()
    {
        var goal = SpotCheckDont(QuotaPeriod.Week);
        GoalMaintenance.Advance(goal, Utc, At(Monday, 0, 5), Ids);
        goal.LogUse(Ids.NewId(), null, At(Monday, 0, 6));

        Assert.False(SpotChecks.Draw(goal, Utc, Quiet, At(Monday.AddDays(1), 0, 5), Ids));
    }

    // -- asked, answered, missed ------------------------------------------

    [Fact]
    public void ACheckIsAskedAtItsMomentAndOpenForThreeMinutes()
    {
        var (goal, check) = Drawn();

        Assert.Empty(goal.IssueDueSpotChecks(check.ScheduledAt.AddSeconds(-1)));

        var issued = Assert.Single(goal.IssueDueSpotChecks(check.ScheduledAt.AddSeconds(20)));
        Assert.Equal(SpotCheckStatus.Open, issued.Status);
        Assert.Equal(check.ScheduledAt.AddSeconds(20) + TimeSpan.FromMinutes(3), issued.ExpiresAt);
        Assert.Same(issued, goal.OpenSpotCheckAt(check.ScheduledAt.AddMinutes(3)));

        // Asked once: the next pass does not ask again.
        Assert.Empty(goal.IssueDueSpotChecks(check.ScheduledAt.AddSeconds(50)));
    }

    [Fact]
    public void ACheckNotAnsweredInThreeMinutesMissesTheWindowAndIsNoRelapse()
    {
        var (goal, check) = Drawn();
        goal.IssueDueSpotChecks(check.ScheduledAt);

        Assert.True(GoalMaintenance.Advance(goal, Utc, check.ScheduledAt.AddMinutes(4), Ids));

        Assert.Equal(SpotCheckStatus.Missed, check.Status);
        var window = goal.Instances.Single(instance => instance.DueOn == Monday);
        Assert.Equal(GoalInstanceStatus.Missed, window.Status);
        Assert.Equal((0, 1, 0), goal.Balance);
    }

    [Fact]
    public void ACheckWhoseMinutesPassedWhileNothingRanIsVoidedNotAskedLate()
    {
        var (goal, check) = Drawn();

        // The server comes back an hour after the moment.
        var later = check.ScheduledAt.AddHours(1);
        Assert.Empty(goal.IssueDueSpotChecks(later));
        GoalMaintenance.Advance(goal, Utc, later, Ids);

        Assert.Equal(SpotCheckStatus.Voided, check.Status);
        Assert.Equal(GoalInstanceStatus.Open, goal.CurrentInstance!.Status);

        // And the day still ends kept: the owner was never asked.
        GoalMaintenance.Advance(goal, Utc, At(Monday.AddDays(1), 0, 5), Ids);
        Assert.Equal(GoalInstanceStatus.Done, goal.Instances.Single(instance => instance.DueOn == Monday).Status);
    }

    [Fact]
    public void ABelievedCheckLeavesTheWindowToItsLimit()
    {
        var (goal, check) = Drawn(voters: 1);
        goal.IssueDueSpotChecks(check.ScheduledAt);

        var proof = goal.AnswerSpotCheck(check.Id, Ids.NewId(), Ids.NewId(), check.ScheduledAt.AddMinutes(1))!;
        Assert.Equal(ProofSource.Live, proof.Source);
        Assert.Equal(SpotCheckStatus.Answered, check.Status);

        Assert.True(goal.ApplyProofOutcome(proof, VotingResult.Confirmed, check.ScheduledAt.AddHours(1)));
        Assert.Equal(GoalInstanceStatus.Open, goal.CurrentInstance!.Status);

        // Still a relapse when the limit breaks afterwards.
        goal.LogUse(Ids.NewId(), null, check.ScheduledAt.AddHours(2));
        Assert.Equal(GoalInstanceStatus.Relapse, goal.Instances.Single(instance => instance.DueOn == Monday).Status);
    }

    [Fact]
    public void ARefusedCheckMissesTheWindowWithoutASecondAttempt()
    {
        var (goal, check) = Drawn(voters: 2);
        goal.IssueDueSpotChecks(check.ScheduledAt);
        var proof = goal.AnswerSpotCheck(check.Id, Ids.NewId(), Ids.NewId(), check.ScheduledAt.AddMinutes(1))!;

        // Doubted by both: on any other proof a retry, on a check the end of it.
        var outcome = ProofVoting.Evaluate([VoteValue.Doubt, VoteValue.Doubt], expectedVoterCount: 2, proof.Attempt);
        Assert.Equal(VotingResult.Rejected, outcome.Result);

        goal.ApplyProofOutcome(proof, outcome.Result, check.ScheduledAt.AddHours(1));
        Assert.Equal(GoalInstanceStatus.Missed, goal.Instances.Single(instance => instance.DueOn == Monday).Status);
    }

    [Fact]
    public void ACheckCannotBeAnsweredOnceItsMinutesAreUp()
    {
        var (goal, check) = Drawn();
        goal.IssueDueSpotChecks(check.ScheduledAt);

        Assert.Null(goal.AnswerSpotCheck(check.Id, Ids.NewId(), Ids.NewId(), check.ScheduledAt.AddMinutes(3).AddSeconds(1)));
    }

    [Fact]
    public void APauseIsNoWayOutOfAnOpenCheck()
    {
        var (goal, check) = Drawn();
        goal.IssueDueSpotChecks(check.ScheduledAt);

        Assert.Throws<DomainValidationException>(() =>
            goal.RequestPause(Ids.NewId(), "Krank im Bett", 2, Monday, Utc.EndOfDay, check.ScheduledAt.AddMinutes(1)));
    }

    private static (Goal Goal, SpotCheck Check) Drawn(int voters = 0)
    {
        var goal = SpotCheckDont(voters: voters);
        GoalMaintenance.Advance(goal, Utc, At(Monday, 0, 5), Ids);
        Assert.True(SpotChecks.Draw(goal, Utc, Quiet, At(Monday, 0, 5), Ids));
        return (goal, goal.CurrentInstance!.SpotChecks.Single());
    }

    private static Goal SpotCheckDont(QuotaPeriod period = QuotaPeriod.Day, DontProofMode mode = DontProofMode.SpotCheck, int voters = 0)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Nicht rauchen",
            null,
            GoalCategory.Other,
            GoalSchedule.Per(period),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            GoalKind.Dont,
            0,
            mode);

        for (var voter = 0; voter < voters; voter++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        return goal;
    }

    private static DateTimeOffset At(DateOnly day, int hour, int minute) =>
        new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero);
}
