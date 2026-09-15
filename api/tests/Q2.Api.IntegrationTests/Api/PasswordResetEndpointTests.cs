using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Mail;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Getting back into an account: asking for a link, and using it.
/// </summary>
/// <remarks>
/// Against the real Identity stack — a real token, a real security stamp, and a
/// real session cookie that stops being accepted. Only the mail is recorded
/// rather than sent (<see cref="RecordingMailTransport"/>).
/// </remarks>
[Trait("Category", "Integration")]
public partial class PasswordResetEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ProblemDocument(string? Title, string? Reason, Dictionary<string, string[]>? Errors);

    private sealed record ResetDocument(string Email);

    /// <summary>Long, like the policy asks, and not the seeded password.</summary>
    private const string NewPassword = "ein-ganz-neues-passwort";

    private const string Email = AutomatedTestSeed.CurrentPersonEmail;

    [GeneratedRegex(@"(?<link>\S+/reset-password#token=(?<token>[A-Za-z0-9._-]+))")]
    private static partial Regex LinkPattern { get; }

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    [Fact]
    public async Task AskingForALinkMailsTheAccountOnce()
    {
        var response = await AskForALinkAsync(Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var mail = Assert.Single(Factory.Mail.Sent);
        Assert.Equal(Email, mail.To);
        Assert.StartsWith("http://localhost:3000/reset-password#token=", LinkIn(mail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLinkPointsAtTheConfiguredAppWhateverHostTheRequestNamed()
    {
        // The classic way to steal a reset: make the server write a link to a
        // domain the attacker owns, and let the victim click it.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password/forgot")
        {
            Content = JsonContent.Create(new { email = Email }, options: TestJson.Options),
        };
        request.Headers.Host = "attacker.example";

        var response = await AnonymousClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.StartsWith("http://localhost:3000/", LinkIn(Assert.Single(Factory.Mail.Sent)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAddressWithNoAccountGetsTheSameAnswerAndNoMail()
    {
        var known = await AskForALinkAsync(Email);
        var unknown = await AskForALinkAsync("nobody@kudos.example");

        // A different answer — or any body at all — would make this a way to
        // find out who has an account here.
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await BodyOf(known), await BodyOf(unknown));

        Assert.DoesNotContain(Factory.Mail.Sent, mail => mail.To == "nobody@kudos.example");
    }

    [Fact]
    public async Task TheLinkSetsANewPasswordAndTheOldOneStopsWorking()
    {
        var token = await TokenForAsync(Email);

        var reset = await ResetAsync(token);

        reset.EnsureSuccessStatusCode();
        Assert.Equal(Email, (await reset.ReadAsync<ResetDocument>()).Email);

        using var withTheOldPassword = Factory.CreateClient().AcceptingJson();
        var refused = await withTheOldPassword.PostJsonAsync("/api/auth/login", new { email = Email, password = SeedAccounts.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // Throws if the new password is refused.
        using var withTheNewPassword = Factory.CreateClient().AcceptingJson();
        await withTheNewPassword.AsAsync(Email, NewPassword);
    }

    [Fact]
    public async Task ALinkWorksOnlyOnce()
    {
        var token = await TokenForAsync(Email);
        (await ResetAsync(token)).EnsureSuccessStatusCode();

        var again = await ResetAsync(token, "noch-ein-anderes-passwort");

        await AssertRefusedLinkAsync(again);
    }

    [Fact]
    public void ALinkIsIssuedForAnHour()
    {
        // Read from the running host, because Identity's token provider checks
        // expiry against the system clock rather than the injected one: moving
        // the test clock cannot age a link, so what is proven is that the hour
        // reaches the provider. Identity's own expiry check does the rest, and
        // refuses an expired link with the same answer a used one gets.
        var options = Factory.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;

        Assert.Equal(AccountPolicy.PasswordResetLinkLifetime, options.TokenLifespan);
        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("aaaaaaaa000040008000000000000001.Q2ZESjg")]
    public async Task ALinkThatIsNotOneOfOursIsRefusedTheSameWay(string token)
    {
        await AssertRefusedLinkAsync(await ResetAsync(token));
    }

    [Fact]
    public async Task AnAlteredLinkIsRefusedAndTheRealOneStillWorks()
    {
        var token = await TokenForAsync(Email);

        // One character in the middle of Identity's part: not a padding bit,
        // a changed byte.
        var middle = token.IndexOf('.', StringComparison.Ordinal) + 20;
        var altered = string.Concat(token.AsSpan(0, middle), token[middle] == 'A' ? "B" : "A", token.AsSpan(middle + 1));

        await AssertRefusedLinkAsync(await ResetAsync(altered));

        // A refused attempt does not spend the link it was an attempt at.
        (await ResetAsync(token)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ALinkForOneAccountDoesNotOpenAnother()
    {
        var token = await TokenForAsync(Email);

        var friendsAccount = Guid.Empty;
        await Factory.WithDatabaseAsync(async database =>
            friendsAccount = await database.Users
                .Where(user => user.Email == AutomatedTestSeed.FriendEmail)
                .Select(user => user.Id)
                .SingleAsync(TestContext.Current.CancellationToken));

        // Identity's part is bound to the account it was issued for, whatever
        // the link says in front of it.
        var redirected = $"{friendsAccount:N}{token[token.IndexOf('.', StringComparison.Ordinal)..]}";

        await AssertRefusedLinkAsync(await ResetAsync(redirected));
    }

    [Fact]
    public async Task ResettingEndsEverySessionOfTheAccount()
    {
        // Client has been signed in as this person since the test began, and
        // a session two minutes old is checked against the account again.
        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.OK, (await SessionAsync(Client)).StatusCode);

        (await ResetAsync(await TokenForAsync(Email))).EnsureSuccessStatusCode();

        // Within AccountPolicy.SessionRecheckInterval the session opened with
        // the old password is refused, rather than half an hour later.
        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(4));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SessionAsync(Client)).StatusCode);
    }

    [Fact]
    public async Task ASecondRequestWithinTheCooldownSendsNoSecondMail()
    {
        await AskForALinkAsync(Email);
        var second = await AskForALinkAsync(Email);

        // The same answer: the cooldown is not something the requester learns about.
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Single(Factory.Mail.Sent);

        Clock.Set(Q2ApiFactory.Now + AccountPolicy.PasswordResetMailCooldown);
        await AskForALinkAsync(Email);

        Assert.Equal(2, Factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task TheMailIsGermanForAnAccountThatNeverChose()
    {
        await AskForALinkAsync(Email);

        Assert.Equal("Dein Qdos-Passwort zurücksetzen", Assert.Single(Factory.Mail.Sent).Subject);
    }

    [Fact]
    public async Task TheMailIsWrittenInTheLanguageTheAccountChose()
    {
        await Factory.WithDatabaseAsync(async database =>
        {
            var preferences = await database.UserSettings.SingleAsync(
                settings => settings.PersonId == AutomatedTestSeed.CurrentPersonId,
                TestContext.Current.CancellationToken);

            preferences.Update(preferences.Theme, LanguagePreference.English, preferences.Notifications);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await AskForALinkAsync(Email);

        Assert.Equal("Reset your Qdos password", Assert.Single(Factory.Mail.Sent).Subject);
    }

    [Fact]
    public async Task WithoutMailTheAnswerSaysSoForEveryAddress()
    {
        Factory.Mail.IsConfigured = false;

        var known = await AskForALinkAsync(Email);
        var unknown = await AskForALinkAsync("nobody@kudos.example");

        // A fact about this deployment, not about anybody's account.
        Assert.Equal(HttpStatusCode.Forbidden, known.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
        Assert.Equal(PasswordResetFailures.MailUnavailable, (await known.ReadAsync<ProblemDocument>()).Reason);
        Assert.Empty(Factory.Mail.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task SomethingThatIsNotAnAddressIsAFieldError(string email)
    {
        var response = await AskForALinkAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.Mail.Sent);
    }

    [Fact]
    public async Task APasswordThatBreaksThePolicyIsRefusedWithoutSpendingTheLink()
    {
        var token = await TokenForAsync(Email);

        var tooShort = await ResetAsync(token, "zu-kurz");

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var problem = await tooShort.ReadAsync<ProblemDocument>();
        Assert.Contains(problem.Errors!.Keys, key => key.Equals("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problem.Errors!.Keys, key => key.Equals("Token", StringComparison.OrdinalIgnoreCase));

        (await ResetAsync(token)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AResetLiftsALockout()
    {
        // Seeded accounts are exempt from lockout, so this one registers.
        const string email = "gesperrt@kudos.example";
        const string password = "ein-langes-passwort";

        (await AnonymousClient.PostJsonAsync("/api/auth/register", new { name = "Gesperrt", email, password }))
            .EnsureSuccessStatusCode();

        using var guesser = Factory.CreateClient().AcceptingJson();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await guesser.PostJsonAsync("/api/auth/login", new { email, password = $"falsch-geraten-{attempt}" });
        }

        var locked = await guesser.PostJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(AuthenticationFailures.LockedOut, (await locked.ReadAsync<ProblemDocument>()).Reason);

        (await ResetAsync(await TokenForAsync(email))).EnsureSuccessStatusCode();

        // Throws if the account is still locked.
        using var owner = Factory.CreateClient().AcceptingJson();
        await owner.AsAsync(email, NewPassword);
    }

    private Task<HttpResponseMessage> AskForALinkAsync(string email) =>
        AnonymousClient.PostJsonAsync("/api/auth/password/forgot", new { email });

    private Task<HttpResponseMessage> ResetAsync(string token, string password = NewPassword) =>
        AnonymousClient.PostJsonAsync("/api/auth/password/reset", new { token, password });

    private static Task<HttpResponseMessage> SessionAsync(HttpClient client) =>
        client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

    private async Task<string> TokenForAsync(string email)
    {
        Assert.Equal(HttpStatusCode.Accepted, (await AskForALinkAsync(email)).StatusCode);

        var match = LinkPattern.Match(Assert.Single(Factory.Mail.Sent, mail => mail.To == email).Body);
        Assert.True(match.Success, "The mail carries no reset link.");

        return match.Groups["token"].Value;
    }

    private static string LinkIn(OutgoingMail mail)
    {
        var match = LinkPattern.Match(mail.Body);
        Assert.True(match.Success, "The mail carries no reset link.");

        return match.Groups["link"].Value;
    }

    private static Task<string> BodyOf(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Refused as a link, under the token and with the one sentence every
    /// refused link gets — expired, used, altered or never ours alike.
    /// </summary>
    private static async Task AssertRefusedLinkAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ProblemDocument>();
        var token = Assert.Single(problem.Errors!, entry => entry.Key.Equals("Token", StringComparison.OrdinalIgnoreCase));

        Assert.Equal([AccountRequestValidator.InvalidResetLink], token.Value);
    }
}
