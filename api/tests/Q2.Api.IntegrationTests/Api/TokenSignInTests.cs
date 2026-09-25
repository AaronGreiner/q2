using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Accounts;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Signing in with bearer tokens — the iOS app's way — and what keeps a token
/// from outliving the password or the account it was issued for.
/// </summary>
/// <remarks>
/// The same real Identity stack as <see cref="AccountEndpointTests"/>; only the
/// credential the client keeps differs
/// (docs/adr/0034-bearer-tokens-for-the-native-app.md).
/// </remarks>
[Trait("Category", "Integration")]
public class TokenSignInTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record TokenDocument(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);

    private sealed record SessionDocument(string Email);

    private sealed record ProblemDocument(string? Reason);

    [Fact]
    public async Task SigningInForTokensSetsNoCookieAndTheTokenIsASession()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/token", new
        {
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = SeedAccounts.Password,
        });

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Set-Cookie"));

        var tokens = await response.ReadAsync<TokenDocument>();
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal((long)AccountPolicy.AccessTokenLifetime.TotalSeconds, tokens.ExpiresIn);
        Assert.NotEqual(tokens.AccessToken, tokens.RefreshToken);

        var session = await GetSessionAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(AutomatedTestSeed.CurrentPersonEmail, (await session.ReadAsync<SessionDocument>()).Email);
    }

    [Fact]
    public async Task AWrongPasswordGetsTheSameAnswerAsTheCookieSignIn()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/token", new
        {
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = "nicht-das-passwort",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AuthenticationFailures.InvalidCredentials, (await response.ReadAsync<ProblemDocument>()).Reason);
    }

    [Fact]
    public async Task AnAccessTokenStopsWorkingOnceItsLifetimeIsOver()
    {
        var tokens = await SignInForTokensAsync();

        MoveClock(AccountPolicy.AccessTokenLifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetSessionAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task ARefreshHandsOutANewPairThatWorks()
    {
        var first = await SignInForTokensAsync();

        // Past the access token's lifetime, which is when the app refreshes.
        MoveClock(AccountPolicy.AccessTokenLifetime + TimeSpan.FromSeconds(1));

        var refreshed = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var second = await refreshed.ReadAsync<TokenDocument>();
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await GetSessionAsync(second.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task ARefreshTokenStopsWorkingAfterAFortnightUnused()
    {
        var tokens = await SignInForTokensAsync();

        MoveClock(AccountPolicy.RefreshTokenLifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    /// <summary>
    /// What a password reset does to every other device: it changes the
    /// security stamp, and a refresh compares the stamp.
    /// </summary>
    [Fact]
    public async Task ARefreshIsRefusedOnceTheSecurityStampHasChanged()
    {
        var tokens = await SignInForTokensAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var account = await users.FindByEmailAsync(AutomatedTestSeed.CurrentPersonEmail);
            Assert.NotNull(account);
            await users.UpdateSecurityStampAsync(account);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task ARefreshIsRefusedOnceTheAccountIsDeleted()
    {
        var tokens = await SignInForTokensAsync();

        using var deletion = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { password = SeedAccounts.Password }, options: TestJson.Options),
        };
        deletion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var deleted = await AnonymousClient.SendAsync(deletion, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    [Theory]
    [InlineData("kein-token")]
    [InlineData("")]
    public async Task SomethingThatIsNotARefreshTokenIsRefused(string refreshToken)
    {
        var response = await RefreshAsync(refreshToken);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"Expected 401 or 400, got {(int)response.StatusCode}.");
    }

    /// <summary>
    /// An access token is not a refresh token: the two are sealed for different
    /// purposes, so one cannot be passed off as the other.
    /// </summary>
    [Fact]
    public async Task AnAccessTokenCannotBeUsedToRefresh()
    {
        var tokens = await SignInForTokensAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.AccessToken)).StatusCode);
    }

    /// <summary>
    /// Only the live connection reads a token from the address. Anywhere else,
    /// a token in a link must not sign anybody in.
    /// </summary>
    [Fact]
    public async Task ATokenInTheQueryIsIgnoredOutsideTheLiveConnection()
    {
        var tokens = await SignInForTokensAsync();

        var response = await AnonymousClient.GetAsync(
            $"/api/auth/session?access_token={Uri.EscapeDataString(tokens.AccessToken)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A request carrying a token is judged by the token alone — a cookie the
    /// same client happens to hold cannot rescue an expired one.
    /// </summary>
    [Fact]
    public async Task ARequestWithATokenIsNotRescuedByACookie()
    {
        var tokens = await SignInForTokensAsync();
        MoveClock(AccountPolicy.AccessTokenLifetime + TimeSpan.FromSeconds(1));

        // Client is signed in with a cookie by ApiTestBase.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<TokenDocument> SignInForTokensAsync()
    {
        var response = await AnonymousClient.PostJsonAsync("/api/auth/token", new
        {
            email = AutomatedTestSeed.CurrentPersonEmail,
            password = SeedAccounts.Password,
        });

        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<TokenDocument>();
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        AnonymousClient.PostJsonAsync("/api/auth/token/refresh", new { refreshToken });

    private async Task<HttpResponseMessage> GetSessionAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await AnonymousClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <remarks>
    /// Put back by <see cref="Q2ApiFactory.ResetAsync"/> before the next test,
    /// like every other test that moves the shared clock.
    /// </remarks>
    private void MoveClock(TimeSpan by) =>
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(Q2ApiFactory.Now + by);
}
