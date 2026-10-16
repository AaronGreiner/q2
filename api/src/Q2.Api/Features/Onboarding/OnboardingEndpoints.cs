using Microsoft.AspNetCore.Http.HttpResults;
using Q2.Api.Features.Accounts;

namespace Q2.Api.Features.Onboarding;

/// <summary>HTTP surface for the first-run introduction.</summary>
/// <remarks>
/// The handle travels in a body like every other request here, and not in the
/// address: it is public, but an address is what lands in request logs and
/// breadcrumbs, and what somebody types while deciding is not a log line.
/// </remarks>
public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var onboarding = endpoints.MapGroup("/api/onboarding").WithTags("Onboarding").RequireAuthorization();

        onboarding.MapPost("/handle/check", CheckHandle)
            .WithName("CheckHandle")
            .WithSummary("Says whether a handle could be yours: available, taken, invalid or the one you have.")
            .Produces<HandleCheckResponse>();

        onboarding.MapPut("/handle", ChooseHandle)
            .WithName("ChooseHandle")
            .WithSummary("Gives you the handle you chose. Only while the introduction is open.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem();

        onboarding.MapPost("/complete", Complete)
            .WithName("CompleteOnboarding")
            .WithSummary("Records that the introduction is behind you. Idempotent.")
            .Produces<SessionResponse>();

        return endpoints;
    }

    private static async Task<Ok<HandleCheckResponse>> CheckHandle(
        OnboardingService onboarding,
        HandleRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await onboarding.CheckHandleAsync(request, cancellationToken));

    private static async Task<Ok<SessionResponse>> ChooseHandle(
        OnboardingService onboarding,
        HandleRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await onboarding.ChooseHandleAsync(request, cancellationToken));

    private static async Task<Ok<SessionResponse>> Complete(
        OnboardingService onboarding,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await onboarding.CompleteAsync(cancellationToken));
}
