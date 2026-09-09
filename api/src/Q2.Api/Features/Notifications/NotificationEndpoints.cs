using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// HTTP surface for notifications.
/// </summary>
/// <remarks>
/// Three endpoints and none of them sends anything. Notifications are produced
/// by the two background jobs that already exist — a window falling due, a
/// challenge being published — and a client that could ask for one would be a
/// client that could send somebody else a notification.
/// </remarks>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var notifications = endpoints.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        notifications.MapGet("/key", GetKey)
            .WithName("GetPushKey")
            .WithSummary("Whether this deployment sends notifications, and the key to subscribe with.")
            .Produces<PushKeyResponse>();

        notifications.MapPost("/subscribe", Subscribe)
            .WithName("SubscribeToNotifications")
            .WithSummary("Registers this browser, or brings its keys up to date.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        notifications.MapPost("/unsubscribe", Unsubscribe)
            .WithName("UnsubscribeFromNotifications")
            .WithSummary("Forgets this browser. Succeeds whether or not there was anything to forget.")
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static Ok<PushKeyResponse> GetKey(NotificationService notifications) =>
        TypedResults.Ok(notifications.Key());

    private static async Task<NoContent> Subscribe(
        NotificationService notifications,
        SubscribeRequest request,
        CancellationToken cancellationToken)
    {
        await notifications.SubscribeAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Unsubscribe(
        NotificationService notifications,
        UnsubscribeRequest request,
        CancellationToken cancellationToken)
    {
        await notifications.UnsubscribeAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }
}
