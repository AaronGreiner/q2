using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// A Don't: a limit per period, uses its owner logs, and an honest relapse
/// (docs/adr/0037-donts.md).
/// </summary>
/// <remarks>
/// Everything runs on a fixed clock in UTC, so "Monday" is a whole day without
/// a daylight-saving edge in it. The cases are the ones #62 names: a limit
/// kept, a relapse that holds the streak, the third relapse that ends it, a
/// photograph at the end that was refused, and catching up over periods that
/// passed while nobody looked.
/// </remarks>
public class DontTests
{
    private static readonly Guid Owner = new("22222222-2222-4222-8222-222222222222");

    /// <summary>A Monday.</summary>
    private static readonly DateOnly Monday = new(2026, 6, 1);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static IIdGenerator Ids { get; } = new SequentialIdGenerator();

    [Fact]
    public void APeriodThatEndsWithinItsLimitIsKept()
    {
        var goal = Dont(limit: 1, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        Assert.NotNull(goal.LogUse(Ids.NewId(), null, Noon(Monday)));
        Assert.Equal(GoalInstanceStatus.Open, goal.CurrentInstance!.Status);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);

        var monday = goal.Instances.Single(instance => instance.DueOn == Monday);
        Assert.Equal(GoalInstanceStatus.Done, monday.Status);
        Assert.Equal(1, monday.Uses);
        Assert.Equal(1, goal.Streak);
        Assert.Equal((1, 0, 0), goal.Balance);
    }

    [Fact]
    public void TheUseOverTheLimitIsARelapseAtOnceAndHoldsTheStreak()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);
        Assert.Equal(1, goal.Streak);

        // Tuesday: one is already over "never".
        goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(1)));

        var tuesday = goal.Instances.Single(instance => instance.DueOn == Monday.AddDays(1));
        Assert.Equal(GoalInstanceStatus.Relapse, tuesday.Status);
        Assert.Equal(Noon(Monday.AddDays(1)), tuesday.ResolvedAt);

        // Held: neither grown nor ended, and not a miss in the balance.
        Assert.Equal(1, goal.Streak);
        Assert.Equal((1, 0, 1), goal.Balance);

        // Still the window of today, though decided.
        Assert.Null(goal.CurrentInstance);
        Assert.Same(tuesday, goal.RelapsedInstanceAt(Noon(Monday.AddDays(1))));

        // A second use is still taken and changes nothing.
        Assert.NotNull(goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(1)).AddHours(2)));
        Assert.Equal(2, tuesday.Uses);
        Assert.Equal(GoalInstanceStatus.Relapse, tuesday.Status);

        // Wednesday kept: the streak goes on from where it was held.
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(3)), Ids);
        Assert.Equal(2, goal.Streak);
    }

    [Fact]
    public void AWeeklyLimitIsCountedOverTheWholeWeek()
    {
        var goal = Dont(limit: 3, QuotaPeriod.Week, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        var week = goal.CurrentInstance!;
        Assert.Equal(Monday, week.StartsOn);
        Assert.Equal(Monday.AddDays(6), week.DueOn);
        Assert.Equal(0, week.RequiredProofs);

        for (var day = 0; day < 3; day++)
        {
            goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(day)));
        }

        Assert.Equal(GoalInstanceStatus.Open, week.Status);

        goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(4)));
        Assert.Equal(GoalInstanceStatus.Relapse, week.Status);
    }

    [Fact]
    public void TheThirdRelapseInARowEndsTheStreakLikeAMiss()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);

        // Two kept days.
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);
        Assert.Equal(2, goal.Streak);

        // Two relapses in a row hold it.
        for (var day = 2; day < 4; day++)
        {
            goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(day)));
            GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(day + 1)), Ids);
        }

        Assert.Equal(2, goal.Streak);

        // The third does not.
        goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(4)));
        Assert.Equal(0, goal.Streak);
        Assert.Equal((2, 0, 3), goal.Balance);
    }

    [Fact]
    public void AKeptPeriodBetweenRelapsesStartsTheCountOfRelapsesAgain()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        // Relapse, relapse, kept, relapse: never three in a row.
        foreach (var (day, relapse) in new[] { (0, true), (1, true), (2, false), (3, true) })
        {
            GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(day)), Ids);

            if (relapse)
            {
                goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(day)));
            }
        }

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(4)), Ids);

        Assert.Equal(1, goal.Streak);
    }

    [Fact]
    public void AProofAtTheEndThatIsRefusedMissesTheWindow()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.ProofAtEnd, voters: 1);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        var window = goal.CurrentInstance!;
        Assert.Equal(1, window.RequiredProofs);

        var proof = goal.SubmitProof(Ids.NewId(), Ids.NewId(), ProofSource.Screenshot, Noon(Monday))!;
        Assert.True(goal.ApplyProofOutcome(proof, VotingResult.Rejected, Noon(Monday).AddHours(1)));

        Assert.Equal(GoalInstanceStatus.Missed, window.Status);
        Assert.Equal((0, 1, 0), goal.Balance);
    }

    [Fact]
    public void ABelievedProofAtTheEndKeepsTheWindowOnlyWhenThePeriodIsOver()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.ProofAtEnd, voters: 1);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        var window = goal.CurrentInstance!;
        var proof = goal.SubmitProof(Ids.NewId(), Ids.NewId(), ProofSource.Screenshot, Noon(Monday))!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Noon(Monday).AddHours(1));

        // Believed, but the day is not over: a use tonight would still be a
        // relapse.
        Assert.Equal(GoalInstanceStatus.Open, window.Status);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);
        Assert.Equal(GoalInstanceStatus.Done, window.Status);
    }

    [Fact]
    public void AUseAfterABelievedProofIsStillARelapse()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.ProofAtEnd, voters: 1);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        var window = goal.CurrentInstance!;
        var proof = goal.SubmitProof(Ids.NewId(), Ids.NewId(), ProofSource.Live, Noon(Monday))!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Noon(Monday).AddHours(1));

        goal.LogUse(Ids.NewId(), null, Noon(Monday).AddHours(8));

        Assert.Equal(GoalInstanceStatus.Relapse, window.Status);
    }

    [Fact]
    public void CatchingUpKeepsEveryHonestPeriodThatPassedAndMissesEveryUnprovenOne()
    {
        var honest = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);
        var proven = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.ProofAtEnd);

        foreach (var goal in new[] { honest, proven })
        {
            GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

            // Three days down, one pass.
            Assert.True(GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(3)), Ids));
            Assert.Equal(4, goal.Instances.Count);
            Assert.Equal(Monday.AddDays(3), goal.CurrentInstance!.DueOn);

            // Idempotent: the second run changes nothing.
            Assert.False(GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(3)), Ids));
        }

        Assert.Equal((3, 0, 0), honest.Balance);
        Assert.Equal(3, honest.Streak);

        Assert.Equal((0, 3, 0), proven.Balance);
        Assert.Equal(0, proven.Streak);
    }

    [Fact]
    public void ARelapsedWeekOpensNoNewWindowUntilTheWeekIsOver()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Week, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        goal.LogUse(Ids.NewId(), null, Noon(Monday));

        Assert.False(GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(3)), Ids));
        Assert.Single(goal.Instances);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(7)), Ids);
        Assert.Equal(Monday.AddDays(7), goal.CurrentInstance!.StartsOn);
    }

    [Fact]
    public void ARunStartsTheDayAfterTheLastRelapse()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        Assert.Equal(Monday, goal.RunStartedOn(Utc.DayOf));

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);
        goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(2)));

        Assert.Equal(Monday.AddDays(3), goal.RunStartedOn(Utc.DayOf));
    }

    [Fact]
    public void ADoHasNoUsesToLog()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(), Owner, "Laufen", null, GoalCategory.Other, GoalSchedule.EveryNDays(1), false, null, null, Start);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);

        Assert.Null(goal.LogUse(Ids.NewId(), null, Noon(Monday)));
        Assert.Null(goal.RunStartedOn(Utc.DayOf));
    }

    [Fact]
    public void APausedDontTakesNoUse()
    {
        var goal = Dont(limit: 0, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        goal.RequestPause(Ids.NewId(), "Krank im Bett", 2, Monday, Utc.EndOfDay, Noon(Monday));

        Assert.Null(goal.LogUse(Ids.NewId(), null, Noon(Monday)));
    }

    [Theory]
    [InlineData(null, DontProofMode.HonestOnly)]
    [InlineData(-1, DontProofMode.HonestOnly)]
    [InlineData(Goal.MaxLimit + 1, DontProofMode.HonestOnly)]
    [InlineData(0, null)]
    public void ADontNeedsALimitAndAProofMode(int? limit, DontProofMode? mode)
    {
        Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), Owner, "Kein Alkohol", null, GoalCategory.Other, GoalSchedule.Per(QuotaPeriod.Day), false, null, null, Start,
            GoalKind.Dont, limit, mode));
    }

    [Fact]
    public void OnlyADontIsCountedPerPeriodAndOnlyADontIsCountedPerDay()
    {
        Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), Owner, "Laufen", null, GoalCategory.Other, GoalSchedule.Per(QuotaPeriod.Week), false, null, null, Start));

        Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), Owner, "Kein Alkohol", null, GoalCategory.Other, GoalSchedule.EveryNDays(1), false, null, null, Start,
            GoalKind.Dont, 0, DontProofMode.HonestOnly));

        Assert.Throws<DomainValidationException>(() => GoalSchedule.TimesPer(3, QuotaPeriod.Day));
    }

    [Fact]
    public void TheConversationSaysEveryUseAndTheRelapseWithTheStreakItHeld()
    {
        var goal = Dont(limit: 1, QuotaPeriod.Day, DontProofMode.HonestOnly);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);

        var image = Ids.NewId();
        goal.LogUse(Ids.NewId(), image, Noon(Monday.AddDays(1)));
        goal.LogUse(Ids.NewId(), null, Noon(Monday.AddDays(1)).AddHours(1));

        var events = GoalTimeline.For(goal, Noon(Monday.AddDays(1)).AddHours(2));

        var uses = events.Where(entry => entry.Kind == GoalEventKind.UseLogged).ToList();
        Assert.Equal([1, 2], uses.Select(entry => entry.Uses!.Value));
        Assert.All(uses, entry => Assert.Equal(1, entry.Limit));
        Assert.Equal(image, uses[0].ImageId);
        Assert.Equal(Owner, uses[0].ActorPersonId);

        var kept = Assert.Single(events, entry => entry.Kind == GoalEventKind.WindowDone);
        Assert.Equal(1, kept.Streak);
        Assert.Equal(1, kept.Limit);

        var relapse = Assert.Single(events, entry => entry.Kind == GoalEventKind.WindowRelapse);
        Assert.Equal(1, relapse.Streak);
        Assert.Equal(2, relapse.Uses);
        Assert.Null(relapse.ActorPersonId);
    }

    /// <summary>
    /// "Höchstens 3× pro Woche" made on a Sunday holds its share of the limit
    /// for the one day left, not the week's three (#96).
    /// </summary>
    [Fact]
    public void AFirstPeriodStartedLateHoldsItsShareOfTheLimit()
    {
        var sunday = Monday.AddDays(6);
        var goal = Goal.Create(
            Guid.CreateVersion7(), Owner, "Bier", null, GoalCategory.Other, GoalSchedule.Per(QuotaPeriod.Week), false, null, null,
            new DateTimeOffset(sunday.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            GoalKind.Dont, 3, DontProofMode.HonestOnly);

        GoalMaintenance.Advance(goal, Utc, Noon(sunday), Ids);

        Assert.Equal(sunday, goal.CurrentInstance!.StartsOn);
        Assert.Equal(1, goal.CurrentInstance.Limit);
    }

    private static readonly DateTimeOffset Start = new(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static Goal Dont(int limit, QuotaPeriod period, DontProofMode mode, int voters = 0)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Kein Alkohol",
            null,
            GoalCategory.Other,
            GoalSchedule.Per(period),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            Start,
            GoalKind.Dont,
            limit,
            mode);

        for (var voter = 0; voter < voters; voter++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        return goal;
    }

    private static DateTimeOffset Noon(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
