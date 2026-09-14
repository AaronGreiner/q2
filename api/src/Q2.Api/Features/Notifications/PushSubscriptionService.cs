using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// This browser's half of notifications: the key to subscribe with, and
/// registering or forgetting a device.
/// </summary>
/// <remarks>
/// Nothing here sends anything. What goes to a device is decided by
/// <see cref="PushDelivery"/>, for notifications only <see cref="Notifier"/>
/// produces — a client that could ask for one would be a client that could
/// send somebody else one.
/// </remarks>
public sealed class PushSubscriptionService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IPushSender sender,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<PushSubscriptionService> logger)
{
    /// <summary>Whether this deployment can send at all, and the key to subscribe with.</summary>
    public PushKeyResponse Key() => new(sender.IsConfigured, sender.PublicKey);

    /// <summary>
    /// Registers this browser, or brings its keys up to date.
    /// </summary>
    /// <remarks>
    /// Keyed on the endpoint rather than the person: the same account on two
    /// devices is two subscriptions and both should ring. An endpoint that
    /// already belongs to somebody else is taken over rather than refused —
    /// that is a shared device where the previous person signed out, and the
    /// browser has quite correctly given the same endpoint to whoever is there
    /// now.
    /// </remarks>
    /// <exception cref="DomainValidationException">The browser sent something unusable.</exception>
    public async Task SubscribeAsync(SubscribeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var endpoint = request.Endpoint?.Trim() ?? string.Empty;

        var existing = await database.PushSubscriptions
            .SingleOrDefaultAsync(subscription => subscription.Endpoint == endpoint, cancellationToken);

        if (existing is not null && existing.PersonId == me.Id)
        {
            existing.Refresh(request.PublicKey?.Trim() ?? string.Empty, request.AuthSecret?.Trim() ?? string.Empty, now);
        }
        else
        {
            if (existing is not null)
            {
                database.PushSubscriptions.Remove(existing);
            }

            database.PushSubscriptions.Add(PushSubscription.Create(
                idGenerator.NewId(),
                me.Id,
                request.Endpoint,
                request.PublicKey,
                request.AuthSecret,
                now));
        }

        await database.SaveChangesAsync(cancellationToken);

        // Never the endpoint: it is a stable handle for one browser
        // installation and the most identifying thing this feature touches.
        logger.LogInformation("A device subscribed to notifications");
    }

    /// <summary>
    /// Forgets a device.
    /// </summary>
    /// <remarks>
    /// Succeeds whether or not there was anything to forget. Unsubscribing is
    /// what somebody does when they are unsure of their state, and an error
    /// there would leave them with the subscription they were trying to remove.
    /// </remarks>
    public async Task UnsubscribeAsync(UnsubscribeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var endpoint = request.Endpoint?.Trim() ?? string.Empty;

        var existing = await database.PushSubscriptions.SingleOrDefaultAsync(
            subscription => subscription.Endpoint == endpoint && subscription.PersonId == me.Id,
            cancellationToken);

        if (existing is null)
        {
            return;
        }

        database.PushSubscriptions.Remove(existing);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("A device unsubscribed from notifications");
    }
}
