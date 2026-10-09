using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Draws, asks and settles the spot checks of every spot-check Don't
/// (docs/adr/0038-spot-checks.md).
/// </summary>
/// <remarks>
/// A worker of its own, beside <see cref="GoalMaintenanceWorker"/>, because of
/// one number: three minutes. A pass every ten minutes is fine for a deadline at
/// midnight and useless for a check that has to be asked within seconds of its
/// moment and decided three minutes later. This one looks every thirty seconds,
/// and only at the goals that take spot checks, which is a short list.
///
/// What it does is the same idempotent work every read does — draw today's
/// check if there is none, ask the ones whose moment has come, settle the ones
/// whose time is up — so a pass that runs twice, or late, or after an outage,
/// does nothing wrong: a check whose three minutes passed while nothing ran is
/// voided, never asked late.
///
/// It does not run in <c>AutomatedTest</c>, like the other workers; tests call
/// <see cref="RunOnceAsync"/>.
/// </remarks>
public sealed class SpotCheckWorker(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<SpotCheckWorker> logger) : BackgroundService
{
    /// <summary>How often to look: well inside the three minutes a check is open for.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue<int?>("Q2:SpotChecks:IntervalSeconds") is { } seconds and > 0
            ? TimeSpan.FromSeconds(seconds)
            : DefaultInterval;

        using var timer = new PeriodicTimer(interval, timeProvider);

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
                // The next pass does the same work; nothing is lost by waiting.
                logger.LogError(exception, "Spot checks failed; the next pass will pick them up");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass. Returns how many goals changed, which is what the tests assert on.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();
        var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var timeZones = scope.ServiceProvider.GetRequiredService<TimeZoneResolver>();
        var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();
        var now = timeProvider.GetUtcNow();

        var goals = await database.Goals
            .Include(goal => goal.Participants)
            .Include(goal => goal.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Votes)
            .Include(goal => goal.Instances)
            .ThenInclude(instance => instance.UseLog)
            .Include(goal => goal.Instances)
            .ThenInclude(instance => instance.SpotChecks)
            .Include(goal => goal.Pauses)
            .Where(goal => goal.Status == GoalStatus.Active
                && goal.Kind == GoalKind.Dont
                && goal.ProofMode == DontProofMode.SpotCheck)
            .ToListAsync(cancellationToken);

        if (goals.Count == 0)
        {
            return 0;
        }

        var ownerIds = goals.Select(goal => goal.OwnerPersonId).Distinct().ToList();

        // Tracked with their days, for the same reason as the maintenance pass:
        // a believed vote settled here counts a day towards the owner's flame.
        var owners = await database.People
            .Include(person => person.CheckIns)
            .Where(person => ownerIds.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);

        // Quiet hours decide when a check may be drawn at all.
        var preferences = await database.UserSettings
            .AsNoTracking()
            .Where(settings => ownerIds.Contains(settings.PersonId))
            .ToDictionaryAsync(settings => settings.PersonId, settings => settings.Notifications, cancellationToken);

        var changed = 0;

        foreach (var goal in goals)
        {
            var owner = owners.GetValueOrDefault(goal.OwnerPersonId);
            var calendar = timeZones.For(owner?.TimeZoneId);

            // Brought up to date first — a window that rolled over at midnight
            // is the one today's check belongs to — then drawn, then asked.
            var moved = GoalMaintenance.Advance(goal, calendar, now, ids, owner);
            moved |= SpotChecks.Draw(goal, calendar, preferences.GetValueOrDefault(goal.OwnerPersonId) ?? NotificationPreferences.Default, now, ids);
            moved |= await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, ids, owner, cancellationToken);

            if (moved)
            {
                changed++;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await notifier.FlushAsync(cancellationToken);

        if (changed > 0)
        {
            // How many, and nothing about whose.
            logger.LogInformation("Spot checks moved on {GoalCount} goal(s)", changed);
        }

        return changed;
    }
}
