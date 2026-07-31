using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.People;

/// <summary>HTTP surface for friends, requests, suggestions and search.</summary>
/// <remarks>
/// Everything is addressed by <em>person</em> id rather than by friendship id.
/// The client always has the person — it came from a search result, a request
/// row or the friends list — and never has to hold on to the id of a row that
/// the next tap may delete and recreate.
/// </remarks>
public static class FriendsEndpoints
{
    public static IEndpointRouteBuilder MapFriendsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/friends").WithTags("Friends").RequireAuthorization();

        group.MapGet("/", GetFriends)
            .WithName("GetFriends")
            .WithSummary("Returns your friends, pending requests in both directions and suggestions.")
            .Produces<FriendsResponse>();

        group.MapGet("/search", SearchPeople)
            .WithName("SearchPeople")
            .WithSummary("Finds people by name or handle, with where you stand with each.")
            .Produces<IReadOnlyList<PersonSearchResultResponse>>();

        group.MapPost("/{personId:guid}/request", SendRequest)
            .WithName("SendFriendRequest")
            .WithSummary("Asks somebody to be friends.")
            .Produces<PersonSearchResultResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{personId:guid}/request", WithdrawRequest)
            .WithName("WithdrawFriendRequest")
            .WithSummary("Takes back a request you sent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/{personId:guid}/accept", AcceptRequest)
            .WithName("AcceptFriendRequest")
            .WithSummary("Accepts a pending friend request.")
            .Produces<FriendResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/{personId:guid}/decline", DeclineRequest)
            .WithName("DeclineFriendRequest")
            .WithSummary("Turns a pending friend request down.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{personId:guid}", RemoveFriend)
            .WithName("RemoveFriend")
            .WithSummary("Ends a friendship, for both people.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<FriendsResponse>> GetFriends(
        FriendsService friends,
        CancellationToken cancellationToken)
    {
        var result = await friends.GetAsync(cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<IReadOnlyList<PersonSearchResultResponse>>> SearchPeople(
        FriendsService friends,
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var result = await friends.SearchAsync(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<PersonSearchResultResponse>> SendRequest(
        FriendsService friends,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var result = await friends.RequestAsync(personId, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> WithdrawRequest(
        FriendsService friends,
        Guid personId,
        CancellationToken cancellationToken)
    {
        await friends.WithdrawAsync(personId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<FriendResponse>> AcceptRequest(
        FriendsService friends,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var result = await friends.AcceptAsync(personId, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> DeclineRequest(
        FriendsService friends,
        Guid personId,
        CancellationToken cancellationToken)
    {
        await friends.DeclineAsync(personId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveFriend(
        FriendsService friends,
        Guid personId,
        CancellationToken cancellationToken)
    {
        await friends.RemoveAsync(personId, cancellationToken);
        return TypedResults.NoContent();
    }
}
