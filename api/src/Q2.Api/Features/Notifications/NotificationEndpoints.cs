using Microsoft.AspNetCore.Http.HttpResults;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// HTTP surface for notifications: the bell, the badges, this device, and the
/// live connection.
/// </summary>
/// <remarks>
/// None of it sends anything. Notifications are produced by the things that
/// cause them, through <see cref="Notifier"/>, and a client that could ask for
/// one would be a client that could send somebody else one.
/// </remarks>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var notifications = endpoints.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        notifications.MapGet("/", ListNotifications)
            .WithName("ListNotifications")
            .WithSummary("The bell, newest first. Reading it marks everything in it as seen.")
            .Produces<IReadOnlyList<NotificationResponse>>();

        notifications.MapDelete("/", ClearNotifications)
            .WithName("ClearNotifications")
            .WithSummary("Deletes every line in the bell up to and including `until`. Anything newer stays.")
            .Produces(StatusCodes.Status204NoContent);

        notifications.MapDelete("/{id:guid}", DismissNotification)
            .WithName("DismissNotification")
            .WithSummary("Deletes one line from the bell. Succeeds whether or not there was anything to delete.")
            .Produces(StatusCodes.Status204NoContent);

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

        var counts = endpoints.MapGroup("/api/counts")
            .WithTags("Notifications")
            .RequireAuthorization();

        counts.MapGet("/", GetCounts)
            .WithName("GetCounts")
            .WithSummary("Every number the app puts on a badge.")
            .Produces<CountsResponse>();

        /*
         * The live connection, guarded like every other feature route.
         *
         * Left out of the OpenAPI document: it is not a request and response,
         * and the two events it carries are named in LiveEvents and mirrored by
         * hand in the frontend. Their payloads — CountsResponse and a
         * LiveChange — are the documented part.
         */
        endpoints.MapHub<LiveHub>(LiveHub.Path)
            .RequireAuthorization()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<NotificationResponse>>> ListNotifications(
        InboxService inbox,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await inbox.ListAsync(cancellationToken));

    private static async Task<NoContent> ClearNotifications(
        InboxService inbox,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        await inbox.ClearAsync(until, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DismissNotification(
        InboxService inbox,
        Guid id,
        CancellationToken cancellationToken)
    {
        await inbox.DismissAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<CountsResponse>> GetCounts(
        CountsService counts,
        CurrentPerson currentPerson,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await counts.ForAsync(await currentPerson.GetIdAsync(cancellationToken), cancellationToken));

    private static Ok<PushKeyResponse> GetKey(PushSubscriptionService devices) =>
        TypedResults.Ok(devices.Key());

    private static async Task<NoContent> Subscribe(
        PushSubscriptionService devices,
        SubscribeRequest request,
        CancellationToken cancellationToken)
    {
        await devices.SubscribeAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Unsubscribe(
        PushSubscriptionService devices,
        UnsubscribeRequest request,
        CancellationToken cancellationToken)
    {
        await devices.UnsubscribeAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }
}
