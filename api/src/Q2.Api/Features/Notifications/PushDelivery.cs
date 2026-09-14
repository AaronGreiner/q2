using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Sends a notification to the devices of the people it may interrupt that way.
/// </summary>
/// <remarks>
/// The last of the three ways, and the only one that reaches somebody who is
/// not looking. Who that is has been decided by the time this runs
/// (<see cref="InterruptionPlanner"/>, by the one rule
/// <see cref="NotificationRules.InterruptionFor"/>); everything here is sending
/// and acting on the push services' answers.
///
/// **A subscription is a device.** The push service's own verdict that one is
/// gone (404, 410) deletes it at once; anything else counts towards
/// <see cref="PushSubscription.MaxConsecutiveFailures"/>, because a phone that
/// is off for a week produces timeouts rather than a verdict.
///
/// A failure never reaches the caller. Whatever caused the notification is
/// committed by the time this runs, and a push service having a bad afternoon
/// must not undo it.
/// </remarks>
public sealed class PushDelivery(
    Q2DbContext database,
    IPushSender sender,
    TimeProvider timeProvider,
    ILogger<PushDelivery> logger)
{
    /// <summary>
    /// Sends <paramref name="payload"/> to every device of
    /// <paramref name="recipients"/>. Returns how many took it.
    /// </summary>
    public async Task<int> DeliverAsync(
        NotificationResponse payload,
        IReadOnlyCollection<Guid> recipients,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(recipients);

        if (!sender.IsConfigured || recipients.Count == 0)
        {
            return 0;
        }

        var subscriptions = await database.PushSubscriptions
            .Where(subscription => recipients.Contains(subscription.PersonId))
            .ToListAsync(cancellationToken);

        // The normal state, not an edge case: permission is asked for once and
        // rarely granted.
        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var delivered = 0;
        var abandoned = new List<PushSubscription>();

        foreach (var subscription in subscriptions)
        {
            switch (await sender.SendAsync(subscription, payload, cancellationToken))
            {
                case PushOutcome.Delivered:
                    subscription.Delivered(now);
                    delivered++;
                    break;

                // The push service's own verdict that this device is gone: an
                // uninstalled app, a cleared browser, a revoked permission.
                case PushOutcome.Gone:
                    abandoned.Add(subscription);
                    break;

                default:
                    if (subscription.Failed())
                    {
                        abandoned.Add(subscription);
                    }

                    break;
            }
        }

        database.PushSubscriptions.RemoveRange(abandoned);
        await database.SaveChangesAsync(cancellationToken);

        if (delivered > 0 || abandoned.Count > 0)
        {
            // The kind and the counts. Never the endpoint — the most identifying
            // thing q2 stores — and never whose device (docs/privacy.md).
            logger.LogInformation(
                "Pushed a {NotificationKind} to {DeviceCount} device(s); forgot {AbandonedCount}",
                payload.Kind,
                delivered,
                abandoned.Count);
        }

        return delivered;
    }
}
