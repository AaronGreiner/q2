using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Challenges;

/// <summary>
/// HTTP surface for the daily challenge.
/// </summary>
/// <remarks>
/// Everything about today is addressed as <c>/today</c> rather than by the
/// challenge's id, and that is not a shortcut: there is exactly one running at
/// any moment, and a client that had to name it could name yesterday's. The
/// archive is the only place a past challenge appears, and it is reached
/// through the contributions rather than through the challenges.
///
/// Nothing here takes an image. The bytes went up separately
/// (<c>POST /api/images</c>) and only the id travels, so a slow upload and a
/// refused contribution are never the same request.
/// </remarks>
public static class ChallengeEndpoints
{
    public static IEndpointRouteBuilder MapChallengeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var challenges = endpoints.MapGroup("/api/challenges").WithTags("Challenges").RequireAuthorization();

        challenges.MapGet("/today", GetToday)
            .WithName("GetTodaysChallenge")
            .WithSummary("Today's prompt and the room of your friends' contributions, or nothing if none is running.")
            .Produces<ChallengeTodayResponse>();

        challenges.MapPost("/today/entry", Submit)
            .WithName("SubmitChallengeEntry")
            .WithSummary("Contributes a photograph. A second one replaces the first.")
            .Produces<ChallengeRoomResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        challenges.MapDelete("/today/entry", Withdraw)
            .WithName("WithdrawChallengeEntry")
            .WithSummary("Takes your contribution back. Your friends' are covered again afterwards.")
            .Produces<ChallengeTodayResponse>()
            .ProducesValidationProblem();

        challenges.MapPost("/entries/{id:guid}/reactions", React)
            .WithName("ReactToChallengeEntry")
            .WithSummary("Adds or takes back a reaction. The same kind twice takes it back.")
            .Produces<ChallengeEntryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        challenges.MapGet("/archive", GetArchive)
            .WithName("ListChallengeArchive")
            .WithSummary("Every challenge you have taken part in, newest first. Your own contributions only.")
            .Produces<IReadOnlyList<ChallengeArchiveEntryResponse>>();

        return endpoints;
    }

    private static async Task<Ok<ChallengeTodayResponse>> GetToday(
        ChallengeService challenges,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await challenges.TodayAsync(cancellationToken));

    private static async Task<Ok<ChallengeRoomResponse>> Submit(
        ChallengeService challenges,
        SubmitChallengeEntryRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await challenges.SubmitAsync(request, cancellationToken));

    private static async Task<Ok<ChallengeTodayResponse>> Withdraw(
        ChallengeService challenges,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await challenges.WithdrawAsync(cancellationToken));

    private static async Task<Ok<ChallengeEntryResponse>> React(
        ChallengeService challenges,
        Guid id,
        ReactToChallengeEntryRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await challenges.ReactAsync(id, request, cancellationToken));

    private static async Task<Ok<IReadOnlyList<ChallengeArchiveEntryResponse>>> GetArchive(
        ChallengeService challenges,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await challenges.ArchiveAsync(cancellationToken));
}
