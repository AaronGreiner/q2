using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Deletes lines from the bell once they are older than
/// <see cref="Notification.KeptFor"/>.
/// </summary>
/// <remarks>
/// The retention policy docs/privacy.md asks for, for the one table q2 fills
/// purely in order to tell somebody something. Everything a line says also
/// lives where it belongs — the friendship, the goal, the verdict — so deleting
/// it loses nothing but the reminder.
///
/// Like every other background job here it is idempotent and holds no state:
/// "older than thirty days" is a question the rows answer themselves. It does
/// not run in <c>AutomatedTest</c>; the tests call <see cref="RunOnceAsync"/>.
/// </remarks>
public sealed class NotificationRetentionWorker(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<NotificationRetentionWorker> logger) : BackgroundService
{
    /// <summary>A few times a day. The deadline is a month; a few hours either side of it change nothing.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification retention failed; the next pass will pick it up");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass. Returns how many lines went.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();

        var cutoff = timeProvider.GetUtcNow() - Notification.KeptFor;

        var removed = await database.Notifications
            .Where(line => line.OccurredAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            // A count, and nothing about whose.
            logger.LogInformation("Removed {NotificationCount} notification(s) past their retention", removed);
        }

        return removed;
    }
}
