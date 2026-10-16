using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.People;

/// <summary>
/// The first-run introduction on the person: once, and the one moment a
/// handle can be chosen (docs/adr/0039-first-run-onboarding.md).
/// </summary>
public class OnboardingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static Person Build() =>
        Person.Create(Guid.CreateVersion7(), "Nora Neu", "@nora.neu", "NN", AvatarColors.Teal);

    [Fact]
    public void ANewPersonHasTheIntroductionAheadOfThem()
    {
        Assert.False(Build().IsOnboarded);
    }

    [Fact]
    public void FinishingItTwiceKeepsTheFirstMoment()
    {
        var person = Build();

        person.CompleteOnboarding(Now);
        person.CompleteOnboarding(Now.AddDays(3));

        Assert.True(person.IsOnboarded);
        Assert.Equal(Now, person.OnboardedAt);
    }

    [Fact]
    public void AHandleCanBeChosenWhileTheIntroductionIsOpen()
    {
        var person = Build();

        person.ChooseHandle("@nora");

        Assert.Equal("@nora", person.Handle);
    }

    [Fact]
    public void AfterwardsTheHandleIsFixed()
    {
        // It is what friends searched for and wrote down; the introduction is
        // the one moment before that matters (#108).
        var person = Build();
        person.CompleteOnboarding(Now);

        var error = Assert.Throws<DomainValidationException>(() => person.ChooseHandle("@nora"));

        Assert.Contains(nameof(Person.Handle), error.Errors.Keys);
        Assert.Equal("@nora.neu", person.Handle);
    }

    [Fact]
    public void AHandleThatBreaksTheRulesIsRefused()
    {
        var person = Build();

        Assert.Throws<DomainValidationException>(() => person.ChooseHandle("@n"));
        Assert.Equal("@nora.neu", person.Handle);
    }

    [Theory]
    [InlineData("@nora")]
    [InlineData("@nora.neu")]
    [InlineData("@nora_n")]
    [InlineData("@nora-2")]
    [InlineData("@n0r")]
    [InlineData("@123")]
    public void TheseHandlesMayBeChosen(string handle)
    {
        Assert.True(Handles.IsChoosable(handle));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nora")]
    [InlineData("@no")]
    [InlineData("@.nora")]
    [InlineData("@nora.")]
    [InlineData("@no..ra")]
    [InlineData("@no-_ra")]
    [InlineData("@Nora")]
    [InlineData("@nora neu")]
    [InlineData("@nöra")]
    [InlineData("@nora!")]
    public void TheseAreRefused(string? handle)
    {
        Assert.False(Handles.IsChoosable(handle));
    }

    [Fact]
    public void ALongestHandleStillFitsTheColumn()
    {
        var longest = "@" + new string('a', Handles.MaxBodyLength);

        Assert.True(Handles.IsChoosable(longest));
        Assert.Equal(Person.MaxHandleLength, longest.Length);
        Assert.False(Handles.IsChoosable(longest + "a"));
    }

    [Theory]
    [InlineData("nora", "@nora")]
    [InlineData("  @Nora.Neu ", "@nora.neu")]
    [InlineData("@@nora", "@nora")]
    [InlineData("   ", null)]
    [InlineData("@", null)]
    [InlineData(null, null)]
    public void WhatWasTypedIsReadAsAHandle(string? typed, string? expected)
    {
        Assert.Equal(expected, Handles.Normalise(typed));
    }
}
