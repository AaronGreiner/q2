using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.Accounts;

/// <summary>HTTP surface for registering, signing in and signing out.</summary>
/// <remarks>
/// The only endpoints in q2 that anonymous callers may reach. Everything else
/// is behind <c>RequireAuthorization</c>, so "did I remember to guard this?" is
/// answered by the group a feature is mapped into rather than route by route.
/// </remarks>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Accounts");

        group.MapPost("/register", Register)
            .AllowAnonymous()
            .WithName("Register")
            .WithSummary("Creates an account and signs it in.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem();

        group.MapPost("/login", Login)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Signs in with an email address and a password.")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        /*
         * The iOS app's sign-in. A WebView on capacitor://localhost cannot keep
         * the cookie the two routes above issue, so it holds a pair of tokens
         * instead (docs/adr/0034-bearer-tokens-for-the-native-app.md). Signing
         * up there is the ordinary register call followed by this one.
         */
        group.MapPost("/token", IssueTokens)
            .AllowAnonymous()
            .WithName("IssueTokens")
            .WithSummary("Signs in with an email address and a password, and answers with bearer tokens instead of a cookie.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/token/refresh", RefreshTokens)
            .AllowAnonymous()
            .WithName("RefreshTokens")
            .WithSummary("Trades a refresh token for a new pair. Refused once the password has changed.")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/logout", Logout)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Ends the session. Succeeds even when there was none.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/account", DeleteAccount)
            .RequireAuthorization()
            .WithName("DeleteAccount")
            .WithSummary("Deletes the signed-in account and everything personal behind it. Asks for the password again.")
            .Produces<AccountDeletionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/session", Session)
            .RequireAuthorization()
            .WithName("GetSession")
            .WithSummary("Returns who is signed in.")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/forgot", RequestPasswordReset)
            .AllowAnonymous()
            .RequireRateLimiting(PasswordResetRateLimit.PolicyName)
            .WithName("RequestPasswordReset")
            .WithSummary("Mails a link to set a new password, if the address has an account. Answers the same either way.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password/reset", ResetPassword)
            .AllowAnonymous()
            .WithName("ResetPassword")
            .WithSummary("Sets a new password with the token from a reset link, and ends every session of the account.")
            .Produces<PasswordResetResponse>()
            .ProducesValidationProblem();

        return endpoints;
    }

    /// <remarks>
    /// 202 for every well-formed address, known or not. What happens next —
    /// looking the account up, the mail — happens after this answer, so the
    /// answer cannot give away which addresses have accounts
    /// (<see cref="IPasswordResetQueue"/>).
    /// </remarks>
    private static async Task<Accepted> RequestPasswordReset(
        PasswordResetService resets,
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await resets.RequestAsync(request, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Ok<PasswordResetResponse>> ResetPassword(
        PasswordResetService resets,
        ResetPasswordRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await resets.ResetAsync(request, cancellationToken));

    private static async Task<Ok<SessionResponse>> Register(
        AccountService accounts,
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<SessionResponse>> Login(
        AccountService accounts,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accounts.LoginAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <remarks>
    /// Empty, because the answer is already written: Identity's token handler
    /// writes the tokens when the account is signed in with it, the same way
    /// its own <c>MapIdentityApi</c> login does.
    /// </remarks>
    private static async Task<EmptyHttpResult> IssueTokens(
        AccountService accounts,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        await accounts.IssueTokensAsync(request, cancellationToken);
        return TypedResults.Empty;
    }

    /// <remarks>Empty for the same reason as <see cref="IssueTokens"/>.</remarks>
    private static async Task<EmptyHttpResult> RefreshTokens(
        AccountService accounts,
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await accounts.RefreshTokensAsync(request, cancellationToken);
        return TypedResults.Empty;
    }

    /// <remarks>
    /// <c>[FromBody]</c> is required rather than decorative: ASP.NET Core will
    /// not infer a body on DELETE, because a body on DELETE is unusual enough
    /// that inferring one is more likely to be a mistake than an intention.
    /// Here it is the intention — the password is what authorises this, and the
    /// verb is still the honest one for erasing a resource.
    /// </remarks>
    private static async Task<Ok<AccountDeletionResponse>> DeleteAccount(
        AccountService accounts,
        [FromBody] DeleteAccountRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await accounts.DeleteAsync(request, cancellationToken));

    private static async Task<NoContent> Logout(AccountService accounts)
    {
        await accounts.LogoutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Ok<SessionResponse>> Session(
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var result = await accounts.GetSessionAsync(cancellationToken);
        return TypedResults.Ok(result);
    }
}
