using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// The domain rules. No database, no host, no clock — <see cref="Goal.Create"/>
/// takes its id and timestamp as arguments, so every case here is a pure
/// function call.
/// </summary>
[Trait("Category", "Unit")]
public class GoalTests
{
    private static readonly Guid Id = new("11111111-1111-4111-8111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static Goal Create(
        string title = "Walk every day",
        string? description = null,
        int progress = 0,
        DateOnly? targetDate = null) =>
        Goal.Create(Id, title, description, progress, targetDate, CreatedAt);

    [Fact]
    public void Create_SetsTheSuppliedValues()
    {
        var goal = Create("Read more", "Ten pages a day", 25, new DateOnly(2026, 6, 1));

        Assert.Equal(Id, goal.Id);
        Assert.Equal("Read more", goal.Title);
        Assert.Equal("Ten pages a day", goal.Description);
        Assert.Equal(25, goal.ProgressPercent);
        Assert.Equal(new DateOnly(2026, 6, 1), goal.TargetDate);
        Assert.Equal(CreatedAt, goal.CreatedAt);
        Assert.Equal(GoalStatus.Active, goal.Status);
        Assert.Empty(goal.Participants);
    }

    [Fact]
    public void Create_TrimsTitleAndDescription()
    {
        var goal = Create("  Stretch  ", "  Every morning  ");

        Assert.Equal("Stretch", goal.Title);
        Assert.Equal("Every morning", goal.Description);
    }

    [Fact]
    public void Create_TurnsBlankDescriptionIntoNull()
    {
        Assert.Null(Create(description: "   ").Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingTitle(string title)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(title));

        Assert.Contains(nameof(Goal.Title), exception.Errors.Keys);
    }

    [Fact]
    public void Create_RejectsTitleAboveTheLimit()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Create(new string('a', Goal.MaxTitleLength + 1)));

        Assert.Contains(nameof(Goal.Title), exception.Errors.Keys);
    }

    [Fact]
    public void Create_AcceptsTitleExactlyAtTheLimit()
    {
        var goal = Create(new string('a', Goal.MaxTitleLength));

        Assert.Equal(Goal.MaxTitleLength, goal.Title.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_RejectsProgressOutsideZeroToHundred(int progress)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(progress: progress));

        Assert.Contains(nameof(Goal.ProgressPercent), exception.Errors.Keys);
    }

    [Fact]
    public void Create_ReportsEveryProblemAtOnce()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(title: "", progress: 500));

        // A caller should not have to fix one field, resubmit, and discover the next.
        Assert.Equal(2, exception.Errors.Count);
    }

    [Fact]
    public void Create_WithFullProgress_IsAlreadyCompleted()
    {
        Assert.Equal(GoalStatus.Completed, Create(progress: 100).Status);
    }

    [Fact]
    public void UpdateProgress_ToHundred_CompletesTheGoal()
    {
        var goal = Create(progress: 40);

        goal.UpdateProgress(100);

        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.Equal(100, goal.ProgressPercent);
    }

    [Fact]
    public void UpdateProgress_BelowHundred_ReopensACompletedGoal()
    {
        var goal = Create(progress: 100);

        goal.UpdateProgress(80);

        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public void UpdateProgress_LeavesAnArchivedGoalArchived()
    {
        var goal = Create(progress: 50);
        goal.Archive();

        goal.UpdateProgress(100);

        // Reopening an archived goal is a deliberate action, not a side effect
        // of recording progress.
        Assert.Equal(GoalStatus.Archived, goal.Status);
        Assert.Equal(100, goal.ProgressPercent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateProgress_RejectsValuesOutsideTheRange(int progress)
    {
        var goal = Create();

        Assert.Throws<DomainValidationException>(() => goal.UpdateProgress(progress));
    }

    [Fact]
    public void AddParticipant_StoresTrimmedNames()
    {
        var goal = Create();

        goal.AddParticipant(Guid.NewGuid(), "  Robin Sample  ");

        Assert.Equal(["Robin Sample"], goal.Participants.Select(p => p.DisplayName));
    }

    [Fact]
    public void AddParticipant_IgnoresCaseInsensitiveDuplicates()
    {
        var goal = Create();

        goal.AddParticipant(Guid.NewGuid(), "Robin Sample");
        goal.AddParticipant(Guid.NewGuid(), "robin sample");

        Assert.Single(goal.Participants);
    }

    [Fact]
    public void AddParticipant_RejectsBlankNames()
    {
        var goal = Create();

        Assert.Throws<DomainValidationException>(() => goal.AddParticipant(Guid.NewGuid(), "  "));
    }

    [Fact]
    public void AddParticipant_RejectsMoreThanTheMaximum()
    {
        var goal = Create();
        for (var i = 0; i < Goal.MaxParticipants; i++)
        {
            goal.AddParticipant(Guid.NewGuid(), $"Participant {i}");
        }

        Assert.Throws<DomainValidationException>(() => goal.AddParticipant(Guid.NewGuid(), "One too many"));
    }

    [Fact]
    public void IsOverdue_IsTrue_ForAPastTargetDateOnAnActiveGoal()
    {
        var goal = Create(targetDate: new DateOnly(2026, 5, 1), progress: 30);

        Assert.True(goal.IsOverdue(new DateOnly(2026, 5, 2)));
    }

    [Fact]
    public void IsOverdue_IsFalse_OnTheTargetDateItself()
    {
        var goal = Create(targetDate: new DateOnly(2026, 5, 1), progress: 30);

        Assert.False(goal.IsOverdue(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void IsOverdue_IsFalse_ForACompletedGoal()
    {
        var goal = Create(targetDate: new DateOnly(2026, 5, 1), progress: 100);

        Assert.False(goal.IsOverdue(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void IsOverdue_IsFalse_WithoutATargetDate()
    {
        var goal = Create(targetDate: null, progress: 10);

        Assert.False(goal.IsOverdue(new DateOnly(2099, 1, 1)));
    }
}
