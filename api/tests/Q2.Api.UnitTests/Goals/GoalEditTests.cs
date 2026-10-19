using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// Editing a running goal: what applies at once, what waits for the next
/// window, and what the people who check it can do about an easier one.
/// </summary>
/// <remarks>
/// Driven through <see cref="GoalMaintenance.Advance"/>, because that is where
/// a waiting change takes effect — as the window it applies with opens
/// (docs/adr/0044-editing-a-running-goal.md).
/// </remarks>
public class GoalEditTests
{
    private static readonly Guid Owner = new("11111111-1111-4111-8111-111111111111");

    /// <summary>A Monday. UTC, so a local day starts at midnight on the clock in these tests.</summary>
    private static readonly DateOnly Monday = new(2026, 6, 1);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    private static IIdGenerator Ids { get; } = new SequentialIdGenerator();

    [Fact]
    public void TheTitleAndIconApplyAtOnceAndAreRecorded()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));

        var change = Edit(goal, new GoalEdit(Title: "Joggen", Icon: "flame"), Noon(Monday));

        Assert.Equal("Joggen", goal.Title);
        Assert.Equal("flame", goal.Icon);
        Assert.NotNull(change);
        Assert.Equal("Anything", change.TitleFrom);
        Assert.Equal("Joggen", change.TitleTo);
        Assert.Equal(GoalChangeStatus.None, change.Status);
        Assert.Null(goal.PendingChange);
    }

    [Fact]
    public void AnEditThatChangesNothingRecordsNothing()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));

        Assert.Null(Edit(goal, new GoalEdit(Title: "Anything", Schedule: GoalSchedule.EveryNDays(1)), Noon(Monday)));
        Assert.Empty(goal.Changes);
    }

    /// <summary>
    /// The open window keeps the terms it opened with: its deadline and what it
    /// takes. Only the next one is cut from the new schedule.
    /// </summary>
    [Fact]
    public void TheOpenWindowKeepsItsTermsAndTheNextFollowsTheNewSchedule()
    {
        var goal = Running(GoalSchedule.TimesPer(3, QuotaPeriod.Week));
        var open = goal.CurrentInstance!;

        var change = Edit(goal, new GoalEdit(Schedule: GoalSchedule.TimesPer(2, QuotaPeriod.Week)), Noon(Monday));

        Assert.NotNull(change);
        Assert.True(change.Objectable);
        Assert.Equal(GoalChangeStatus.Pending, change.Status);
        Assert.Equal(3, goal.Schedule.Times);
        Assert.Equal(3, open.RequiredProofs);
        Assert.Equal(Monday.AddDays(6), open.DueOn);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(7)), Ids);

        Assert.Equal(GoalChangeStatus.Applied, change.Status);
        Assert.Equal(Monday.AddDays(7), change.AppliesOn);
        Assert.Equal(2, goal.Schedule.Times);
        Assert.Equal(3, open.RequiredProofs);
        Assert.Equal(2, goal.CurrentInstance!.RequiredProofs);
        Assert.Equal(Monday.AddDays(7), goal.CurrentInstance.StartsOn);
    }

    /// <summary>
    /// A change does not reset or rewrite the streak: the windows behind it are
    /// what they were.
    /// </summary>
    [Fact]
    public void AScheduleChangeLeavesTheStreakAlone()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), ProofSource.Live, Noon(Monday))!;
        goal.ApplyProofOutcome(proof, VotingResult.Confirmed, Noon(Monday));
        Assert.Equal(1, goal.Streak);

        Edit(goal, new GoalEdit(Schedule: GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday])), Noon(Monday));

        Assert.Equal(1, goal.Streak);
    }

    [Fact]
    public void AnEasierChangeWaitsForAWindowThatOpensADayAfterTheEdit()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));

        // Late in the evening, so tomorrow's window opens before a day is up.
        var change = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(2)), Monday.ToDateTime(new TimeOnly(23, 55)).ToUtc());

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);

        Assert.Equal(GoalChangeStatus.Pending, change!.Status);
        Assert.Equal(1, goal.Schedule.EveryDays);
        Assert.Equal(Monday.AddDays(1), goal.CurrentInstance!.DueOn);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);

        Assert.Equal(GoalChangeStatus.Applied, change.Status);
        Assert.Equal(Monday.AddDays(2), change.AppliesOn);
        Assert.Equal(2, goal.Schedule.EveryDays);
    }

    [Fact]
    public void AStricterChangeAppliesWithTheVeryNextWindow()
    {
        var goal = Running(GoalSchedule.EveryNDays(2));

        var change = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(1)), Monday.ToDateTime(new TimeOnly(23, 55)).ToUtc());

        Assert.False(change!.Objectable);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(1)), Ids);

        Assert.Equal(GoalChangeStatus.Applied, change.Status);
        Assert.Equal(Monday.AddDays(1), change.AppliesOn);
        Assert.Equal(Monday.AddDays(1), goal.CurrentInstance!.DueOn);
    }

    /// <summary>
    /// After a change the new terms count from the day it applied, never from
    /// days before it — an easier change made at night does not open a window
    /// for the day it had to wait through.
    /// </summary>
    [Fact]
    public void TheNewTermsDoNotReachBackBeforeTheDayTheChangeApplied()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        Edit(goal, new GoalEdit(Schedule: GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday])), Noon(Monday));

        // Tuesday is still the old schedule's; the change counts from Wednesday.
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);

        Assert.Equal(
            [Monday, Monday.AddDays(1), Monday.AddDays(2)],
            goal.Instances.Select(instance => instance.DueOn).Order());
        Assert.Equal(Monday.AddDays(2), goal.Changes.Single().AppliesOn);
    }

    /// <summary>
    /// The period changed under a running goal: the first window of the new
    /// one starts the day after the old window, and asks for its share.
    /// </summary>
    [Fact]
    public void AChangedPeriodCutsTheFirstWindowAndScalesItsQuota()
    {
        var goal = Running(GoalSchedule.TimesPer(3, QuotaPeriod.Week));

        Edit(goal, new GoalEdit(Schedule: GoalSchedule.TimesPer(10, QuotaPeriod.Month)), Noon(Monday));
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(7)), Ids);

        var cut = goal.CurrentInstance!;
        Assert.Equal(new DateOnly(2026, 6, 8), cut.StartsOn);
        Assert.Equal(new DateOnly(2026, 6, 30), cut.DueOn);

        // Ten in thirty days, for twenty-three of them: 7.67, rounded up.
        Assert.Equal(8, cut.RequiredProofs);
    }

    [Fact]
    public void ADontWindowCutShortHoldsItsShareOfTheLimit()
    {
        var goal = RunningDont(limit: 2, QuotaPeriod.Week, DontProofMode.HonestOnly);

        Edit(goal, new GoalEdit(Schedule: GoalSchedule.Per(QuotaPeriod.Month), Limit: 10), Noon(Monday));
        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(7)), Ids);

        var cut = goal.CurrentInstance!;
        Assert.Equal(new DateOnly(2026, 6, 8), cut.StartsOn);
        Assert.Equal(8, cut.Limit);
        Assert.Equal(10, goal.Limit);
    }

    [Fact]
    public void EnoughObjectionsLiftAnEasierChange()
    {
        var friends = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var goal = Running(GoalSchedule.EveryNDays(1), friends);

        var change = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(3)), Noon(Monday))!;

        goal.VetoChange(Guid.CreateVersion7(), friends[0], Noon(Monday));
        Assert.Equal(GoalChangeStatus.Pending, change.Status);

        goal.VetoChange(Guid.CreateVersion7(), friends[1], Noon(Monday));
        Assert.Equal(GoalChangeStatus.Overturned, change.Status);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(3)), Ids);

        Assert.Equal(1, goal.Schedule.EveryDays);
        Assert.Equal(Monday.AddDays(3), goal.CurrentInstance!.DueOn);
    }

    [Fact]
    public void AnObjectionCanBeTakenBack()
    {
        var friends = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        var goal = Running(GoalSchedule.EveryNDays(1), friends);
        var change = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(3)), Noon(Monday))!;

        goal.VetoChange(Guid.CreateVersion7(), friends[0], Noon(Monday));
        goal.VetoChange(Guid.CreateVersion7(), friends[0], Noon(Monday));

        Assert.Equal(0, change.VetoCount);
    }

    /// <summary>
    /// Nothing to object to in a change that only asks more of the owner.
    /// </summary>
    [Fact]
    public void AStricterChangeCannotBeObjectedTo()
    {
        var friends = new[] { Guid.CreateVersion7() };
        var goal = Running(GoalSchedule.EveryNDays(2), friends);
        Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(1)), Noon(Monday));

        Assert.Null(goal.VetoChange(Guid.CreateVersion7(), friends[0], Noon(Monday)));
    }

    [Fact]
    public void RemovingTheLastCheckerIsRefused()
    {
        var goal = Running(GoalSchedule.EveryNDays(1), [Guid.CreateVersion7()]);

        var refused = Assert.Throws<DomainValidationException>(() =>
            goal.PlanEdit(new GoalEdit(CheckerIds: []), Monday, Noon(Monday)));

        Assert.True(refused.Errors.ContainsKey("ParticipantIds"));
    }

    [Fact]
    public void AnAddedCheckerIsOnAtOnceAndARemovedOneLeavesWithTheChange()
    {
        var stays = Guid.CreateVersion7();
        var leaves = Guid.CreateVersion7();
        var joins = Guid.CreateVersion7();
        var goal = Running(GoalSchedule.EveryNDays(1), [stays, leaves]);

        var change = Edit(goal, new GoalEdit(CheckerIds: [stays, joins]), Noon(Monday))!;

        Assert.True(goal.CanVote(joins));
        Assert.True(goal.CanVote(leaves));
        Assert.True(change.Objectable);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday.AddDays(2)), Ids);

        Assert.False(goal.CanVote(leaves));
        Assert.Equal(new[] { stays, joins }.Order(), goal.Participants.Select(participant => participant.PersonId).Order());
    }

    /// <summary>
    /// One change waits at a time. A title edited the next day does not take
    /// back the schedule change made the day before, nor restart its clock.
    /// </summary>
    [Fact]
    public void AnEditThatLeavesTheWaitingChangeAloneKeepsIt()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        var waiting = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(2)), Noon(Monday))!;

        var titled = Edit(goal, new GoalEdit(Title: "Joggen"), Noon(Monday.AddDays(1)))!;

        Assert.Same(waiting, goal.PendingChange);
        Assert.Equal(GoalChangeStatus.None, titled.Status);
    }

    [Fact]
    public void ANewWaitingChangeReplacesTheOldOne()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        var first = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(2)), Noon(Monday))!;

        var second = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(3)), Noon(Monday))!;

        Assert.Equal(GoalChangeStatus.Superseded, first.Status);
        Assert.Same(second, goal.PendingChange);
    }

    [Fact]
    public void AskingForWhatIsThereAlreadyWithdrawsTheWaitingChange()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        var waiting = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(2)), Noon(Monday))!;

        Assert.Null(Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(1)), Noon(Monday)));

        Assert.Equal(GoalChangeStatus.Withdrawn, waiting.Status);
        Assert.Null(goal.PendingChange);
    }

    [Fact]
    public void StoppingTheGoalDropsTheWaitingChange()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));
        var waiting = Edit(goal, new GoalEdit(Schedule: GoalSchedule.EveryNDays(2)), Noon(Monday))!;

        goal.Close(completed: false, Noon(Monday));

        Assert.Equal(GoalChangeStatus.Superseded, waiting.Status);
        Assert.Throws<DomainValidationException>(() => goal.PlanEdit(new GoalEdit(Title: "Neu"), Monday, Noon(Monday)));
    }

    [Fact]
    public void AOneOffStaysAOneOffAndARepeatingGoalKeepsRepeating()
    {
        var once = Running(GoalSchedule.Once(), targetDate: Monday.AddDays(5));
        var daily = Running(GoalSchedule.EveryNDays(1));

        var toRepeating = Assert.Throws<DomainValidationException>(() =>
            once.PlanEdit(new GoalEdit(Schedule: GoalSchedule.EveryNDays(1)), Monday, Noon(Monday)));
        var toOnce = Assert.Throws<DomainValidationException>(() =>
            daily.PlanEdit(new GoalEdit(Schedule: GoalSchedule.Once()), Monday, Noon(Monday)));

        Assert.Equal(FieldErrorCodes.KindFixed, toRepeating.Codes!["Schedule"][0].Code);
        Assert.Equal(FieldErrorCodes.KindFixed, toOnce.Codes!["Schedule"][0].Code);
    }

    [Fact]
    public void AnEarlierDayForAOneOffAppliesAtOnce()
    {
        var goal = Running(GoalSchedule.Once(), targetDate: Monday.AddDays(5));

        var change = Edit(goal, new GoalEdit(TargetDate: Monday.AddDays(2)), Noon(Monday))!;

        Assert.Equal(GoalChangeStatus.None, change.Status);
        Assert.Equal(Monday.AddDays(2), goal.CurrentInstance!.DueOn);
        Assert.Equal(Monday.AddDays(2), goal.TargetDate);
    }

    [Fact]
    public void ALaterDayForAOneOffMovesTheDeadlineADayAfterTheEdit()
    {
        var goal = Running(GoalSchedule.Once(), targetDate: Monday.AddDays(5));

        var change = Edit(goal, new GoalEdit(TargetDate: Monday.AddDays(9)), Noon(Monday))!;

        Assert.True(change.Objectable);
        Assert.Equal(Monday.AddDays(5), goal.CurrentInstance!.DueOn);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday).AddHours(23), Ids);
        Assert.Equal(GoalChangeStatus.Pending, change.Status);

        GoalMaintenance.Advance(goal, Utc, Noon(Monday).AddHours(25), Ids);
        Assert.Equal(GoalChangeStatus.Applied, change.Status);
        Assert.Equal(Monday.AddDays(9), goal.CurrentInstance!.DueOn);
    }

    /// <summary>
    /// A later day waits a day for objections, so it cannot be the way past a
    /// deadline that is about to pass.
    /// </summary>
    [Fact]
    public void AOneOffCannotBeMovedLaterOnItsLastDay()
    {
        var goal = Running(GoalSchedule.Once(), targetDate: Monday);

        var refused = Assert.Throws<DomainValidationException>(() =>
            goal.PlanEdit(new GoalEdit(TargetDate: Monday.AddDays(3)), Monday, Noon(Monday)));

        Assert.Equal(FieldErrorCodes.TooLateToMove, refused.Codes!["TargetDate"][0].Code);
    }

    [Fact]
    public void ALimitBelongsToADontOnly()
    {
        var goal = Running(GoalSchedule.EveryNDays(1));

        var refused = Assert.Throws<DomainValidationException>(() =>
            goal.PlanEdit(new GoalEdit(Limit: 2), Monday, Noon(Monday)));

        Assert.True(refused.Errors.ContainsKey("Limit"));
    }

    [Fact]
    public void WhenAChangeAppliesIsTheDayOfTheWindowItAppliesWith()
    {
        var goal = Running(GoalSchedule.TimesPer(3, QuotaPeriod.Week));
        Edit(goal, new GoalEdit(Schedule: GoalSchedule.TimesPer(2, QuotaPeriod.Week)), Noon(Monday.AddDays(6)));

        // Sunday noon: next Monday is less than a day away, so the Monday after.
        Assert.Equal(Monday.AddDays(14), GoalMaintenance.WhenPendingChangeApplies(goal, Utc));
    }

    private static GoalChange? Edit(Goal goal, GoalEdit edit, DateTimeOffset now)
    {
        var plan = goal.PlanEdit(edit, Utc.Today(now), now);
        return goal.Edit(Guid.CreateVersion7(), plan, Ids.NewId, Utc.EndOfDay, now);
    }

    /// <summary>A goal created at the start of <see cref="Monday"/>, with its first window open.</summary>
    private static Goal Running(GoalSchedule schedule, IReadOnlyList<Guid>? checkers = null, DateOnly? targetDate = null)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            "target",
            schedule,
            isGroup: false,
            reminderAt: null,
            targetDate,
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        foreach (var checker in checkers ?? [Guid.CreateVersion7()])
        {
            goal.AddParticipant(Guid.CreateVersion7(), checker);
        }

        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        return goal;
    }

    private static Goal RunningDont(int limit, QuotaPeriod period, DontProofMode mode)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Owner,
            "Anything",
            null,
            "target",
            GoalSchedule.Per(period),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            new DateTimeOffset(Monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            GoalKind.Dont,
            limit,
            mode);

        goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        GoalMaintenance.Advance(goal, Utc, Noon(Monday), Ids);
        return goal;
    }

    private static DateTimeOffset Noon(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}

internal static class DateTimeExtensions
{
    public static DateTimeOffset ToUtc(this DateTime local) => new(local, TimeSpan.Zero);
}
