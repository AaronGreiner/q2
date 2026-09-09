using Q2.Api.Features.Goals;

namespace Q2.Api.UnitTests.Goals;

/// <summary>
/// The request validators.
/// </summary>
/// <remarks>
/// Not a duplicate of the domain rules: this is the layer that turns a request
/// into good field-level messages. What it must never do is disagree with the
/// domain about what is allowed, which is why every limit here is read from the
/// same constants the entities use.
/// </remarks>
public class CreateGoalRequestValidatorTests
{
    [Fact]
    public void AFullyPopulatedRequestIsValid()
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(
            Title: "Halbmarathon im Mai",
            Description: "Drei Läufe pro Woche.",
            Icon: "medal",
            Schedule: new GoalScheduleRequest(Kind: ScheduleKind.Times, Times: 3, Period: QuotaPeriod.Week),
            IsGroup: false,
            ReminderAt: new TimeOnly(18, 0),
            TargetDate: new DateOnly(2026, 12, 24),
            ParticipantIds: [Guid.CreateVersion7()]));

        Assert.Empty(errors);
    }

    [Fact]
    public void AnEmptyRequestReportsOnlyTheTitle()
    {
        // Everything else has a sensible default; a goal without a title has
        // nothing to be.
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest());

        Assert.Equal([nameof(CreateGoalRequest.Title)], errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTitleIsReported(string title)
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(Title: title));

        Assert.Contains(nameof(CreateGoalRequest.Title), errors.Keys);
    }

    [Fact]
    public void ATitleAtTheLimitIsAccepted()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: new string('a', Goal.MaxTitleLength)));

        Assert.Empty(errors);
    }

    public static TheoryData<GoalScheduleRequest> BrokenSchedules =>
    [
        new GoalScheduleRequest(Kind: ScheduleKind.Interval, EveryDays: 0),
        new GoalScheduleRequest(Kind: ScheduleKind.Interval, EveryDays: GoalSchedule.MaxEveryDays + 1),
        new GoalScheduleRequest(Kind: ScheduleKind.Interval),
        new GoalScheduleRequest(Kind: ScheduleKind.Weekdays),
        new GoalScheduleRequest(Kind: ScheduleKind.Weekdays, Weekdays: []),
        new GoalScheduleRequest(Kind: ScheduleKind.Weekdays, Weekdays: [(Weekday)9]),
        new GoalScheduleRequest(Kind: ScheduleKind.Times, Times: 0, Period: QuotaPeriod.Week),
        new GoalScheduleRequest(Kind: ScheduleKind.Times, Times: 2),
        new GoalScheduleRequest(Kind: (ScheduleKind)42),
    ];

    public static TheoryData<GoalScheduleRequest> GoodSchedules =>
    [
        new GoalScheduleRequest(Kind: ScheduleKind.Once),
        new GoalScheduleRequest(Kind: ScheduleKind.Interval, EveryDays: 1),
        new GoalScheduleRequest(Kind: ScheduleKind.Interval, EveryDays: 3),
        new GoalScheduleRequest(Kind: ScheduleKind.Weekdays, Weekdays: [Weekday.Monday, Weekday.Thursday]),
        new GoalScheduleRequest(Kind: ScheduleKind.Times, Times: 3, Period: QuotaPeriod.Week),
        new GoalScheduleRequest(Kind: ScheduleKind.Times, Times: 2, Period: QuotaPeriod.Month),
    ];

    [Fact]
    public void AnUnknownIconIsReported()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", Icon: "rocket"));

        Assert.Contains(nameof(CreateGoalRequest.Icon), errors.Keys);
    }

    [Fact]
    public void EveryIconTheServerOffersPassesItsOwnValidator()
    {
        foreach (var icon in GoalIcons.All)
        {
            var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(Title: "Anything", Icon: icon));

            Assert.Empty(errors);
        }
    }

    [Fact]
    public void ASayingNothingAboutTheScheduleIsAllowedAndMeansEveryDay()
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(Title: "Anything"));

        Assert.Empty(errors);

        var schedule = CreateGoalRequestValidator.ToSchedule(null);
        Assert.Equal(ScheduleKind.Interval, schedule.Kind);
        Assert.Equal(1, schedule.EveryDays);
    }

    [Theory]
    [MemberData(nameof(BrokenSchedules))]
    public void AScheduleThatCannotMeanAnythingIsReported(GoalScheduleRequest schedule)
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", Schedule: schedule));

        Assert.Contains(nameof(CreateGoalRequest.Schedule), errors.Keys);
    }

    [Theory]
    [MemberData(nameof(GoodSchedules))]
    public void AScheduleTheServerWouldAcceptPassesItsOwnValidator(GoalScheduleRequest schedule)
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", Schedule: schedule));

        Assert.Empty(errors);

        // And converts without throwing, which is the half a field message
        // cannot cover.
        Assert.NotNull(CreateGoalRequestValidator.ToSchedule(schedule));
    }

    [Fact]
    public void AnEmptyParticipantIdIsReported()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", ParticipantIds: [Guid.Empty]));

        Assert.Contains(nameof(CreateGoalRequest.ParticipantIds), errors.Keys);
    }
}
