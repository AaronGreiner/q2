using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// A Do that waits for its owner's first friend, and the goal it becomes
/// (docs/adr/0039-first-run-onboarding.md).
/// </summary>
public class GoalDraftTests
{
    private static readonly DateTimeOffset Drafted = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = Drafted.AddDays(3);
    private static readonly Guid Owner = new("00000000-0000-4000-8000-000000000001");

    private static GoalDraft Build(GoalSchedule? schedule = null, string title = "  Jeden Tag lesen ") =>
        GoalDraft.Create(
            Guid.CreateVersion7(),
            Owner,
            title,
            GoalCategory.Reading,
            schedule ?? GoalSchedule.TimesPer(3, QuotaPeriod.Week),
            new TimeOnly(19, 0),
            Drafted);

    [Fact]
    public void ItKeepsWhatWasDescribed()
    {
        var draft = Build();

        Assert.Equal("Jeden Tag lesen", draft.Title);
        Assert.Equal("book-open", draft.Icon);
        Assert.Equal(ScheduleKind.Times, draft.Schedule.Kind);
        Assert.Equal(new TimeOnly(19, 0), draft.ReminderAt);
    }

    [Fact]
    public void AOneOffCannotWait()
    {
        // Its day could pass before anybody arrives.
        var error = Assert.Throws<DomainValidationException>(() => Build(GoalSchedule.Once()));

        Assert.Contains(nameof(GoalDraft.Schedule), error.Errors.Keys);
    }

    [Fact]
    public void ADontsLimitIsNotAWaitingDo()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(GoalSchedule.Per(QuotaPeriod.Day)));

        Assert.Contains(nameof(GoalDraft.Schedule), error.Errors.Keys);
    }

    [Fact]
    public void ATitleIsRequired()
    {
        var error = Assert.Throws<DomainValidationException>(() => Build(title: "   "));

        Assert.Contains(nameof(GoalDraft.Title), error.Errors.Keys);
    }

    [Fact]
    public void AnUnknownCategoryIsRefused()
    {
        var draft = Build();

        var error = Assert.Throws<DomainValidationException>(
            () => draft.Describe("Lesen", (GoalCategory)42, GoalSchedule.EveryNDays(1), null));

        Assert.Contains(nameof(GoalDraft.Category), error.Errors.Keys);
        Assert.Equal("Jeden Tag lesen", draft.Title);
    }

    [Fact]
    public void DescribingAgainReplacesIt()
    {
        var draft = Build();

        draft.Describe("Laufen", null, GoalSchedule.EveryNDays(2), null);

        Assert.Equal("Laufen", draft.Title);
        Assert.Equal(GoalCategories.Default, draft.Category);
        Assert.Equal(2, draft.Schedule.EveryDays);
        Assert.Null(draft.ReminderAt);
    }

    [Fact]
    public void StartingItMakesTheDoItDescribes()
    {
        var draft = Build();

        var goal = draft.Start(Guid.CreateVersion7(), Started);

        Assert.Equal(Owner, goal.OwnerPersonId);
        Assert.Equal("Jeden Tag lesen", goal.Title);
        Assert.Equal(GoalKind.Do, goal.Kind);
        Assert.Equal(GoalStatus.Active, goal.Status);
        Assert.Equal(new TimeOnly(19, 0), goal.ReminderAt);
        Assert.Equal(ScheduleKind.Times, goal.Schedule.Kind);
        Assert.Equal(3, goal.Schedule.Times);
        Assert.Equal(QuotaPeriod.Week, goal.Schedule.Period);

        // From the moment somebody is there to see it, not from the draft.
        Assert.Equal(Started, goal.CreatedAt);

        // Its own schedule: EF Core owns one through exactly one row.
        Assert.NotSame(draft.Schedule, goal.Schedule);
    }
}
