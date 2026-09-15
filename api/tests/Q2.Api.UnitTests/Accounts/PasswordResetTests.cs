using Q2.Api.Features.Accounts;
using Q2.Api.Features.Settings;

namespace Q2.Api.UnitTests.Accounts;

/// <summary>What a reset link carries, and that it reads back as exactly that.</summary>
public class PasswordResetTokenTests
{
    private static readonly Guid AccountId = new("aaaaaaaa-0000-4000-8000-000000000042");

    /// <summary>Base64 with every character a URL treats specially.</summary>
    private const string IdentityToken = "CfDJ8+/abc/def+ghi==";

    [Fact]
    public void ATokenReadsBackAsTheAccountAndIdentitysToken()
    {
        var encoded = PasswordResetToken.Encode(AccountId, IdentityToken);

        Assert.True(PasswordResetToken.TryDecode(encoded, out var accountId, out var identityToken));
        Assert.Equal(AccountId, accountId);
        Assert.Equal(IdentityToken, identityToken);
    }

    [Fact]
    public void ATokenIsMadeOfCharactersAUrlLeavesAlone()
    {
        // Nothing a fragment parser, an address bar or a mail client that wraps
        // long lines would rewrite on the way.
        Assert.Matches("^[A-Za-z0-9._-]+$", PasswordResetToken.Encode(AccountId, IdentityToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separator-at-all")]
    [InlineData(".Q2ZESjg")]
    [InlineData("aaaaaaaa000040008000000000000042.")]
    [InlineData("not-a-guid.Q2ZESjg")]
    [InlineData("aaaaaaaa000040008000000000000042.!!!")]
    public void AnythingElseIsRefusedWithoutSayingWhy(string? value)
    {
        Assert.False(PasswordResetToken.TryDecode(value, out var accountId, out var identityToken));
        Assert.Equal(Guid.Empty, accountId);
        Assert.Equal(string.Empty, identityToken);
    }

    [Theory]
    [InlineData("https://q2.example.com")]
    [InlineData("https://q2.example.com/")]
    public void TheLinkCarriesTheTokenInTheFragment(string publicAppUrl)
    {
        // In the fragment, because a browser never sends a fragment anywhere:
        // not to the proxy, not to the server rendering the page.
        Assert.Equal(
            "https://q2.example.com/reset-password#token=abc.def",
            PasswordResetToken.Link(new Uri(publicAppUrl), "abc.def"));
    }
}

/// <summary>The words of a reset mail.</summary>
public class PasswordResetMailTests
{
    private const string Link = "https://q2.example.com/reset-password#token=abc.def";

    private const string Address = "mara.k@kudos.example";

    [Fact]
    public void AMailIsGermanUnlessTheAccountChoseEnglish()
    {
        var mail = PasswordResetMail.For(Address, LanguagePreference.German, Link);

        Assert.Equal(Address, mail.To);
        Assert.Equal("Dein Qdos-Passwort zurücksetzen", mail.Subject);
        Assert.Contains("Mit diesem Link", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAccountThatChoseEnglishGetsAnEnglishMail()
    {
        var mail = PasswordResetMail.For(Address, LanguagePreference.English, Link);

        Assert.Equal("Reset your Qdos password", mail.Subject);
        Assert.Contains("This link lets you set a new one", mail.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LanguagePreference.German)]
    [InlineData(LanguagePreference.English)]
    public void TheLinkStandsOnALineOfItsOwn(LanguagePreference language)
    {
        // So every mail client turns all of it into one link, rather than
        // stopping at a word that happens to follow it.
        var lines = PasswordResetMail.For(Address, language, Link).Body.Split('\n').Select(line => line.Trim());

        Assert.Contains(Link, lines);
    }

    [Theory]
    [InlineData(LanguagePreference.German)]
    [InlineData(LanguagePreference.English)]
    public void AMailNamesNobody(LanguagePreference language)
    {
        // It passes through a provider, and the address it is sent to is all it
        // needs to carry.
        Assert.DoesNotContain(Address, PasswordResetMail.For(Address, language, Link).Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLifetimeTheMailPromisesIsTheOneThatIsEnforced()
    {
        // Both texts say "one hour". This is what notices the policy moving
        // without them.
        Assert.Equal(TimeSpan.FromHours(1), AccountPolicy.PasswordResetLinkLifetime);

        Assert.Contains("eine Stunde", PasswordResetMail.For(Address, LanguagePreference.German, Link).Body, StringComparison.Ordinal);
        Assert.Contains("one hour", PasswordResetMail.For(Address, LanguagePreference.English, Link).Body, StringComparison.Ordinal);
    }
}

/// <summary>At most one reset mail per account in a short while.</summary>
public class PasswordResetCooldownTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Mara = new("aaaaaaaa-0000-4000-8000-000000000001");

    private static readonly Guid Jonas = new("aaaaaaaa-0000-4000-8000-000000000002");

    [Fact]
    public void TheFirstMailIsAlwaysAllowed()
    {
        Assert.True(new PasswordResetCooldown().TryClaim(Mara, Now));
    }

    [Fact]
    public void ASecondMailWithinTheCooldownIsNot()
    {
        var cooldown = new PasswordResetCooldown();
        cooldown.TryClaim(Mara, Now);

        Assert.False(cooldown.TryClaim(Mara, Now + AccountPolicy.PasswordResetMailCooldown - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void OnceTheCooldownHasPassedTheNextMailIsAllowed()
    {
        var cooldown = new PasswordResetCooldown();
        cooldown.TryClaim(Mara, Now);

        Assert.True(cooldown.TryClaim(Mara, Now + AccountPolicy.PasswordResetMailCooldown));
    }

    [Fact]
    public void EachAccountHasItsOwnCooldown()
    {
        var cooldown = new PasswordResetCooldown();
        cooldown.TryClaim(Mara, Now);

        Assert.True(cooldown.TryClaim(Jonas, Now));
    }

    [Fact]
    public void AMailThatNeverLeftGivesItsClaimBack()
    {
        var cooldown = new PasswordResetCooldown();
        cooldown.TryClaim(Mara, Now);

        cooldown.Release(Mara);

        Assert.True(cooldown.TryClaim(Mara, Now));
    }
}
