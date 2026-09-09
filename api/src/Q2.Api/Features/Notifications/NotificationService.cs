using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Subscribing a device, and deciding whether anything actually goes to it.
/// </summary>
/// <remarks>
/// **Push is a delivery route, not a second product.** Everything this sends
/// already exists somewhere a person can read it: the warning is an activity
/// entry ([0019](../../../../docs/adr/0019-warning-and-balance.md)), the
/// challenge is on the start screen
/// ([0021](../../../../docs/adr/0021-daily-challenge.md)). What is new here is
/// only that it arrives without the app being open.
///
/// Which means the recipients are not decided here either. They are whoever
/// would have seen the thing anyway, and the callers pass that set in.
///
/// Three gates stand between a notification and a device, in this order:
///
/// 1. **The person's switch** for that kind of thing.
/// 2. **Quiet hours**, in their own zone (<see cref="QuietHours"/>).
/// 3. **A live subscription.** No device, nothing to do — which is the normal
///    state, because permission is asked for once and rarely granted.
/// </remarks>
public sealed class NotificationService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IPushSender sender,
    TimeZoneResolver timeZones,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<NotificationService> logger)
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

    /// <summary>
    /// Delivers one notification to everybody in <paramref name="recipients"/>
    /// who wants it. Returns how many devices took it.
    /// </summary>
    /// <remarks>
    /// Called <em>after</em> whatever produced the notification has been saved,
    /// never inside that transaction: this makes network calls, and a database
    /// transaction held open across one is a transaction held open for as long
    /// as somebody else's server feels like taking.
    ///
    /// A failure is never allowed to reach the caller. The callers are
    /// background jobs whose actual work — advancing windows, filling the queue
    /// — has already been committed, and a push service having a bad afternoon
    /// must not roll that back or stop the pass.
    /// </remarks>
    public async Task<int> NotifyAsync(
        IReadOnlyCollection<Guid> recipients,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(payload);

        if (!sender.IsConfigured || recipients.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();

        var subscriptions = await database.PushSubscriptions
            .Where(subscription => recipients.Contains(subscription.PersonId))
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var wanted = await WhoWantsAsync(
            [.. subscriptions.Select(subscription => subscription.PersonId).Distinct()],
            payload.Kind,
            now,
            cancellationToken);

        var delivered = 0;
        var abandoned = new List<PushSubscription>();

        foreach (var subscription in subscriptions.Where(s => wanted.Contains(s.PersonId)))
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
            logger.LogInformation(
                "Notified {DeviceCount} device(s) about a {PushKind}; forgot {AbandonedCount}",
                delivered,
                payload.Kind,
                abandoned.Count);
        }

        return delivered;
    }

    /// <summary>
    /// Of these people, the ones whose settings and clock allow this kind of
    /// notification right now.
    /// </summary>
    /// <remarks>
    /// Quiet hours are read against each person's own zone, which is why this
    /// loads the people as well as their settings. One deployment-wide "is it
    /// night" would be night in Berlin for somebody in Auckland.
    ///
    /// Somebody with no settings row is treated as having the defaults, not as
    /// wanting nothing. A row is written at sign-up and created on first read,
    /// so an absent one is an implementation detail rather than an answer — and
    /// the defaults *are* this person's preference until they change them.
    /// Reading "no row" as silence would mean the switch that says "on" is off
    /// for anybody who has never opened the settings screen.
    /// </remarks>
    private async Task<HashSet<Guid>> WhoWantsAsync(
        IReadOnlyCollection<Guid> personIds,
        PushKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await database.UserSettings
            .AsNoTracking()
            .Where(entry => personIds.Contains(entry.PersonId))
            .ToDictionaryAsync(entry => entry.PersonId, cancellationToken);

        var zones = await database.People
            .AsNoTracking()
            .Where(person => personIds.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, person => person.TimeZoneId, cancellationToken);

        var wanted = new HashSet<Guid>();

        foreach (var personId in personIds)
        {
            var preference = settings.GetValueOrDefault(personId)
                ?? UserSettings.CreateDefault(Guid.Empty, personId);

            if (!Wants(preference, kind))
            {
                continue;
            }

            var localTime = TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, timeZones.For(zones.GetValueOrDefault(personId)).Zone).DateTime);

            if (QuietHours.Covers(preference.QuietHoursFrom, preference.QuietHoursTo, localTime))
            {
                continue;
            }

            wanted.Add(personId);
        }

        return wanted;
    }

    /// <summary>
    /// Which switch governs which kind.
    /// </summary>
    /// <remarks>
    /// A switch statement rather than a lookup, so a new kind of notification
    /// is a compiler-visible decision about which preference it answers to —
    /// the same shape as <see cref="Q2.Api.Features.Images.ImageService.CanRead"/>,
    /// and for the same reason. The default is silence.
    /// </remarks>
    private static bool Wants(UserSettings settings, PushKind kind) => kind switch
    {
        PushKind.WindowAtRisk => settings.NotifyReminders,
        PushKind.ChallengePublished => settings.NotifyChallenge,
        _ => false,
    };
}
