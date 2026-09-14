using System.Threading.Channels;

namespace Q2.Api.Features.Notifications;

/// <summary>One thing to deliver, after the change that caused it has been saved.</summary>
/// <param name="Notification">What to announce, or null when a screen merely changed.</param>
/// <param name="People">Who to tell. Empty when <paramref name="Everyone"/> is set.</param>
/// <param name="Change">Which part of an open app to read again, if any.</param>
/// <param name="Everyone">The daily challenge: the one delivery addressed to nobody in particular.</param>
/// <param name="At">When it happened, which is what a device shows it as.</param>
public sealed record NotificationDelivery(
    NotificationEvent? Notification,
    IReadOnlyList<Guid> People,
    LiveChange? Change,
    bool Everyone,
    DateTimeOffset At)
{
    public static NotificationDelivery To(NotificationEvent notification, IReadOnlyList<Guid> people, DateTimeOffset at) =>
        new(notification, people, ChangeOf(notification), Everyone: false, at);

    public static NotificationDelivery ToEveryone(NotificationEvent notification, DateTimeOffset at) =>
        new(notification, [], ChangeOf(notification), Everyone: true, at);

    public static NotificationDelivery Refresh(IReadOnlyList<Guid> people, LiveChange? change, DateTimeOffset at) =>
        new(Notification: null, people, change, Everyone: false, at);

    private static LiveChange ChangeOf(NotificationEvent notification) =>
        new(NotificationRules.AreaFor(notification.Kind, notification.Target), notification.TargetId);
}

/// <summary>Where <see cref="Notifier"/> hands what it has to deliver.</summary>
/// <remarks>
/// An interface with two implementations, each for a stated reason: a real
/// queue for a running host, so a request never waits on a push service; and
/// an inline one for <c>AutomatedTest</c>, so an assertion made straight after
/// a request sees everything the request caused — the same reason the
/// background workers do not run there.
/// </remarks>
public interface INotificationQueue
{
    ValueTask EnqueueAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
}

/// <summary>
/// The queue a running host delivers from.
/// </summary>
/// <remarks>
/// In memory, bounded, and dropping the oldest when full. A restart loses
/// whatever was still waiting, and that is the posture quiet hours already
/// take: a notification is dropped rather than held, and everything it would
/// have said is already in the app — in the bell, in the chat, on the banner
/// ([0023](../../../../docs/adr/0023-web-push.md)). A durable outbox would be
/// the "Neubau" push was never meant to be.
/// </remarks>
public sealed class NotificationQueue : INotificationQueue
{
    /// <summary>Far more than one host produces between two reads of it.</summary>
    public const int Capacity = 1_000;

    private readonly Channel<NotificationDelivery> _channel = Channel.CreateBounded<NotificationDelivery>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<NotificationDelivery> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(NotificationDelivery delivery, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(delivery, cancellationToken);
}

/// <summary>
/// Delivers straight away, in the caller's own time. <c>AutomatedTest</c> only.
/// </summary>
/// <remarks>
/// In a fresh scope rather than the caller's, exactly as the worker would: the
/// dispatcher reads the database, and it must see what was committed rather
/// than whatever the caller's context still has tracked.
/// </remarks>
public sealed class InlineNotificationQueue(IServiceScopeFactory scopes) : INotificationQueue
{
    public async ValueTask EnqueueAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<NotificationDispatcher>()
            .DispatchAsync(delivery, cancellationToken);
    }
}

/// <summary>Reads <see cref="NotificationQueue"/> and delivers, one at a time.</summary>
/// <remarks>
/// Does not run in <c>AutomatedTest</c>, where
/// <see cref="InlineNotificationQueue"/> stands in for it. A failed delivery is
/// logged and the loop carries on: one bad push must not stop every one behind
/// it.
/// </remarks>
public sealed class NotificationDeliveryWorker(
    NotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var delivery in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider
                        .GetRequiredService<NotificationDispatcher>()
                        .DispatchAsync(delivery, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "A notification could not be delivered");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down. Whatever is still queued is dropped, deliberately
            // — see NotificationQueue.
        }
    }
}
