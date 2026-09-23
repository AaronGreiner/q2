using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// HTTP surface for photographs and the votes on them.
/// </summary>
/// <remarks>
/// Delivering a photograph stays on the goal (<c>POST /api/goals/{id}/proof</c>)
/// because it is something you do *to a goal*, and everything after it is about
/// the photograph itself. Nothing here takes an image: the bytes went up
/// separately (<c>POST /api/images</c>) and only the id travels, so a slow
/// upload and a failed vote are never the same request.
/// </remarks>
public static class ProofEndpoints
{
    public static IEndpointRouteBuilder MapProofEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var proofs = endpoints.MapGroup("/api/proofs").WithTags("Proofs").RequireAuthorization();

        proofs.MapGet("/pending", ListPending)
            .WithName("ListPendingProofs")
            .WithSummary("The photographs waiting for your vote, newest first.")
            .Produces<IReadOnlyList<FeedProofResponse>>();

        proofs.MapGet("/mine", ListMine)
            .WithName("ListOwnProofs")
            .WithSummary("Every photograph you have delivered, newest first, whatever became of it.")
            .Produces<IReadOnlyList<OwnProofResponse>>();

        proofs.MapGet("/{id:guid}", GetProof)
            .WithName("GetProof")
            .WithSummary("Returns one photograph, if it is yours to see.")
            .Produces<ProofResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        proofs.MapPost("/{id:guid}/vote", Vote)
            .WithName("VoteOnProof")
            .WithSummary("Confirms or doubts a photograph. One say per person, and it stands.")
            .Produces<ProofResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        proofs.MapPost("/{id:guid}/reactions", React)
            .WithName("ReactToProof")
            .WithSummary("Adds or takes back a reaction. The same kind twice takes it back.")
            .Produces<ProofResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/goals/{id:guid}/proof", Submit)
            .RequireAuthorization()
            .WithTags("Proofs")
            .WithName("SubmitGoalProof")
            .WithSummary("Delivers a photograph into the goal's open window for its friends to vote on.")
            .Produces<DeliveredProofResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<FeedProofResponse>>> ListPending(
        ProofService proofs,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await proofs.FeedAsync(cancellationToken));

    private static async Task<Ok<IReadOnlyList<OwnProofResponse>>> ListMine(
        ProofService proofs,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await proofs.ListOwnAsync(cancellationToken));

    private static async Task<Ok<ProofResponse>> GetProof(
        ProofService proofs,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await proofs.GetAsync(id, cancellationToken));

    private static async Task<Created<DeliveredProofResponse>> Submit(
        ProofService proofs,
        Guid id,
        SubmitProofRequest request,
        CancellationToken cancellationToken)
    {
        var delivered = await proofs.SubmitAsync(id, request, cancellationToken);
        return TypedResults.Created($"/api/proofs/{delivered.Proof.Id}", delivered);
    }

    private static async Task<Ok<ProofResponse>> Vote(
        ProofService proofs,
        Guid id,
        CastVoteRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await proofs.VoteAsync(id, request, cancellationToken));

    private static async Task<Ok<ProofResponse>> React(
        ProofService proofs,
        Guid id,
        ReactToProofRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await proofs.ReactAsync(id, request, cancellationToken));
}
