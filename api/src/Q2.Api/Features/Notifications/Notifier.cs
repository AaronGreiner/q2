using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// The one way anything in q2 tells anybody anything.
/// </summary>
/// <remarks>
/// A message, a friend request, a verdict, the evening warning and the daily
/// challenge all go through here, and there is no second route beside it
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)). Two calls,
/// on either side of the caller's own save:
///
/// 1. <see cref="StageAsync"/> before it. The lines the bell keeps are added to
///    the same unit of work as the change that caused them, so "Jonas hat
///    angenommen" and the friendship are committed together or not at all.
/// 2. <see cref="FlushAsync"/> after it. Only then is anything delivered — live
///    to an open app, as a push to a device — because a network call inside a
///    transaction is a transaction held open for as long as somebody else's
///    server takes ([0023](../../../../docs/adr/0023-web-push.md)).
///
/// Delivery is handed to <see cref="INotificationQueue"/> rather than done
/// here, so a message into a group of twenty does not wait on twenty people's
/// push services before its sender sees it arrive.
///
/// Two rules are applied here rather than further down, because they are about
/// the event and not about any one way of delivering it: **nobody is told about
/// their own doing**, and **a block hides somebody here as it does everywhere
/// else** — in both directions, and without saying so (<see cref="BlockList"/>).
///
/// Scoped: what it collects belongs to one request or one background pass.
/// </remarks>
public sealed class Notifier(
    Q2DbContext database,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    INotificationQueue queue,
    ILogger<Notifier> logger)
{
    private readonly List<NotificationDelivery> _pending = [];

    /// <summary>
    /// Records a notification for <paramref name="recipients"/>, ready to be
    /// delivered once the caller has saved.
    /// </summary>
    public async Task StageAsync(
        NotificationEvent notification,
        IEnumerable<Guid> recipients,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(recipients);

        var people = recipients
            .Where(id => id != Guid.Empty && id != notification.ActorPersonId)
            .Distinct()
            .ToList();

        if (people.Count > 0 && notification.ActorPersonId is { } actor)
        {
            var hidden = await BlockList.HiddenFromAsync(database, actor, cancellationToken);
            people.RemoveAll(hidden.Contains);
        }

        if (people.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        if (NotificationRules.IsKept(notification.Kind))
        {
            foreach (var person in people)
            {
                database.Notifications.Add(Notification.Create(idGenerator.NewId(), person, notification, now));
            }
        }

        _pending.Add(NotificationDelivery.To(notification, people, now));
    }

    /// <summary>
    /// Records a notification for everybody — the daily challenge, and only
    /// that.
    /// </summary>
    /// <remarks>
    /// The one delivery not scoped to anybody's friends, because the prompt is
    /// deliberately the same for all of them
    /// ([0021](../../../../docs/adr/0021-daily-challenge.md)). Nothing is kept
    /// in the bell for it: the banner is where it lives.
    /// </remarks>
    public void StageForEveryone(NotificationEvent notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _pending.Add(NotificationDelivery.ToEveryone(notification, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Says that something these people can see has changed, with nothing to
    /// announce about it.
    /// </summary>
    /// <remarks>
    /// A chat that was read on the phone, so the laptop's badge moves; a
    /// request that was withdrawn, so it leaves the other person's screen. The
    /// counts go to everybody named; <paramref name="area"/> is left out when
    /// only a number moved.
    /// </remarks>
    public void Touch(IEnumerable<Guid> people, LiveArea? area = null, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(people);

        var distinct = people.Where(person => person != Guid.Empty).Distinct().ToList();

        if (distinct.Count == 0)
        {
            return;
        }

        _pending.Add(NotificationDelivery.Refresh(
            distinct,
            area is { } changed ? new LiveChange(changed, id) : null,
            timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Takes a reaction back out of a bell that has not seen it yet.
    /// </summary>
    /// <remarks>
    /// Only while it is unseen. Once somebody has read "Mara hat reagiert", it
    /// happened; taking the reaction back later changes the reaction, not the
    /// fact that it was there. Nothing is ever sent for a withdrawal.
    /// </remarks>
    public async Task RetractAsync(
        NotificationKind kind,
        Guid recipientPersonId,
        Guid actorPersonId,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var seen = await database.People
            .AsNoTracking()
            .Where(person => person.Id == recipientPersonId)
            .Select(person => person.NotificationsSeenAt)
            .SingleOrDefaultAsync(cancellationToken) ?? DateTimeOffset.MinValue;

        var unseen = await database.Notifications
            .Where(line => line.RecipientPersonId == recipientPersonId
                && line.Kind == kind
                && line.ActorPersonId == actorPersonId
                && line.TargetId == targetId
                && line.OccurredAt > seen)
            .OrderByDescending(line => line.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (unseen is null)
        {
            return;
        }

        database.Notifications.Remove(unseen);
        Touch([recipientPersonId]);
    }

    /// <summary>
    /// Hands everything staged to delivery. Call after the caller's save.
    /// </summary>
    /// <remarks>
    /// A failure is never allowed to reach the caller: the change that caused
    /// the notification is committed by now, and failing its request would
    /// tell somebody their message was not sent when it was.
    /// </remarks>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = _pending.ToList();
        _pending.Clear();

        foreach (var delivery in batch)
        {
            try
            {
                await queue.EnqueueAsync(delivery, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "A notification could not be handed to delivery");
            }
        }
    }
}
