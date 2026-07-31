using Microsoft.AspNetCore.Http.HttpResults;

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

        group.MapPost("/logout", Logout)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Ends the session. Succeeds even when there was none.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/session", Session)
            .RequireAuthorization()
            .WithName("GetSession")
            .WithSummary("Returns who is signed in.")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

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
