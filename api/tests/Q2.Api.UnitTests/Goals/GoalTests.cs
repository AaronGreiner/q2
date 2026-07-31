using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

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
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static Goal Build(int completedSteps = 0, int totalSteps = 10, DateOnly? targetDate = null) =>
        Goal.Create(
            Guid.CreateVersion7(),
            "Walk 8.000 steps a day",
            null,
            "target",
            GoalRhythm.Daily,
            isGroup: false,
            completedSteps,
            totalSteps,
            reminderAt: null,
            targetDate,
            Created);

    [Fact]
    public void ATitleIsRequiredAndIsTrimmed()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(), "  Read every day  ", null, "book-open", GoalRhythm.Daily,
            false, 0, 30, null, null, Created);

        Assert.Equal("Read every day", goal.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTitleIsRejected(string title)
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), title, null, "target", GoalRhythm.Daily, false, 0, 10, null, null, Created));

        Assert.Contains(nameof(Goal.Title), error.Errors.Keys);
    }

    [Fact]
    public void ATitleLongerThanTheLimitIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), new string('a', Goal.MaxTitleLength + 1), null, "target", GoalRhythm.Daily,
            false, 0, 10, null, null, Created));

        Assert.Contains(nameof(Goal.Title), error.Errors.Keys);
    }

    [Fact]
    public void AnUnknownIconIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), "Anything", null, "rocket", GoalRhythm.Daily, false, 0, 10, null, null, Created));

        Assert.Contains(nameof(Goal.Icon), error.Errors.Keys);
    }

    [Fact]
    public void AMissingIconFallsBackToTheDefaultRatherThanFailing()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(), "Anything", null, null, GoalRhythm.Daily, false, 0, 10, null, null, Created);

        Assert.Equal(GoalIcons.Default, goal.Icon);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Goal.MaxSteps + 1)]
    public void AnImpossibleNumberOfStepsIsRejected(int totalSteps)
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), "Anything", null, "target", GoalRhythm.Daily, false, 0, totalSteps, null, null, Created));

        Assert.Contains(nameof(Goal.TotalSteps), error.Errors.Keys);
    }

    [Fact]
    public void MoreCompletedStepsThanTotalStepsIsRejected()
    {
        var error = Assert.Throws<DomainValidationException>(() => Goal.Create(
            Guid.CreateVersion7(), "Anything", null, "target", GoalRhythm.Daily, false, 11, 10, null, null, Created));

        Assert.Contains(nameof(Goal.CompletedSteps), error.Errors.Keys);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 50)]
    [InlineData(14, 21, 67)]
    [InlineData(10, 10, 100)]
    public void ProgressIsDerivedFromTheSteps(int completed, int total, int expected)
    {
        Assert.Equal(expected, Build(completed, total).ProgressPercent);
    }

    [Fact]
    public void AGoalThatStartsAtItsTotalIsAlreadyCompleted()
    {
        Assert.Equal(GoalStatus.Completed, Build(10, 10).Status);
    }

    [Fact]
    public void ContributingAddsOneStepAndOneDay()
    {
        var goal = Build(4, 10);

        Assert.True(goal.Contribute(Guid.CreateVersion7(), Today));

        Assert.Equal(5, goal.CompletedSteps);
        Assert.Equal(50, goal.ProgressPercent);
        Assert.Single(goal.Contributions);
        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public void ASecondContributionOnTheSameDayCountsAsAStepButNotAsADay()
    {
        var goal = Build(4, 10);

        goal.Contribute(Guid.CreateVersion7(), Today);
        goal.Contribute(Guid.CreateVersion7(), Today);

        Assert.Equal(6, goal.CompletedSteps);
        Assert.Single(goal.Contributions);
        Assert.Equal(1, goal.StreakOn(Today));
    }

    [Fact]
    public void TheLastStepCompletesTheGoal()
    {
        var goal = Build(9, 10);

        goal.Contribute(Guid.CreateVersion7(), Today);

        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.Equal(100, goal.ProgressPercent);
    }

    [Fact]
    public void ContributingToACompletedGoalChangesNothingAndSaysSo()
    {
        var goal = Build(10, 10);

        Assert.False(goal.Contribute(Guid.CreateVersion7(), Today));
        Assert.Equal(10, goal.CompletedSteps);
    }

    [Fact]
    public void AnArchivedGoalIsNotReopenedByContributing()
    {
        var goal = Build(4, 10);
        goal.Archive();

        Assert.False(goal.Contribute(Guid.CreateVersion7(), Today));
        Assert.Equal(GoalStatus.Archived, goal.Status);
    }

    [Fact]
    public void TheStreakCountsConsecutiveDaysEndingToday()
    {
        var goal = Build(4, 10);

        foreach (var offset in new[] { 0, 1, 2, 4 })
        {
            goal.RecordContribution(Guid.CreateVersion7(), Today.AddDays(-offset));
        }

        // Three days back to back, then a gap: the day before the gap does not
        // extend the run.
        Assert.Equal(3, goal.StreakOn(Today));
    }

    [Fact]
    public void AParticipantIsAddedOnceHoweverOftenTheyAreOffered()
    {
        var goal = Build();
        var person = Guid.CreateVersion7();

        goal.AddParticipant(Guid.CreateVersion7(), person);
        goal.AddParticipant(Guid.CreateVersion7(), person);

        Assert.Single(goal.Participants);
    }

    [Fact]
    public void MoreParticipantsThanTheLimitAreRejected()
    {
        var goal = Build();

        for (var i = 0; i < Goal.MaxParticipants; i++)
        {
            goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        var error = Assert.Throws<DomainValidationException>(
            () => goal.AddParticipant(Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.Contains(nameof(Goal.Participants), error.Errors.Keys);
    }

    [Fact]
    public void AGoalIsOverdueOnceItsTargetDateHasPassed()
    {
        Assert.True(Build(targetDate: Today.AddDays(-1)).IsOverdue(Today));
    }

    [Fact]
    public void AGoalDueTodayIsNotOverdueYet()
    {
        Assert.False(Build(targetDate: Today).IsOverdue(Today));
    }

    [Fact]
    public void ACompletedGoalIsNeverOverdue()
    {
        Assert.False(Build(10, 10, Today.AddDays(-30)).IsOverdue(Today));
    }

    [Fact]
    public void AGoalWithoutATargetDateIsNeverOverdue()
    {
        Assert.False(Build().IsOverdue(Today));
    }
}
