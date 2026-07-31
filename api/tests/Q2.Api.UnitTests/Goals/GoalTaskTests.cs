using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// Which day a task falls on, and what ticking it off means.
/// </summary>
/// <remarks>
/// The scheduling cases are the reason this file exists: "is this on today's
/// list?" is the only rule in q2 that depends on the day of the week, and it is
/// the one a client must never be left to work out for itself.
/// </remarks>
public class GoalTaskTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    // 2026-06-15 is a Monday; the 20th is the Saturday of the same week.
    private static readonly DateOnly Monday = new(2026, 6, 15);
    private static readonly DateOnly Saturday = new(2026, 6, 20);

    private static GoalTask Build(
        GoalRhythm rhythm = GoalRhythm.Daily,
        DayOfWeek? weeklyOn = null,
        DateOnly? dueOn = null,
        double? measured = null,
        double? target = null,
        string? unit = null) =>
        GoalTask.Create(
            Guid.CreateVersion7(),
            goalId: null,
            "Joggen 5 km",
            rhythm,
            reminderAt: new TimeOnly(7, 0),
            weeklyOn,
            dueOn,
            measured,
            target,
            unit,
            sortOrder: 0,
            Created);

    [Fact]
    public void ADailyTaskIsOnEveryDay()
    {
        var task = Build();

        Assert.True(task.IsScheduledFor(Monday));
        Assert.True(task.IsScheduledFor(Saturday));
    }

    [Fact]
    public void AWeekdaysTaskSkipsTheWeekend()
    {
        var task = Build(GoalRhythm.Weekdays);

        Assert.True(task.IsScheduledFor(Monday));
        Assert.False(task.IsScheduledFor(Saturday));
        Assert.False(task.IsScheduledFor(Saturday.AddDays(1)));
    }

    [Fact]
    public void AWeeklyTaskIsOnlyOnItsOwnDay()
    {
        var task = Build(GoalRhythm.Weekly, weeklyOn: DayOfWeek.Monday);

        Assert.True(task.IsScheduledFor(Monday));
        Assert.False(task.IsScheduledFor(Monday.AddDays(1)));
    }

    [Fact]
    public void AWeeklyTaskWithoutADayIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(GoalRhythm.Weekly));

        Assert.Contains(nameof(GoalTask.WeeklyOn), error.Errors.Keys);
    }

    [Fact]
    public void AOneOffTaskIsOnlyOnTheDayItIsDue()
    {
        var task = Build(GoalRhythm.Once, dueOn: Monday);

        Assert.True(task.IsScheduledFor(Monday));
        Assert.False(task.IsScheduledFor(Monday.AddDays(1)));
    }

    [Fact]
    public void AOneOffTaskWithoutADueDateIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(GoalRhythm.Once));

        Assert.Contains(nameof(GoalTask.DueOn), error.Errors.Keys);
    }

    [Fact]
    public void DoneIsAskedPerDayRatherThanStored()
    {
        var task = Build();

        task.Toggle(Monday);

        Assert.True(task.IsDoneOn(Monday));

        // The same daily task is open again tomorrow, with no nightly job
        // involved.
        Assert.False(task.IsDoneOn(Monday.AddDays(1)));
    }

    [Fact]
    public void TogglingTwiceOnTheSameDayLeavesItOpen()
    {
        var task = Build();

        Assert.True(task.Toggle(Monday));
        Assert.False(task.Toggle(Monday));
        Assert.False(task.IsDoneOn(Monday));
    }

    [Fact]
    public void TickingOffAMeasurableTaskFillsItToItsTarget()
    {
        var task = Build(measured: 1.2, target: 2, unit: "L");

        task.Toggle(Monday);

        Assert.Equal(2, task.MeasuredValue);
    }

    [Fact]
    public void UnTickingAMeasurableTaskLeavesTheAmountAlone()
    {
        var task = Build(measured: 1.2, target: 2, unit: "L");

        task.Toggle(Monday);
        task.Toggle(Monday);

        // What was drunk was drunk.
        Assert.Equal(2, task.MeasuredValue);
    }

    [Fact]
    public void AMeasuredAmountWithoutATargetIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(measured: 1.2));

        Assert.Contains(nameof(GoalTask.TargetValue), error.Errors.Keys);
    }

    [Fact]
    public void ATargetOfZeroIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(measured: 0, target: 0));

        Assert.Contains(nameof(GoalTask.TargetValue), error.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTitleIsRejected(string title)
    {
        var error = Assert.Throws<DomainValidationException>(() => GoalTask.Create(
            Guid.CreateVersion7(), null, title, GoalRhythm.Daily, null, null, null, null, null, null, 0, Created));

        Assert.Contains(nameof(GoalTask.Title), error.Errors.Keys);
    }
}
