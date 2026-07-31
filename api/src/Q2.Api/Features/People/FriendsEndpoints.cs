using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.People;

/// <summary>HTTP surface for friends, requests and suggestions.</summary>
public static class FriendsEndpoints
{
    public static IEndpointRouteBuilder MapFriendsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/friends").WithTags("Friends");

        group.MapGet("/", GetFriends)
            .WithName("GetFriends")
            .WithSummary("Returns your friends, pending requests and suggestions.")
            .Produces<FriendsResponse>();

        group.MapPost("/requests/{id:guid}/accept", AcceptRequest)
            .WithName("AcceptFriendRequest")
            .WithSummary("Accepts a pending friend request.")
            .Produces<FriendResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/requests/{id:guid}/decline", DeclineRequest)
            .WithName("DeclineFriendRequest")
            .WithSummary("Turns a pending friend request down.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/suggestions/{id:guid}/request", SendRequest)
            .WithName("SendFriendRequest")
            .WithSummary("Asks a suggested person to be friends.")
            .Produces<FriendSuggestionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Ok<FriendsResponse>> GetFriends(
        FriendsService friends,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await friends.GetAsync(search, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<FriendResponse>> AcceptRequest(
        FriendsService friends,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await friends.AcceptAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> DeclineRequest(
        FriendsService friends,
        Guid id,
        CancellationToken cancellationToken)
    {
        await friends.DeclineAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<FriendSuggestionResponse>> SendRequest(
        FriendsService friends,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await friends.InviteAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }
}
