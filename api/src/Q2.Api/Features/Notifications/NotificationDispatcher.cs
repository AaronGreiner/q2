using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Delivers one <see cref="NotificationDelivery"/>: live to whoever is looking,
/// then as a push to whoever is not.
/// </summary>
/// <remarks>
/// Two different things reach an open app, for two different audiences:
///
/// - **What moved** — fresh badge counts and "this part changed" — goes to
///   everybody concerned who is looking, whatever they have switched off. It is
///   what they are allowed to find, not an interruption.
/// - **The notification itself**, for the app to show as a banner, goes only to
///   whoever it may interrupt, and to them *instead of* a push. Who gets which
///   is one decision per person (<see cref="InterruptionPlanner"/>), so nobody
///   is told twice, and nobody who puts the app away in between is told not at
///   all.
/// </remarks>
public sealed class NotificationDispatcher(
    Q2DbContext database,
    IHubContext<LiveHub> hub,
    LiveConnections connections,
    CountsService counts,
    InterruptionPlanner interruptions,
    PushDelivery push)
{
    public async Task DispatchAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        await RefreshAsync(delivery, cancellationToken);

        if (delivery.Notification is not { } notification)
        {
            return;
        }

        var recipients = delivery.Everyone
            ? await EveryoneWhoCanBeToldAsync(cancellationToken)
            : delivery.People;

        var plan = await interruptions.PlanAsync(notification, recipients, delivery.At, cancellationToken);

        if (plan.Payload is not { } payload)
        {
            return;
        }

        // After the refresh above, so an app that opens the banner finds the
        // screen behind it already read again.
        foreach (var personId in plan.Banner)
        {
            await hub.Clients.Group(LiveHub.GroupOf(personId))
                .SendAsync(LiveEvents.Notification, payload, cancellationToken);
        }

        await push.DeliverAsync(payload, plan.Push, cancellationToken);
    }

    /// <summary>Tells whoever is looking what moved: the counts, and which part of the screen.</summary>
    private async Task RefreshAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Everyone)
        {
            // No counts: nobody's numbers move when a prompt goes up, and
            // computing everybody's to say so would be the one delivery whose
            // cost grew with the size of the whole deployment.
            if (delivery.Change is { } change)
            {
                await hub.Clients.All.SendAsync(LiveEvents.Changed, change, cancellationToken);
            }

            return;
        }

        foreach (var personId in delivery.People)
        {
            // Nobody looking, nothing to say: a count computed for a closed app
            // would be thrown away unread.
            if (!connections.IsWatching(personId))
            {
                continue;
            }

            var group = hub.Clients.Group(LiveHub.GroupOf(personId));

            if (delivery.Change is { } change)
            {
                await group.SendAsync(LiveEvents.Changed, change, cancellationToken);
            }

            await group.SendAsync(LiveEvents.Counts, await counts.ForAsync(personId, cancellationToken), cancellationToken);
        }
    }

    /// <summary>
    /// Everybody the challenge could reach: whoever has the app open, and
    /// whoever has a device to buzz.
    /// </summary>
    /// <remarks>
    /// The honest recipient set for something that is the same for everyone,
    /// and the one that would not scale: a deployment with a hundred thousand
    /// people would fan this out rather than send it from one pass, which is a
    /// different design rather than a bigger loop.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> EveryoneWhoCanBeToldAsync(CancellationToken cancellationToken)
    {
        var withADevice = await database.PushSubscriptions
            .AsNoTracking()
            .Select(subscription => subscription.PersonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. withADevice.Union(connections.Watching())];
    }
}
