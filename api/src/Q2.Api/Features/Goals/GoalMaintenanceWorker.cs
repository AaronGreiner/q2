using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
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
/// yesterday even if you have not opened q2 since, the evening warning goes out
/// to people who are not the one who is running late, and a photograph whose
/// twelve hours ran out overnight is decided — and its owner told — without
/// anybody looking.
///
/// It also opens the conversation of any shared goal that has none — goals
/// from before a goal came with one. Here because this is the pass that runs
/// once at startup and then on its own, so an existing database catches up
/// without anybody running anything (<see cref="GoalConversations.OpenMissingAsync"/>).
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
/// they test; those tests call <see cref="RunOnceAsync"/> directly instead.
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
        var friends = scope.ServiceProvider.GetRequiredService<FriendsService>();

        /*
         * Everything this pass has to tell anybody is staged beside the rows it
         * changes and delivered once the last batch is saved.
         *
         * Never from inside the loop: that would put a network call in the
         * middle of a database transaction, held open for as long as somebody
         * else's push service feels like taking (docs/adr/0023-web-push.md).
         */
        var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();

        var now = timeProvider.GetUtcNow();
        var changed = 0;
        var warned = 0;
        var lastId = Guid.Empty;

        var opened = await GoalConversations.OpenMissingAsync(database, ids, now, cancellationToken);

        if (opened > 0)
        {
            await database.SaveChangesAsync(cancellationToken);

            // How many, and nothing about whose.
            logger.LogInformation("Goal maintenance opened {ConversationCount} missing goal conversation(s)", opened);
        }

        while (true)
        {
            // Keyed pagination rather than Skip/Take: the set being paged is
            // the set being written to, and an offset would step over a goal
            // whose predecessor moved out from under it.
            var goals = await database.Goals

                // The people who vote on it, who hear when a vote they were
                // part of has closed.
                .Include(g => g.Participants)
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

                if (await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, ids, owner, cancellationToken))
                {
                    changed++;
                }

                if (owner is not null && WarnIfAtRisk(goal, calendar, now) is { } missing)
                {
                    warned++;

                    /*
                     * The recipients are the owner's friends — the audience
                     * the warning has always had
                     * (docs/adr/0019-warning-and-balance.md) — and never the
                     * owner: the one person who does not need telling that they
                     * are running out of time is the person running out of
                     * time.
                     *
                     * A line in each friend's bell now, where the feed used to
                     * carry it (docs/adr/0024-one-notification-pipeline.md).
                     * About the goal, so deleting it takes the line with it;
                     * naming the owner, which is where tapping it leads.
                     */
                    await notifier.StageAsync(
                        new NotificationEvent(
                            NotificationKind.FriendWindowAtRisk,
                            owner.Id,
                            NotificationTarget.Goal,
                            goal.Id,
                            goal.Title,
                            missing),
                        await friends.FriendIdsAsync(owner.Id, cancellationToken),
                        cancellationToken);
                }
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        if (changed > 0)
        {
            // No title and no person: how many, and nothing about whose.
            logger.LogInformation("Goal maintenance advanced {GoalCount} goal(s)", changed);
        }

        if (warned > 0)
        {
            logger.LogInformation("Goal maintenance warned about {WindowCount} window(s)", warned);
        }

        await notifier.FlushAsync(cancellationToken);

        return changed;
    }

    /// <summary>
    /// Whether a goal's friends are to be warned about its window — at most
    /// once per window. Returns how many proofs are still missing, or null.
    /// </summary>
    /// <remarks>
    /// Here rather than inside <see cref="GoalMaintenance"/>, and the split is
    /// deliberate: advancing a goal is a pure function over the goal, while a
    /// warning is something written for other people to read. Keeping the pure
    /// part pure is what lets the window arithmetic be tested without a
    /// database.
    ///
    /// It runs only in the background pass, never on a read. A warning is for
    /// the person's *friends*, so tying it to the owner opening the app would
    /// mean the one person whose attention is not in question decides whether
    /// anybody else hears about it.
    /// </remarks>
    private static int? WarnIfAtRisk(Goal goal, LocalCalendar calendar, DateTimeOffset now) =>
        goal.CurrentInstance is { } window
        && GoalRisk.Assess(window, calendar, now) is { } risk
        && window.NotifyRisk(now)
            ? risk.MissingProofs
            : null;

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
