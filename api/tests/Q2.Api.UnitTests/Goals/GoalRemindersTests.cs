using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// When a Do's daily reminder goes out (#82) — and, mostly, when it does not.
/// </summary>
public sealed class GoalRemindersTests
{
    private static readonly LocalCalendar Berlin =
        new(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

    private static readonly DateOnly Monday = new(2026, 6, 15);

    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0) =>
        Berlin.StartOfDay(day).AddHours(hour).AddMinutes(minute);

    private static Goal Goal(
        GoalSchedule? schedule = null,
        TimeOnly? reminderAt = null,
        DateOnly? start = null,
        DateOnly? due = null,
        int requiredProofs = 1)
    {
        var first = start ?? Monday;

        var goal = Q2.Api.Features.Goals.Goal.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Meditation",
            null,
            null,
            schedule ?? GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: reminderAt ?? new TimeOnly(8, 0),
            targetDate: null,
            createdAt: At(first, 7));

        goal.OpenWindow(
            Guid.CreateVersion7(),
            new GoalWindow(first, due ?? first, requiredProofs),
            Berlin.StartOfDay(first),
            Berlin.EndOfDay(due ?? first));

        return goal;
    }

    [Fact]
    public void RemindsFromTheTimeItWasSetFor()
    {
        var goal = Goal();

        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday, 7, 59)));
        Assert.Same(goal.CurrentInstance, GoalReminders.Due(goal, Berlin, At(Monday, 8)));

        // Late rather than never: a pass that comes round after it still reminds.
        Assert.Same(goal.CurrentInstance, GoalReminders.Due(goal, Berlin, At(Monday, 15)));
    }

    [Fact]
    public void RemindsOnceADay()
    {
        var goal = Goal();
        var window = GoalReminders.Due(goal, Berlin, At(Monday, 8))!;

        Assert.True(window.Remind(Monday));
        Assert.False(window.Remind(Monday));
        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday, 8, 10)));
    }

    [Fact]
    public void AQuotaIsRemindedEveryDayUntilItIsMet()
    {
        var goal = Goal(GoalSchedule.TimesPer(2, QuotaPeriod.Week), due: Monday.AddDays(6), requiredProofs: 2);
        var window = goal.CurrentInstance!;

        Assert.True(GoalReminders.Due(goal, Berlin, At(Monday, 9))!.Remind(Monday));
        Assert.Same(window, GoalReminders.Due(goal, Berlin, At(Monday.AddDays(1), 9)));

        // Both photographs believed: nothing left to be reminded of this week.
        for (var proof = 0; proof < 2; proof++)
        {
            var photograph = window.SubmitProof(Guid.CreateVersion7(), goal.OwnerPersonId, Guid.CreateVersion7(), ProofSource.Live, At(Monday, 10))!;
            window.ApplyProofOutcome(photograph, VotingResult.Confirmed, At(Monday, 10));
        }

        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday.AddDays(2), 9)));
    }

    [Fact]
    public void APhotographBeingLookedAtIsNotRemindedAbout()
    {
        var goal = Goal();
        goal.CurrentInstance!.SubmitProof(Guid.CreateVersion7(), goal.OwnerPersonId, Guid.CreateVersion7(), ProofSource.Live, At(Monday, 7, 30));

        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday, 8)));
    }

    [Fact]
    public void AGoalWithoutAReminderIsNeverReminded()
    {
        var goal = Goal();
        goal.SetReminder(null);

        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday, 12)));
    }

    [Fact]
    public void AWindowThatHasNotStartedIsNotRemindedAbout()
    {
        // A weekday goal whose next window is tomorrow.
        var goal = Goal(start: Monday.AddDays(1));

        Assert.Null(GoalReminders.Due(goal, Berlin, At(Monday, 12)));
    }

    [Fact]
    public void ADontIsNeverReminded()
    {
        var dont = Q2.Api.Features.Goals.Goal.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Kein Zucker",
            null,
            null,
            GoalSchedule.Per(QuotaPeriod.Day),
            isGroup: false,
            reminderAt: new TimeOnly(8, 0),
            targetDate: null,
            createdAt: At(Monday, 7),
            GoalKind.Dont,
            limit: 0,
            DontProofMode.HonestOnly);

        dont.OpenWindow(Guid.CreateVersion7(), new GoalWindow(Monday, Monday, 0), Berlin.StartOfDay(Monday), Berlin.EndOfDay(Monday));

        Assert.Null(GoalReminders.Due(dont, Berlin, At(Monday, 12)));
    }
}
