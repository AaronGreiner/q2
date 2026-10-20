using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Challenges;

/// <summary>
/// Keeps a week of prompts queued ahead of time.
/// </summary>
/// <remarks>
/// The whole answer to "who writes the challenge every morning?" — nobody
/// does. The prompts are configuration (<see cref="ChallengeOptions"/>), this
/// writes the next few days' rows from them, and a day becomes the current
/// challenge simply because the clock reached it.
///
/// Like <see cref="Q2.Api.Features.Goals.GoalMaintenanceWorker"/> it holds no
/// state of its own and two runs are one run: it asks which of the coming days
/// already have a row and writes only the others. A host that was down for a
/// week comes back and fills the gap in one pass, and the prompt each day gets
/// does not depend on how many passes there have been
/// (<see cref="ChallengeQueue.PromptFor"/>).
///
/// It does <b>not</b> run in <c>AutomatedTest</c>, for the same reason the goal
/// job does not: a job writing between an arrange and an assert makes tests
/// flaky for a reason unrelated to what they check. Those tests seed a
/// challenge, or call <see cref="RunOnceAsync"/> themselves.
/// </remarks>
public sealed class ChallengeQueueWorker(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IOptions<ChallengeOptions> options,
    ILogger<ChallengeQueueWorker> logger) : BackgroundService
{
    /// <summary>
    /// How often to look.
    /// </summary>
    /// <remarks>
    /// Every minute, and there is no setting for it. The queue is a week deep
    /// and would be served by an hourly pass; the announcement is not. A
    /// challenge appears at a moment of the day, the same for everybody
    /// (#136), and its push has to go out then rather than up to an hour
    /// later. A pass is two small reads, so a minute costs nothing.
    /// </remarks>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        // Once at startup: the most likely reason the queue is empty is that
        // this process was not running when it should have been filled.
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await AnnounceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Includes the unique index on the day, which is what a second
                // host filling the same queue would hit. Losing the race is not
                // a failure — the row it wanted is there — and the next pass
                // finds nothing left to do.
                logger.LogError(exception, "Filling the challenge queue failed; the next pass will pick it up");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass. Returns how many challenges were queued, which is what the
    /// tests assert on.
    /// </summary>
    /// <remarks>
    /// Announcing is part of the same pass rather than a job of its own,
    /// because it is the same question asked an hour later: this fills the
    /// queue days ahead, and tells people about the one that has since become
    /// today's.
    /// </remarks>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.Prompts.Length == 0)
        {
            // No prompts configured is "no daily challenge", not a failure: the
            // room reports that nothing is running and no screen breaks.
            return 0;
        }

        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();
        var ids = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var timeZones = scope.ServiceProvider.GetRequiredService<TimeZoneResolver>();

        // The deployment's zone, not a person's. A challenge is one row for
        // everybody and turns over at one moment — see Challenge.
        var calendar = timeZones.For(timeZones.ConfiguredZoneId);
        var today = calendar.Today(timeProvider.GetUtcNow());
        var horizon = Math.Max(1, settings.HorizonDays);
        var last = today.AddDays(horizon - 1);

        var queued = await database.Challenges
            .AsNoTracking()
            .Where(challenge => challenge.Day >= today && challenge.Day <= last)
            .Select(challenge => challenge.Day)
            .ToListAsync(cancellationToken);

        var planned = ChallengeQueue.Plan(settings.Prompts, queued, today, horizon, calendar);

        foreach (var item in planned)
        {
            database.Challenges.Add(Challenge.Create(
                ids.NewId(),
                item.Day,
                item.Prompt.German,
                item.Prompt.English,
                item.PublishedAt,
                item.ExpiresAt,
                item.MomentEndsAt));
        }

        var translated = await TranslateEarlierRowsAsync(database, settings.Prompts, cancellationToken);

        if (planned.Count == 0 && translated == 0)
        {
            return 0;
        }

        await database.SaveChangesAsync(cancellationToken);

        // The counts, not the prompts: they are authored text, and a log line
        // is read by more people than wrote them.
        logger.LogInformation(
            "Queued {ChallengeCount} challenge(s); gave {TranslatedCount} earlier one(s) their English prompt",
            planned.Count,
            translated);

        return planned.Count;
    }

    /// <summary>
    /// Gives the rows written before prompts came in two languages their
    /// English one, wherever the configuration still holds the German prompt
    /// they were written from (#105). Returns how many it gave one to.
    /// </summary>
    /// <remarks>
    /// The past ones too: the archive shows every prompt somebody answered.
    /// A row whose prompt has since left the configuration keeps reading in
    /// German, and that is the honest answer — nobody wrote its English.
    /// Idempotent like the rest of the pass: a row that has its English is
    /// not asked again.
    /// </remarks>
    private static async Task<int> TranslateEarlierRowsAsync(
        Q2DbContext database,
        IReadOnlyList<ChallengePrompt> prompts,
        CancellationToken cancellationToken)
    {
        var untranslated = await database.Challenges
            .Where(challenge => challenge.PromptEnglish == null)
            .ToListAsync(cancellationToken);

        var translated = 0;

        foreach (var challenge in untranslated)
        {
            var pair = prompts.FirstOrDefault(prompt => prompt.German.Trim() == challenge.Prompt);

            if (pair is not null && challenge.GiveEnglish(pair.English))
            {
                translated++;
            }
        }

        return translated;
    }

    /// <summary>
    /// Tells everybody about today's challenge, once. Returns 1 when this call
    /// announced it and 0 when there was nothing left to announce.
    /// </summary>
    /// <remarks>
    /// Separate from the queue pass above and called after it, so the network
    /// call is never inside the transaction that wrote the rows.
    ///
    /// **Everybody with a device**, which is the honest recipient set for
    /// something that is deliberately the same for all of them — and the one
    /// place in q2 where a notification is not scoped to somebody's friends.
    /// It is also the one that would not scale: a deployment with a hundred
    /// thousand people would want this fanned out rather than sent from one
    /// hourly pass, and that is a different design rather than a bigger loop.
    /// </remarks>
    public async Task<int> AnnounceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();

        var now = timeProvider.GetUtcNow();

        var current = await database.Challenges
            .Where(challenge => challenge.PublishedAt <= now
                && challenge.ExpiresAt > now
                && challenge.AnnouncedAt == null)
            .OrderByDescending(challenge => challenge.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null || !current.Announce(now))
        {
            return 0;
        }

        // Marked first and saved before anything is sent: a pass that fell over
        // between sending and saving would announce the same prompt again on
        // the hour, and a second notification is worse than a missing one.
        await database.SaveChangesAsync(cancellationToken);

        // Through the same pipeline as everything else, with the one
        // difference that is the challenge's whole character: it is addressed
        // to nobody in particular.
        notifier.StageForEveryone(new NotificationEvent(
            NotificationKind.ChallengePublished,
            ActorPersonId: null,
            NotificationTarget.Challenge,
            current.Id,
            current.Prompt,
            EnglishSubject: current.PromptEnglish));

        await notifier.FlushAsync(cancellationToken);

        return 1;
    }
}
