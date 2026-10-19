using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Accounts;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Changing the password while signed in (#40).
/// </summary>
/// <remarks>
/// Against the real Identity stack, like the reset: a real security stamp, a
/// real lockout counter, and real session cookies that stop being accepted.
/// Only the notice mail is recorded rather than sent.
/// </remarks>
[Trait("Category", "Integration")]
public class PasswordChangeEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ChangedDocument(string Email);

    /// <summary>Long, like the policy asks, and not the seeded password.</summary>
    private const string NewPassword = "ein-ganz-neues-passwort";

    private const string Email = AutomatedTestSeed.CurrentPersonEmail;

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    [Fact]
    public async Task TheNewPasswordWorksAndTheOldOneStops()
    {
        var changed = await ChangeAsync(Client, SeedAccounts.Password, NewPassword);

        changed.EnsureSuccessStatusCode();
        Assert.Equal(Email, (await changed.ReadAsync<ChangedDocument>()).Email);

        using var withTheOldPassword = Factory.CreateClient().AcceptingJson();
        var refused = await withTheOldPassword.PostJsonAsync("/api/auth/login", new { email = Email, password = SeedAccounts.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // Throws if the new password is refused.
        using var withTheNewPassword = Factory.CreateClient().AcceptingJson();
        await withTheNewPassword.AsAsync(Email, NewPassword);
    }

    /// <summary>
    /// The person who proved the password stays signed in; everybody else
    /// holding a session of the account is out within the recheck interval —
    /// the same rule a reset follows (docs/adr/0026-mail-and-password-reset.md).
    /// </summary>
    [Fact]
    public async Task ThisSessionStaysAndEveryOtherEnds()
    {
        var elsewhere = await ClientForAsync(Email);

        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.OK, (await SessionAsync(elsewhere)).StatusCode);

        var changed = await ChangeAsync(Client, SeedAccounts.Password, NewPassword);
        changed.EnsureSuccessStatusCode();

        // The test clients send a fixed Cookie header rather than keeping a
        // jar (SignIn.AsAsync), so this one takes the renewed cookie the way a
        // browser would.
        AdoptSessionCookie(Client, changed);

        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(4));
        Assert.Equal(HttpStatusCode.OK, (await SessionAsync(Client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SessionAsync(elsewhere)).StatusCode);
    }

    /// <summary>
    /// A wrong current password is an answer about a field, not about the
    /// session: a field error the form words, and nothing changes.
    /// </summary>
    [Fact]
    public async Task AWrongCurrentPasswordIsAFieldErrorAndChangesNothing()
    {
        var refused = await ChangeAsync(Client, "nicht-das-passwort", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(FieldErrorCodes.WrongPassword, (await refused.ReadAsync<CodedProblemDocument>()).CodeOf("CurrentPassword").Code);

        // Throws if the old password stopped working.
        using var withTheOldPassword = Factory.CreateClient().AcceptingJson();
        await withTheOldPassword.AsAsync(Email, SeedAccounts.Password);
        Assert.Empty(Factory.Mail.Sent);
    }

    [Fact]
    public async Task TheNewPasswordFollowsTheSamePolicyAsEveryOther()
    {
        var empty = await (await ChangeAsync(Client, "", "")).ReadAsync<CodedProblemDocument>();

        Assert.Equal(FieldErrorCodes.Required, empty.CodeOf("CurrentPassword").Code);
        Assert.Equal(FieldErrorCodes.Required, empty.CodeOf("NewPassword").Code);

        var tooShort = await (await ChangeAsync(Client, SeedAccounts.Password, "kurz")).ReadAsync<CodedProblemDocument>();

        Assert.Equal(FieldErrorCodes.TooShort, tooShort.CodeOf("NewPassword").Code);
        Assert.Equal(AccountPolicy.MinimumPasswordLength, tooShort.CodeOf("NewPassword").Min);
    }

    /// <summary>
    /// Wrong guesses here count like wrong guesses at the sign-in, or a stolen
    /// session could try passwords without limit.
    /// </summary>
    [Fact]
    public async Task WrongCurrentPasswordsCountTowardsTheLockout()
    {
        // Seeded accounts are exempt from lockout, so this one registers.
        const string email = "wechsel@kudos.example";
        const string password = "ein-langes-passwort";

        (await AnonymousClient.PostJsonAsync("/api/auth/register", new { name = "Wechsel", email, password }))
            .EnsureSuccessStatusCode();

        using var owner = Factory.CreateClient().AcceptingJson();
        await owner.AsAsync(email, password);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await ChangeAsync(owner, $"falsch-geraten-{attempt}", NewPassword);
        }

        var locked = await ChangeAsync(owner, password, NewPassword);
        Assert.Equal(FieldErrorCodes.LockedOut, (await locked.ReadAsync<CodedProblemDocument>()).CodeOf("CurrentPassword").Code);

        using var signingIn = Factory.CreateClient().AcceptingJson();
        var refused = await signingIn.PostJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    /// <summary>
    /// For the person who did not change it: the address on the account hears
    /// about it, in the language the account chose.
    /// </summary>
    [Fact]
    public async Task TheAccountIsToldItsPasswordChanged()
    {
        (await ChangeAsync(Client, SeedAccounts.Password, NewPassword)).EnsureSuccessStatusCode();

        var mail = Assert.Single(Factory.Mail.Sent);
        Assert.Equal(Email, mail.To);
        Assert.Equal("Dein Qdos-Passwort wurde geändert", mail.Subject);
        Assert.DoesNotContain("http", mail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutMailThePasswordStillChanges()
    {
        Factory.Mail.IsConfigured = false;

        (await ChangeAsync(Client, SeedAccounts.Password, NewPassword)).EnsureSuccessStatusCode();

        Assert.Empty(Factory.Mail.Sent);
    }

    [Fact]
    public async Task ChangingAPasswordNeedsASession()
    {
        var refused = await ChangeAsync(AnonymousClient, SeedAccounts.Password, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string currentPassword, string newPassword) =>
        client.PostJsonAsync("/api/auth/password/change", new { currentPassword, newPassword });

    private static void AdoptSessionCookie(HttpClient client, HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
    }

    private static Task<HttpResponseMessage> SessionAsync(HttpClient client) =>
        client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);
}
