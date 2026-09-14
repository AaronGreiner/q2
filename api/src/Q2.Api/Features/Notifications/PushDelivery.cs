using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Decides whether anything goes to a device, and sends it.
/// </summary>
/// <remarks>
/// The last of the three ways, and the only one that can interrupt somebody.
/// Everything that can stop it is one rule —
/// <see cref="NotificationRules.ShouldPush"/> — and everything here is fetching
/// what that rule needs and acting on the push services' answers.
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
    LiveConnections connections,
    TimeZoneResolver timeZones,
    TimeProvider timeProvider,
    ILogger<PushDelivery> logger)
{
    /// <summary>
    /// Sends <paramref name="notification"/> to every device in
    /// <paramref name="recipients"/> that wants it now. Returns how many took it.
    /// </summary>
    public async Task<int> DeliverAsync(
        NotificationEvent notification,
        IReadOnlyCollection<Guid> recipients,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(recipients);

        if (!sender.IsConfigured || recipients.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();

        var subscriptions = await database.PushSubscriptions
            .Where(subscription => recipients.Contains(subscription.PersonId))
            .ToListAsync(cancellationToken);

        // The normal state, not an edge case: permission is asked for once and
        // rarely granted.
        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var wanted = await WhoWantsAsync(
            [.. subscriptions.Select(subscription => subscription.PersonId).Distinct()],
            notification,
            now,
            cancellationToken);

        if (wanted.Count == 0)
        {
            return 0;
        }

        var payload = await PayloadAsync(notification, occurredAt, now, cancellationToken);
        var delivered = 0;
        var abandoned = new List<PushSubscription>();

        foreach (var subscription in subscriptions.Where(candidate => wanted.Contains(candidate.PersonId)))
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
                notification.Kind,
                delivered,
                abandoned.Count);
        }

        return delivered;
    }

    /// <summary>Of these people, the ones a push may reach right now.</summary>
    /// <remarks>
    /// Quiet hours are read against each person's own zone, which is why the
    /// people are loaded as well as their settings: one deployment-wide "is it
    /// night" would be night in Berlin for somebody in Auckland.
    ///
    /// Somebody with no settings row has the defaults rather than silence. A
    /// row is written at sign-up and on first read, so an absent one is an
    /// implementation detail, not an answer — and reading it as "off" would
    /// make the switch that says "on" a lie for everybody who never opened it.
    /// </remarks>
    private async Task<HashSet<Guid>> WhoWantsAsync(
        IReadOnlyCollection<Guid> personIds,
        NotificationEvent notification,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var preferences = await database.UserSettings
            .AsNoTracking()
            .Where(settings => personIds.Contains(settings.PersonId))
            .ToDictionaryAsync(settings => settings.PersonId, settings => settings.Notifications, cancellationToken);

        var zones = await database.People
            .AsNoTracking()
            .Where(person => personIds.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, person => person.TimeZoneId, cancellationToken);

        var muted = await MutedAsync(personIds, notification, cancellationToken);
        var wanted = new HashSet<Guid>();

        foreach (var personId in personIds)
        {
            var localTime = TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, timeZones.For(zones.GetValueOrDefault(personId)).Zone).DateTime);

            if (NotificationRules.ShouldPush(
                    preferences.GetValueOrDefault(personId) ?? NotificationPreferences.Default,
                    notification.Kind,
                    localTime,
                    isWatching: connections.IsWatching(personId),
                    isMuted: muted.Contains(personId)))
            {
                wanted.Add(personId);
            }
        }

        return wanted;
    }

    /// <summary>Who has muted the conversation a message was written in.</summary>
    private async Task<HashSet<Guid>> MutedAsync(
        IReadOnlyCollection<Guid> personIds,
        NotificationEvent notification,
        CancellationToken cancellationToken)
    {
        if (notification.Kind != NotificationKind.MessageReceived || notification.TargetId is not { } conversationId)
        {
            return [];
        }

        return
        [
            .. await database.ConversationParticipants
                .AsNoTracking()
                .Where(participant => participant.ConversationId == conversationId
                    && participant.MutedAt != null
                    && personIds.Contains(participant.PersonId))
                .Select(participant => participant.PersonId)
                .ToListAsync(cancellationToken),
        ];
    }

    /// <summary>
    /// What the device is handed: the same shape as a line in the bell.
    /// </summary>
    /// <remarks>
    /// One contract for both, so the service worker composes its sentence with
    /// the very function the bell uses and the two cannot drift apart.
    /// </remarks>
    private async Task<NotificationResponse> PayloadAsync(
        NotificationEvent notification,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var actor = notification.ActorPersonId is { } actorId
            ? await database.People.AsNoTracking().SingleOrDefaultAsync(person => person.Id == actorId, cancellationToken)
            : null;

        return NotificationResponse.ForDevice(
            notification,
            actor is null ? null : PersonSummary.From(actor, now),
            occurredAt);
    }
}
