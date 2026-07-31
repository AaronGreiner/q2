using Microsoft.AspNetCore.Identity;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.UnitTests.Accounts;

/// <summary>
/// The seeded credentials, and the profile a new account is given.
/// </summary>
public class SeedAccountTests
{
    /// <summary>
    /// The one test that would notice a framework change silently locking every
    /// seeded account out.
    /// </summary>
    /// <remarks>
    /// <see cref="SeedAccounts.PasswordHash"/> is a constant because a seed has
    /// to be a pure function — Identity's hasher salts randomly, which would
    /// make the same seed produce different rows on every run. The cost of that
    /// choice is that nothing recomputes the hash, so nothing would notice if
    /// the framework stopped accepting it. This does.
    /// </remarks>
    [Fact]
    public void TheSeededPasswordHashStillVerifies()
    {
        var result = new PasswordHasher<AppUser>()
            .VerifyHashedPassword(new AppUser(), SeedAccounts.PasswordHash, SeedAccounts.Password);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void TheSeededHashDoesNotVerifyAnythingElse()
    {
        var result = new PasswordHasher<AppUser>()
            .VerifyHashedPassword(new AppUser(), SeedAccounts.PasswordHash, "not-the-seed-password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void TheSeededPasswordSatisfiesThePolicyItWouldBeCheckedAgainst()
    {
        // A seeded account that could not have been registered through the app
        // would be a fixture testing something the product does not allow.
        Assert.True(SeedAccounts.Password.Length >= AccountPolicy.MinimumPasswordLength);
    }

    [Fact]
    public void AnAddressIsDerivedFromTheHandle()
    {
        Assert.Equal("mara.k@kudos.example", SeedAccounts.EmailFor("@mara.k"));
    }

    [Fact]
    public void AnAccountPointsAtItsPersonAndCarriesNothingElseAboutThem()
    {
        var person = Person.Create(
            Guid.CreateVersion7(), "Mara Klein", "@mara.k", "MK", AvatarColors.Green);

        var account = SeedAccounts.For(Guid.CreateVersion7(), person);

        Assert.Equal(person.Id, account.PersonId);
        Assert.Equal("mara.k@kudos.example", account.Email);
        Assert.Equal(account.Email, account.UserName);

        // Identity looks accounts up by the normalised value, so a seed that
        // left it unset would insert accounts nobody could sign in to.
        Assert.Equal("MARA.K@KUDOS.EXAMPLE", account.NormalizedEmail);
        Assert.Equal("MARA.K@KUDOS.EXAMPLE", account.NormalizedUserName);
    }
}

/// <summary>
/// The handle, initials and colour a registration derives from a typed name.
/// </summary>
public class ProfileDefaultsTests
{
    [Theory]
    [InlineData("Mara Klein", "mara.klein")]
    [InlineData("  Jonas   Weber  ", "jonas.weber")]
    [InlineData("Anne-Marie Fischer", "anne.marie.fischer")]
    [InlineData("Lena", "lena")]
    public void AHandleIsTheNameMadeIntoSomethingTypeable(string name, string expected)
    {
        Assert.Equal(expected, ProfileDefaults.HandleBody(name));
    }

    [Theory]
    [InlineData("Jürgen Groß", "juergen.gross")]
    [InlineData("Änne Öhler", "aenne.oehler")]
    public void GermanUmlautsBecomeTheLettersAGermanReaderExpects(string name, string expected)
    {
        // Stripping the diacritic alone would give "jrgen.gro", which is not a
        // name anybody would recognise as theirs.
        Assert.Equal(expected, ProfileDefaults.HandleBody(name));
    }

    [Theory]
    [InlineData("Zoë Café", "zoe.cafe")]
    [InlineData("Łukasz Wójcik", "lukasz.wojcik")]
    [InlineData("Anaïs Sørensen", "anais.soerensen")]
    public void OtherDiacriticsAreReducedToTheirBaseLetter(string name, string expected)
    {
        Assert.Equal(expected, ProfileDefaults.HandleBody(name));
    }

    [Fact]
    public void AnAlphabetTheTableDoesNotCoverFallsBackRatherThanProducingNonsense()
    {
        // A real limit, stated rather than hidden: InvariantGlobalization is on
        // for the whole backend, so there is no Unicode normalisation to lean
        // on and the folding table is the whole of what q2 can transliterate.
        Assert.Equal(ProfileDefaults.FallbackHandle, ProfileDefaults.HandleBody("Ирина Петрова"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("🎉🎉")]
    [InlineData("...")]
    public void ANameWithNothingUsableInItFallsBackRatherThanProducingAnEmptyHandle(string name)
    {
        Assert.Equal(ProfileDefaults.FallbackHandle, ProfileDefaults.HandleBody(name));
    }

    [Fact]
    public void AHandleLeavesRoomForTheSuffixADuplicateNeeds()
    {
        var handle = ProfileDefaults.Handle(ProfileDefaults.HandleBody(new string('a', 200)), attempt: 99);

        Assert.True(handle.Length <= Person.MaxHandleLength);
    }

    [Fact]
    public void TheFirstOfANameGetsThePlainHandleAndTheSecondANumber()
    {
        Assert.Equal("@lena.schmidt", ProfileDefaults.Handle("lena.schmidt", attempt: 1));
        Assert.Equal("@lena.schmidt-2", ProfileDefaults.Handle("lena.schmidt", attempt: 2));
    }

    [Theory]
    [InlineData("Mara Klein", "MK")]
    [InlineData("Lena", "L")]
    [InlineData("Anne Marie Fischer", "AM")]
    [InlineData("   ", "?")]
    public void InitialsAreTheFirstLetterOfTheFirstTwoWords(string name, string expected)
    {
        Assert.Equal(expected, ProfileDefaults.Initials(name));
    }

    [Fact]
    public void AColourIsAlwaysOneFromThePalette()
    {
        // The palette is closed because every colour in it carries white text
        // at 4.5:1; a colour picked freely could not promise that.
        Assert.All(
            new[] { "@mara.k", "@jonas.w", "@lena.s", "@a", "@zzzzzzzz" },
            handle => Assert.Contains(ProfileDefaults.AvatarColor(handle), AvatarColors.All));
    }

    [Fact]
    public void TheSameHandleAlwaysGetsTheSameColour()
    {
        // Not string.GetHashCode: .NET randomises string hashing per process,
        // so the same person would change colour on every server restart.
        Assert.Equal(ProfileDefaults.AvatarColor("@mara.k"), ProfileDefaults.AvatarColor("@mara.k"));
    }
}
