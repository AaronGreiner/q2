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

    [Fact]
    public void ABioIsTrimmedAndBlankTakesItAway()
    {
        var person = Build();

        person.SetBio("  Läuft lieber morgens.  ");
        Assert.Equal("Läuft lieber morgens.", person.Bio);

        person.SetBio("   ");
        Assert.Null(person.Bio);
    }

    [Fact]
    public void ABioHasALimit()
    {
        var person = Build();

        person.SetBio(new string('a', Person.MaxBioLength));
        Assert.Throws<DomainValidationException>(() => person.SetBio(new string('a', Person.MaxBioLength + 1)));
        Assert.Equal(Person.MaxBioLength, person.Bio!.Length);
    }

    [Fact]
    public void EverybodyCanFindThemAndSeeTheirActivityUntilTheyChooseOtherwise()
    {
        var person = Build();

        Assert.Equal(Findability.Everyone, person.Findability);
        Assert.True(person.ShowsActivity);
    }

    [Fact]
    public void HiddenActivityIsNeitherOnlineNorLastSeen()
    {
        var person = Build();
        person.SetLastSeen(Now.AddMinutes(-1));

        person.SetPrivacy(Findability.Handle, showsActivity: false);

        Assert.False(person.IsOnlineAt(Now));
        Assert.Null(person.VisibleLastSeenAt);

        // Kept, not forgotten: shown again the moment they say so.
        person.SetPrivacy(Findability.Handle, showsActivity: true);
        Assert.True(person.IsOnlineAt(Now));
        Assert.Equal(Now.AddMinutes(-1), person.VisibleLastSeenAt);
    }

    [Fact]
    public void AFindabilityThatDoesNotExistIsRefused()
    {
        Assert.Throws<DomainValidationException>(() => Build().SetPrivacy((Findability)7, showsActivity: true));
    }
}
