using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.People;

/// <summary>
/// What a person is allowed to be, and how their streak is read back.
/// </summary>
public class PersonTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 17);

    private static Person Build() =>
        Person.Create(Guid.CreateVersion7(), "Mara Klein", "@mara.k", "MK", AvatarColors.Green);

    [Fact]
    public void ADisplayNameIsRequired()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Person.Create(Guid.CreateVersion7(), "  ", "@mara.k", "MK", AvatarColors.Green));

        Assert.Contains(nameof(Person.DisplayName), error.Errors.Keys);
    }

    [Fact]
    public void AHandleMustStartWithAnAtSign()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Person.Create(Guid.CreateVersion7(), "Mara Klein", "mara.k", "MK", AvatarColors.Green));

        Assert.Contains(nameof(Person.Handle), error.Errors.Keys);
    }

    [Fact]
    public void AnAvatarColourOutsideThePaletteIsRejected()
    {
        // The palette exists because every colour in it has been checked to
        // carry white text; an arbitrary one could not promise that.
        var error = Assert.Throws<DomainValidationException>(
            () => Person.Create(Guid.CreateVersion7(), "Mara Klein", "@mara.k", "MK", "#ffff00"));

        Assert.Contains(nameof(Person.AvatarColor), error.Errors.Keys);
    }

    [Fact]
    public void CheckingInTwiceOnOneDayIsOneDay()
    {
        var person = Build();

        person.CheckIn(Guid.CreateVersion7(), Today);
        person.CheckIn(Guid.CreateVersion7(), Today);

        Assert.Single(person.CheckIns);
        Assert.Equal(1, person.StreakOn(Today));
    }

    [Fact]
    public void TheStreakIsReadBackFromTheDaysThemselves()
    {
        var person = Build();

        for (var offset = 0; offset < 5; offset++)
        {
            person.CheckIn(Guid.CreateVersion7(), Today.AddDays(-offset));
        }

        Assert.Equal(5, person.StreakOn(Today));
    }

    [Fact]
    public void TheWeekShowsWhichDaysWereUsed()
    {
        var person = Build();
        person.CheckIn(Guid.CreateVersion7(), Today);

        Assert.Equal(7, person.WeekActivity(Today).Count);
        Assert.Equal(1, person.WeekActivity(Today).Count(day => day));
    }

    [Fact]
    public void SomebodySeenAMinuteAgoIsOnline()
    {
        var person = Build();
        person.SetLastSeen(Now.AddMinutes(-1));

        Assert.True(person.IsOnlineAt(Now));
    }

    [Fact]
    public void SomebodySeenAnHourAgoIsNot()
    {
        var person = Build();
        person.SetLastSeen(Now.AddHours(-1));

        Assert.False(person.IsOnlineAt(Now));
    }

    [Fact]
    public void SomebodyNeverSeenIsNotOnline()
    {
        Assert.False(Build().IsOnlineAt(Now));
    }

    [Fact]
    public void KudosCanBeGivenAndTakenBackButNeverBelowZero()
    {
        var person = Build();

        person.ReceiveKudos();
        person.WithdrawKudos();
        person.WithdrawKudos();

        Assert.Equal(0, person.KudosReceived);
    }

    [Fact]
    public void ABadgeIsAwardedOnce()
    {
        var person = Build();

        person.AwardBadge(Guid.CreateVersion7(), BadgeKey.StreakHero, Today);
        person.AwardBadge(Guid.CreateVersion7(), BadgeKey.StreakHero, Today);

        Assert.Single(person.Badges);
    }
}
