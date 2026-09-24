using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.People;

/// <summary>
/// HTTP surface for somebody's invite link.
/// </summary>
/// <remarks>
/// Your own code, replacing it, and the two things the page a link lands on
/// needs: who sent it, and accepting it for somebody who already has an
/// account. Somebody without one spends the code during registration
/// (<c>POST /api/auth/register</c>) instead.
///
/// The lookup is the one route in this group that answers without a session,
/// and it says so where it is mapped rather than living in a group of its own:
/// an exception to <c>RequireAuthorization</c> should be visible next to the
/// rule it breaks. Why it is safe to answer at all is in
/// docs/adr/0033-invite-links-for-new-and-existing-accounts.md.
///
/// The code travels in the body, never in the path. A path ends up in request
/// logs, in the browser's fetch breadcrumbs and in Sentry, which strip query
/// strings and bodies but keep paths — and the code is a credential in
/// everything but name.
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

        invites.MapPost("/preview", Preview)
            .AllowAnonymous()
            .WithName("PreviewInvite")
            .WithSummary("Whose link this is, and — with a session — where you stand with them.")
            .Produces<InvitePreviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        invites.MapPost("/accept", Accept)
            .WithName("AcceptInvite")
            .WithSummary("Become friends with whoever sent this link.")
            .Produces<PersonSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

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

    private static async Task<Ok<InvitePreviewResponse>> Preview(
        InviteCodeRequest request,
        InviteService invites,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invites.PreviewAsync(request.Code, cancellationToken));

    private static async Task<Ok<PersonSummary>> Accept(
        InviteCodeRequest request,
        InviteService invites,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await invites.AcceptAsync(request.Code, cancellationToken));
}
