using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Runs <see cref="GoalMaintenance"/> for everybody, on a timer.
/// </summary>
/// <remarks>
/// The read path already brings a person's own goals up to date when they open
/// the app, so this is not what keeps <em>their</em> screen honest. It is what
/// keeps everybody else's: a friend has to be able to see that you missed
/// yesterday even if you have not opened q2 since, and from stage 5 the evening
/// warning goes out to people who are not the one who is running late.
///
/// Deliberately simple. It is not a scheduler, it has no queue and it holds no
/// state of its own: every run asks the same question — "is any goal behind?" —
/// and the answer is in the goals themselves. Two runs of it are one run
/// (<see cref="GoalMaintenance"/>), so a restart at the wrong moment costs
/// nothing.
///
/// It does <b>not</b> run in <c>AutomatedTest</c>. Integration tests assert on
/// exact rows, and a job mutating the database between the arrange and the
/// assert would make them flaky for a reason that has nothing to do with what
/// they test; those tests call <see cref="GoalMaintenance"/> directly instead.
/// </remarks>
public sealed class GoalMaintenanceWorker(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<GoalMaintenanceWorker> logger) : BackgroundService
{
    /// <summary>
    /// How often to look. Every ten minutes is far more often than a deadline
    /// can move, and infrequent enough to be invisible.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);

    /// <summary>How many goals one pass loads at a time.</summary>
    public const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue<int?>("Q2:Maintenance:IntervalMinutes") is { } minutes and > 0
            ? TimeSpan.FromMinutes(minutes)
            : DefaultInterval;

        // Once at startup, because the most likely reason a deadline is
        // unprocessed is that the process was not running when it passed.
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
                // A failed pass is not a reason to stop passing: the next one
                // does the same work, because the work is idempotent.
                logger.LogError(exception, "Goal maintenance failed; the next pass will pick it up");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass over every goal that could have moved. Returns how many
    /// changed, which is what the tests assert on.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();
        var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var timeZones = scope.ServiceProvider.GetRequiredService<TimeZoneResolver>();
        var recorder = scope.ServiceProvider.GetRequiredService<ActivityRecorder>();

        var now = timeProvider.GetUtcNow();
        var changed = 0;
        var lastId = Guid.Empty;

        /*
         * The warnings this pass published, kept until after the save.
         *
         * Sending inside the loop would put a network call in the middle of a
         * database transaction, held open for as long as somebody else's push
         * service feels like taking. Collecting them costs one list.
         */
        var warnings = new List<(Guid OwnerId, string Title, int Missing)>();

        while (true)
        {
            // Keyed pagination rather than Skip/Take: the set being paged is
            // the set being written to, and an offset would step over a goal
            // whose predecessor moved out from under it.
            var goals = await database.Goals
                .Include(g => g.Instances)
                .ThenInclude(instance => instance.Proofs)
                .ThenInclude(proof => proof.Votes)

                // Without the pauses this job cannot see one, and would spend
                // the night missing windows somebody is excused from — the
                // exact thing a pause exists to prevent.
                .Include(g => g.Pauses)
                .Where(g => g.Status == GoalStatus.Active && g.Id.CompareTo(lastId) > 0)
                .OrderBy(g => g.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (goals.Count == 0)
            {
                break;
            }

            lastId = goals[^1].Id;

            var owners = await LoadOwnersAsync(database, goals, cancellationToken);

            foreach (var goal in goals)
            {
                var owner = owners.GetValueOrDefault(goal.OwnerPersonId);
                var calendar = timeZones.For(owner?.TimeZoneId);

                if (GoalMaintenance.Advance(goal, calendar, now, ids, owner))
                {
                    changed++;
                }

                if (owner is not null && WarnIfAtRisk(goal, owner, calendar, now, recorder) is { } warning)
                {
                    warnings.Add(warning);
                }
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        if (changed > 0)
        {
            // No title and no person: how many, and nothing about whose.
            logger.LogInformation("Goal maintenance advanced {GoalCount} goal(s)", changed);
        }

        if (warnings.Count > 0)
        {
            logger.LogInformation("Goal maintenance warned about {WindowCount} window(s)", warnings.Count);
            await NotifyAsync(warnings, cancellationToken);
        }

        return changed;
    }

    /// <summary>
    /// Sends the warnings this pass produced to the people who would have seen
    /// them in their feed.
    /// </summary>
    /// <remarks>
    /// **The recipients are the owner's friends**, which is exactly who the
    /// activity feed shows the same warning to
    /// ([0019](../../../../docs/adr/0019-warning-and-balance.md)). Push is a
    /// delivery route for something that already exists, so a set that differed
    /// from the feed's would mean telling somebody something they cannot then
    /// go and look at — or missing somebody the app has already told.
    ///
    /// Never the owner. The warning is for the people who are watching, and the
    /// one person who does not need telling that they are running out of time
    /// is the person running out of time.
    ///
    /// Failures are swallowed: the pass's real work is committed by now, and a
    /// push service having a bad afternoon must not stop windows from
    /// advancing.
    /// </remarks>
    private async Task NotifyAsync(
        IReadOnlyList<(Guid OwnerId, string Title, int Missing)> warnings,
        CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();

        foreach (var warning in warnings)
        {
            try
            {
                var recipients = await friends.FriendIdsAsync(warning.OwnerId, cancellationToken);

                await notifications.NotifyAsync(
                    recipients,
                    new PushPayload(PushKind.WindowAtRisk, warning.Title, warning.Missing, null),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "A warning could not be delivered as a notification");
            }
        }
    }

    /// <summary>
    /// Tells a goal's friends that its window is about to be missed, at most
    /// once per window. Returns what to notify about, or null when there was
    /// nothing to warn about.
    /// </summary>
    /// <remarks>
    /// Here rather than inside <see cref="GoalMaintenance"/>, and the split is
    /// deliberate: advancing a goal is a pure function over the goal, while a
    /// warning is a row written for other people to read. Keeping the pure part
    /// pure is what lets the window arithmetic be tested without a database.
    ///
    /// It runs only in the background pass, never on a read. A warning is for
    /// the person's *friends*, so tying it to the owner opening the app would
    /// mean the one person whose attention is not in question decides whether
    /// anybody else hears about it.
    /// </remarks>
    private static (Guid OwnerId, string Title, int Missing)? WarnIfAtRisk(
        Goal goal,
        Person owner,
        LocalCalendar calendar,
        DateTimeOffset now,
        ActivityRecorder recorder)
    {
        if (goal.CurrentInstance is not { } window
            || GoalRisk.Assess(window, calendar, now) is not { } risk
            || !window.NotifyRisk(now))
        {
            return null;
        }

        recorder.Publish(
            owner.Id,
            ActivityKind.WindowAtRisk,
            goal.Title,
            risk.MissingProofs,
            now,
            goal.Id);

        return (owner.Id, goal.Title, risk.MissingProofs);
    }

    private static async Task<Dictionary<Guid, Person>> LoadOwnersAsync(
        Q2DbContext database,
        IReadOnlyCollection<Goal> goals,
        CancellationToken cancellationToken)
    {
        var ids = goals.Select(goal => goal.OwnerPersonId).Distinct().ToList();

        /*
         * Tracked, and with their check-ins.
         *
         * A confirmed vote counts a day towards the owner's streak, and a
         * check-in written onto an untracked person is a check-in that is never
         * saved. The days come along for the same reason CurrentPerson loads
         * them: Person.CheckIn on somebody whose days were never read would
         * insert a duplicate and hit the unique index instead of doing nothing.
         */
        return await database.People
            .Include(person => person.CheckIns)
            .Where(person => ids.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);
    }
}
