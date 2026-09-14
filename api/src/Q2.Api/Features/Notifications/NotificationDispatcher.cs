using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Delivers one <see cref="NotificationDelivery"/>: live to whoever is looking,
/// then as a push to whoever is not.
/// </summary>
/// <remarks>
/// The order matters only for one thing: <see cref="PushDelivery"/> skips
/// anybody who is watching, so a person with the app open sees the change live
/// and their phone stays quiet.
/// </remarks>
public sealed class NotificationDispatcher(
    Q2DbContext database,
    IHubContext<LiveHub> hub,
    LiveConnections connections,
    CountsService counts,
    PushDelivery push)
{
    public async Task DispatchAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        await SendLiveAsync(delivery, cancellationToken);

        if (delivery.Notification is not { } notification)
        {
            return;
        }

        var recipients = delivery.Everyone
            ? await EveryoneWithADeviceAsync(cancellationToken)
            : delivery.People;

        await push.DeliverAsync(notification, recipients, delivery.At, cancellationToken);
    }

    private async Task SendLiveAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
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
    /// Everybody who could possibly be buzzed about the challenge.
    /// </summary>
    /// <remarks>
    /// The honest recipient set for something that is the same for everyone,
    /// and the one that would not scale: a deployment with a hundred thousand
    /// people would fan this out rather than send it from one pass, which is a
    /// different design rather than a bigger loop.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> EveryoneWithADeviceAsync(CancellationToken cancellationToken) =>
        await database.PushSubscriptions
            .AsNoTracking()
            .Select(subscription => subscription.PersonId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
