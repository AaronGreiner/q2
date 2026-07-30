using Q2.Api.Features.Goals;

namespace Q2.Api.UnitTests.Goals;

[Trait("Category", "Unit")]
public class CreateGoalRequestValidatorTests
{
    [Fact]
    public void AMinimalValidRequestHasNoErrors()
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(Title: "Drink more water"));

        Assert.Empty(errors);
    }

    [Fact]
    public void AFullyPopulatedValidRequestHasNoErrors()
    {
        var request = new CreateGoalRequest(
            Title: "Run a 10k",
            Description: "Together, twice a week.",
            ProgressPercent: 30,
            TargetDate: new DateOnly(2026, 9, 1),
            Participants: ["Robin Sample", "Kim Example"]);

        Assert.Empty(CreateGoalRequestValidator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TitleIsRequired(string? title)
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(Title: title));

        Assert.Contains(nameof(CreateGoalRequest.Title), errors.Keys);
    }

    [Fact]
    public void TitleAboveTheDomainLimitIsRejected()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: new string('a', Goal.MaxTitleLength + 1)));

        Assert.Contains(nameof(CreateGoalRequest.Title), errors.Keys);
    }

    [Fact]
    public void DescriptionAboveTheDomainLimitIsRejected()
    {
        var errors = CreateGoalRequestValidator.Validate(new CreateGoalRequest(
            Title: "Fine",
            Description: new string('a', Goal.MaxDescriptionLength + 1)));

        Assert.Contains(nameof(CreateGoalRequest.Description), errors.Keys);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ProgressOutsideZeroToHundredIsRejected(int progress)
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Fine", ProgressPercent: progress));

        Assert.Contains(nameof(CreateGoalRequest.ProgressPercent), errors.Keys);
    }

    [Fact]
    public void TooManyParticipantsAreRejected()
    {
        var participants = Enumerable.Range(0, Goal.MaxParticipants + 1).Select(i => $"P{i}").ToList();

        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Fine", Participants: participants));

        Assert.Contains(nameof(CreateGoalRequest.Participants), errors.Keys);
    }

    [Fact]
    public void BlankParticipantNamesAreRejected()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "Fine", Participants: ["Robin Sample", "  "]));

        Assert.Contains(nameof(CreateGoalRequest.Participants), errors.Keys);
    }

    [Fact]
    public void EveryInvalidFieldIsReported()
    {
        var errors = CreateGoalRequestValidator.Validate(
            new CreateGoalRequest(Title: "", ProgressPercent: 300));

        Assert.Equal(2, errors.Count);
    }
}
