using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Notifications;

/// <summary>Who of a notification's recipients is interrupted, and how.</summary>
/// <param name="Payload">What they are handed, the same whichever way. Null when nobody is.</param>
/// <param name="Banner">People looking at the app, to be shown it there.</param>
/// <param name="Push">People who are not, to be sent it on whatever devices they have.</param>
public sealed record InterruptionPlan(
    NotificationResponse? Payload,
    IReadOnlyList<Guid> Banner,
    IReadOnlyList<Guid> Push)
{
    public static InterruptionPlan Nobody { get; } = new(Payload: null, [], []);
}

/// <summary>
/// Decides, once per person, whether a notification interrupts them — as a
/// banner in the app they are looking at, as a push to their devices, or not
/// at all.
/// </summary>
/// <remarks>
/// The rule is <see cref="NotificationRules.InterruptionFor"/>; everything here
/// is fetching what that rule needs. One decision for both routes rather than
/// one on each: "is this person looking" read twice can change in between, and
/// somebody putting their phone away at that moment would be told twice, or not
/// at all.
///
/// Somebody with no settings row has the defaults rather than silence. A row is
/// written at sign-up and on first read, so an absent one is an implementation
/// detail, not an answer — and reading it as "off" would make the switch that
/// says "on" a lie for everybody who never opened it.
/// </remarks>
public sealed class InterruptionPlanner(
    Q2DbContext database,
    LiveConnections connections,
    TimeZoneResolver timeZones,
    TimeProvider timeProvider)
{
    public async Task<InterruptionPlan> PlanAsync(
        NotificationEvent notification,
        IReadOnlyCollection<Guid> recipients,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(recipients);

        if (recipients.Count == 0)
        {
            return InterruptionPlan.Nobody;
        }

        var now = timeProvider.GetUtcNow();

        var preferences = await database.UserSettings
            .AsNoTracking()
            .Where(settings => recipients.Contains(settings.PersonId))
            .ToDictionaryAsync(settings => settings.PersonId, settings => settings.Notifications, cancellationToken);

        // Quiet hours are read against each person's own zone, which is why the
        // people are loaded as well as their settings: one deployment-wide "is
        // it night" would be night in Berlin for somebody in Auckland.
        var zones = await database.People
            .AsNoTracking()
            .Where(person => recipients.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, person => person.TimeZoneId, cancellationToken);

        var muted = await MutedAsync(recipients, notification, cancellationToken);
        var banner = new List<Guid>();
        var push = new List<Guid>();

        foreach (var personId in recipients)
        {
            var localTime = TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, timeZones.For(zones.GetValueOrDefault(personId)).Zone).DateTime);

            var interruption = NotificationRules.InterruptionFor(
                preferences.GetValueOrDefault(personId) ?? NotificationPreferences.Default,
                notification.Kind,
                localTime,
                isWatching: connections.IsWatching(personId),
                isMuted: muted.Contains(personId));

            switch (interruption)
            {
                case Interruption.Banner:
                    banner.Add(personId);
                    break;

                case Interruption.Push:
                    push.Add(personId);
                    break;

                case Interruption.None:
                    break;
            }
        }

        if (banner.Count == 0 && push.Count == 0)
        {
            return InterruptionPlan.Nobody;
        }

        return new InterruptionPlan(await PayloadAsync(notification, occurredAt, now, cancellationToken), banner, push);
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
    /// What a banner and a device are both handed: the same shape as a line in
    /// the bell.
    /// </summary>
    /// <remarks>
    /// One contract for all three, so the service worker and the open app
    /// compose their sentence with the very function the bell uses, and none of
    /// them can drift from the others.
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
