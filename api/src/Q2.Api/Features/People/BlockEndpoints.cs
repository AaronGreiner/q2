using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.People;

/// <summary>
/// HTTP surface for blocking somebody.
/// </summary>
/// <remarks>
/// Under <c>/api/blocks</c> rather than under <c>/api/friends</c>, and that is
/// the same distinction <see cref="Block"/> makes: blocking is not a state a
/// friendship can be in, it is the thing that ends one.
///
/// Every one of these answers with the list afterwards rather than with nothing.
/// The screen behind them is the list, so a client that has just blocked
/// somebody would otherwise have to ask again to draw the result.
/// </remarks>
public static class BlockEndpoints
{
    public static IEndpointRouteBuilder MapBlockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var blocks = endpoints.MapGroup("/api/blocks").WithTags("Blocks").RequireAuthorization();

        blocks.MapGet("/", ListBlocked)
            .WithName("ListBlockedPeople")
            .WithSummary("The people you have blocked. Never the ones who blocked you.")
            .Produces<IReadOnlyList<PersonSummary>>();

        blocks.MapPost("/{personId:guid}", BlockPerson)
            .WithName("BlockPerson")
            .WithSummary("Blocks somebody and ends whatever connection there was. Blocking twice changes nothing.")
            .Produces<IReadOnlyList<PersonSummary>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        blocks.MapDelete("/{personId:guid}", UnblockPerson)
            .WithName("UnblockPerson")
            .WithSummary("Lifts a block you set. The friendship does not come back.")
            .Produces<IReadOnlyList<PersonSummary>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<PersonSummary>>> ListBlocked(
        BlockService blocks,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await blocks.ListAsync(cancellationToken));

    private static async Task<Ok<IReadOnlyList<PersonSummary>>> BlockPerson(
        BlockService blocks,
        Guid personId,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await blocks.BlockAsync(personId, cancellationToken));

    private static async Task<Ok<IReadOnlyList<PersonSummary>>> UnblockPerson(
        BlockService blocks,
        Guid personId,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await blocks.UnblockAsync(personId, cancellationToken));
}
