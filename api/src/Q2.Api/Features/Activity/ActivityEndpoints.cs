using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Activity;

/// <summary>HTTP surface for the feed, kudos and the leaderboard.</summary>
public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var feed = endpoints.MapGroup("/api/feed").WithTags("Activity");

        feed.MapGet("/", ListFeed)
            .WithName("ListFeed")
            .WithSummary("Lists what your friends have been up to, newest first.")
            .Produces<IReadOnlyList<ActivityResponse>>();

        feed.MapPost("/{id:guid}/kudos", ToggleKudos)
            .WithName("ToggleKudos")
            .WithSummary("Gives kudos for an activity, or takes them back.")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/leaderboard", ListLeaderboard)
            .WithTags("Activity")
            .WithName("ListLeaderboard")
            .WithSummary("Ranks you and your friends by kudos received.")
            .Produces<IReadOnlyList<LeaderboardEntryResponse>>();

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<ActivityResponse>>> ListFeed(
        ActivityService activity,
        CancellationToken cancellationToken)
    {
        var result = await activity.ListFeedAsync(cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ActivityResponse>> ToggleKudos(
        ActivityService activity,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await activity.ToggleKudosAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<IReadOnlyList<LeaderboardEntryResponse>>> ListLeaderboard(
        ActivityService activity,
        CancellationToken cancellationToken)
    {
        var result = await activity.ListLeaderboardAsync(cancellationToken);
        return TypedResults.Ok(result);
    }
}
