using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.People;

/// <summary>
/// HTTP surface for somebody's invite link.
/// </summary>
/// <remarks>
/// Two endpoints and no way to look a code up. Redeeming happens during
/// registration (<c>POST /api/auth/register</c>) and nowhere else: an endpoint
/// that answered "whose code is this?" would turn an unguessable string into
/// something worth guessing at.
/// </remarks>
public static class InviteEndpoints
{
    public static IEndpointRouteBuilder MapInviteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var invites = endpoints.MapGroup("/api/invite").WithTags("Invites").RequireAuthorization();

        invites.MapGet("/", GetInvite)
            .WithName("GetInviteCode")
            .WithSummary("Your invite code, created the first time you ask for it.")
            .Produces<InviteResponse>();

        invites.MapPost("/regenerate", Regenerate)
            .WithName("RegenerateInviteCode")
            .WithSummary("Replaces your code, so a link that got out stops working.")
            .Produces<InviteResponse>();

        return endpoints;
    }

    private static async Task<Ok<InviteResponse>> GetInvite(
        InviteService invites,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invites.GetAsync(cancellationToken));

    private static async Task<Ok<InviteResponse>> Regenerate(
        InviteService invites,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invites.RegenerateAsync(cancellationToken));
}
