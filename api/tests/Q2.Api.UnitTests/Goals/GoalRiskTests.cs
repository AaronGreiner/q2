using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// When friends are told that somebody is about to miss.
/// </summary>
/// <remarks>
/// This is the half of "do it or shame it" that still has a use, and it is one
/// bad rule away from being muted forever. So the tests are mostly about when
/// the warning does <em>not</em> go out: too early in the day, when a
/// photograph is already being voted on, when there is still room in the week.
/// </remarks>
public sealed class GoalRiskTests
{
    private static readonly LocalCalendar Berlin =
        new(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

    private static readonly DateOnly Monday = new(2026, 6, 15);

    /// <summary>An instant at a given local hour on a given local day.</summary>
    private static DateTimeOffset At(DateOnly day, int hour) =>
        Berlin.StartOfDay(day).AddHours(hour);

    private static GoalInstance Window(
        DateOnly start,
        DateOnly due,
        int requiredProofs = 1,
        int confirmedProofs = 0)
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Test",
            null,
            null,
            requiredProofs > 1 ? GoalSchedule.TimesPer(requiredProofs, QuotaPeriod.Week) : GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            createdAt: At(start, 8));

        var instance = goal.OpenWindow(
            Guid.CreateVersion7(),
            new GoalWindow(start, due, requiredProofs),
            Berlin.StartOfDay(start),
            Berlin.EndOfDay(due))!;

        for (var proof = 0; proof < confirmedProofs; proof++)
        {
            var photograph = instance.SubmitProof(Guid.CreateVersion7(), goal.OwnerPersonId, Guid.CreateVersion7(), true, At(start, 9))!;
            instance.ApplyProofOutcome(photograph, VotingResult.Confirmed, At(start, 9));
        }

        return instance;
    }

    [Fact]
    public void WarnsOnTheEveningOfTheLastDay()
    {
        var assessment = GoalRisk.Assess(Window(Monday, Monday), Berlin, At(Monday, 20));

        Assert.NotNull(assessment);
        Assert.Equal(RiskReason.LastDay, assessment.Value.Reason);
        Assert.Equal(1, assessment.Value.MissingProofs);
        Assert.Equal(1, assessment.Value.RemainingDays);
    }

    [Fact]
    public void SaysNothingBeforeTheEvening()
    {
        // Somebody who has delivered nothing by lunchtime is not failing, they
        // are having an ordinary day.
        Assert.Null(GoalRisk.Assess(Window(Monday, Monday), Berlin, At(Monday, 19)));
        Assert.Null(GoalRisk.Assess(Window(Monday, Monday), Berlin, At(Monday, 12)));
    }

    [Fact]
    public void UsesTheOwnersEveningRatherThanTheServersHour()
    {
        // 20:00 in Berlin is 18:00 UTC. Reading the server's hour would warn
        // two hours late here — and, for somebody further west, in the morning.
        var window = Window(Monday, Monday);
        var berlinEvening = new DateTimeOffset(2026, 6, 15, 18, 0, 0, TimeSpan.Zero);

        Assert.NotNull(GoalRisk.Assess(window, Berlin, berlinEvening));
        Assert.Null(GoalRisk.Assess(window, Berlin, berlinEvening.AddHours(-1)));
    }

    [Fact]
    public void SaysNothingAboutAWindowThatIsAlreadyFull()
    {
        Assert.Null(GoalRisk.Assess(Window(Monday, Monday, confirmedProofs: 1), Berlin, At(Monday, 21)));
    }

    /// <summary>
    /// The rule that keeps the warning honest. Warning about the person who has
    /// just handed in is the fastest way to teach everybody to ignore this.
    /// </summary>
    [Fact]
    public void SaysNothingWhileAPhotographIsBeingVotedOn()
    {
        var window = Window(Monday, Monday);
        window.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), true, At(Monday, 19));

        Assert.Null(GoalRisk.Assess(window, Berlin, At(Monday, 21)));
    }

    [Fact]
    public void SaysNothingOnceTheDeadlineHasPassed()
    {
        // Past the deadline it is no longer a risk. It is a result, and the
        // maintenance job is about to say so.
        Assert.Null(GoalRisk.Assess(Window(Monday, Monday), Berlin, At(Monday.AddDays(1), 21)));
    }

    [Fact]
    public void SaysNothingOnAnEarlyEveningOfALongWindow()
    {
        // Monday evening of a week that wants three, none in yet: unpleasant,
        // but there are six days for three proofs. Nothing has gone wrong.
        var week = Window(Monday, Monday.AddDays(6), requiredProofs: 3);

        Assert.Null(GoalRisk.Assess(week, Berlin, At(Monday, 21)));
    }

    /// <summary>
    /// The point on a quota where it stops being comfortable: from here on,
    /// something has to be delivered every remaining day.
    /// </summary>
    [Fact]
    public void WarnsWhenAQuotaNeedsEveryRemainingDay()
    {
        var week = Window(Monday, Monday.AddDays(6), requiredProofs: 3);
        var thursday = Monday.AddDays(4);

        var assessment = GoalRisk.Assess(week, Berlin, At(thursday, 20));

        Assert.NotNull(assessment);
        Assert.Equal(RiskReason.Tight, assessment.Value.Reason);
        Assert.Equal(3, assessment.Value.MissingProofs);
        Assert.Equal(3, assessment.Value.RemainingDays);
    }

    [Fact]
    public void AWindowWantingOneProofIsNeverMerelyTight()
    {
        // With a single required proof there is no "every remaining day" point:
        // only the last day decides, whatever the length of the window.
        var week = Window(Monday, Monday.AddDays(6));

        Assert.Null(GoalRisk.Assess(week, Berlin, At(Monday.AddDays(5), 21)));

        var lastDay = GoalRisk.Assess(week, Berlin, At(Monday.AddDays(6), 21));
        Assert.Equal(RiskReason.LastDay, lastDay!.Value.Reason);
    }

    [Fact]
    public void CountsWhatIsStillOutstandingRatherThanWhatWasAskedFor()
    {
        var week = Window(Monday, Monday.AddDays(6), requiredProofs: 3, confirmedProofs: 1);

        var assessment = GoalRisk.Assess(week, Berlin, At(Monday.AddDays(5), 20));

        Assert.NotNull(assessment);
        Assert.Equal(2, assessment.Value.MissingProofs);
        Assert.Equal(3, assessment.Value.RequiredProofs);
    }
}
