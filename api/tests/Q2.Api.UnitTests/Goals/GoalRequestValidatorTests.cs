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
            Rhythm: GoalRhythm.Weekly,
            IsGroup: false,
            TotalSteps: 21,
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

    [Theory]
    [InlineData(0)]
    [InlineData(Goal.MaxSteps + 1)]
    public void AnImpossibleNumberOfStepsIsReported(int steps)
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", TotalSteps: steps));

        Assert.Contains(nameof(CreateGoalRequest.TotalSteps), errors.Keys);
    }

    [Fact]
    public void AnEmptyParticipantIdIsReported()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Anything", ParticipantIds: [Guid.Empty]));

        Assert.Contains(nameof(CreateGoalRequest.ParticipantIds), errors.Keys);
    }
}

public class CreateTaskRequestValidatorTests
{
    [Fact]
    public void ATitleIsRequired()
    {
        var errors = CreateTaskRequestValidator.Validate(new CreateTaskRequest());

        Assert.Contains(nameof(CreateTaskRequest.Title), errors.Keys);
    }

    [Fact]
    public void AUnitWithoutATargetIsReported()
    {
        // "· L" under a task would measure nothing.
        var errors = CreateTaskRequestValidator.Validate(
            new CreateTaskRequest(Title: "Wasser trinken", MeasureUnit: "L"));

        Assert.Contains(nameof(CreateTaskRequest.TargetValue), errors.Keys);
    }

    [Fact]
    public void ATargetOfZeroIsReported()
    {
        var errors = CreateTaskRequestValidator.Validate(
            new CreateTaskRequest(Title: "Wasser trinken", TargetValue: 0, MeasureUnit: "L"));

        Assert.Contains(nameof(CreateTaskRequest.TargetValue), errors.Keys);
    }

    [Fact]
    public void AMeasurableTaskWithBothPartsIsValid()
    {
        var errors = CreateTaskRequestValidator.Validate(
            new CreateTaskRequest(Title: "Wasser trinken", TargetValue: 2, MeasureUnit: "L"));

        Assert.Empty(errors);
    }
}
