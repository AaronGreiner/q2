using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// The bell: what happened to you, newest first.
/// </summary>
/// <remarks>
/// **Opening it is seeing it.** The list is read and the person's seen-marker
/// moves in the same call, the way opening a conversation marks it read — a
/// separate "mark as seen" request is one a client could forget, leaving a
/// number on a bell somebody is looking into.
///
/// What is new is decided against the marker as it stood *before* this read, so
/// the lines that brought somebody here are still drawn as new on the screen
/// that just cleared them.
/// </remarks>
public sealed class InboxService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    BlockList blockList,
    Notifier notifier,
    TimeProvider timeProvider)
{
    /// <summary>How many rows the bell reads. Thirty days of anything is less than this.</summary>
    public const int Limit = 100;

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var seenBefore = me.NotificationsSeenAt ?? DateTimeOffset.MinValue;

        var rows = await RecentAsync(database, me.Id, now, cancellationToken);

        /*
         * Somebody hidden by a block is hidden here too, including what they
         * did before it. Filtered on read rather than deleted, so lifting the
         * block puts the history back — the same choice a direct chat makes
         * (docs/adr/0022-blocking-reporting-and-erasure.md).
         */
        var hidden = await blockList.HiddenFromMeAsync(cancellationToken);
        var visible = rows.Where(row => row.ActorPersonId is not { } actor || !hidden.Contains(actor)).ToList();

        var lines = InboxLines.Group(visible, seenBefore);
        var actors = await LoadActorsAsync(lines, cancellationToken);

        var responses = lines
            .Select(line => NotificationResponse.ForBell(
                line.Latest,
                line.Latest.ActorPersonId is { } actorId && actors.TryGetValue(actorId, out var actor)
                    ? PersonSummary.From(actor, now)
                    : null,
                line.Latest.Kind == NotificationKind.ReactionReceived ? line.People.Count : line.Latest.Amount,
                line.IsNew))
            .ToList();

        me.MarkNotificationsSeen(now);
        await database.SaveChangesAsync(cancellationToken);

        /*
         * The badge on every other device of this person goes to zero too.
         *
         * The numbers only, and deliberately no "the bell changed": a bell
         * that is open somewhere would read itself again on hearing that, and
         * reading it is what sends it — a loop with a request in every turn.
         */
        notifier.Touch([me.Id]);
        await notifier.FlushAsync(cancellationToken);

        return responses;
    }

    /// <summary>
    /// Takes one line out of the bell for good.
    /// </summary>
    /// <remarks>
    /// A line of reactions is every reaction to the same thing, so removing it
    /// removes all of them up to the one it was drawn from — leaving the older
    /// ones would bring the line straight back with "und 3 weitere" shrunk by
    /// one. A line that is already gone, or was never this person's, is not an
    /// error: the answer is the same either way, and it says nothing about
    /// anybody else's bell.
    /// </remarks>
    public async Task DismissAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var line = await database.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id && row.RecipientPersonId == me.Id, cancellationToken);

        if (line is null)
        {
            return;
        }

        var rows = database.Notifications.Where(row => row.RecipientPersonId == me.Id);

        await (line.Kind == NotificationKind.ReactionReceived
                ? rows.Where(row => row.Kind == NotificationKind.ReactionReceived
                    && row.Target == line.Target
                    && row.TargetId == line.TargetId
                    && row.OccurredAt <= line.OccurredAt)
                : rows.Where(row => row.Id == line.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Empties the bell of everything up to <paramref name="until"/>.
    /// </summary>
    /// <remarks>
    /// Bounded by the newest line the person was looking at rather than by
    /// "now", so something that arrived while they were reading is not thrown
    /// away unseen. Somebody hidden by a block is left alone: those lines were
    /// not on the screen, and lifting the block is meant to bring them back.
    /// </remarks>
    public async Task ClearAsync(DateTimeOffset until, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var hidden = (await blockList.HiddenFromMeAsync(cancellationToken)).ToList();

        await database.Notifications
            .Where(row => row.RecipientPersonId == me.Id
                && row.OccurredAt <= until
                && (row.ActorPersonId == null || !hidden.Contains(row.ActorPersonId.Value)))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// The rows still inside the bell's thirty days, newest first.
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="CountsService"/>, so the number on the bell and
    /// the lines behind it are counted from the same rows by the same rule.
    /// </remarks>
    internal static async Task<List<Notification>> RecentAsync(
        Q2DbContext database,
        Guid personId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var since = now - Notification.KeptFor;

        return await database.Notifications
            .AsNoTracking()
            .Where(row => row.RecipientPersonId == personId && row.OccurredAt > since)
            .OrderByDescending(row => row.OccurredAt)
            .ThenByDescending(row => row.Id)
            .Take(Limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Person>> LoadActorsAsync(
        IReadOnlyCollection<InboxLines.Line> lines,
        CancellationToken cancellationToken)
    {
        var ids = lines
            .Select(line => line.Latest.ActorPersonId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await database.People
            .AsNoTracking()
            .Where(person => ids.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);
    }
}

/// <summary>
/// How the bell turns rows into lines.
/// </summary>
/// <remarks>
/// A row is one fact — one reaction by one person — and a line is what is worth
/// reading. Five people reacting to the same photograph is one line, "Lena und
/// 4 weitere", not five; everything else is a line of its own.
///
/// New and seen are never merged into one line: "und 4 weitere" must not count
/// people somebody has already been told about as if they were news.
///
/// Pure, so the grouping is tested without a database.
/// </remarks>
public static class InboxLines
{
    /// <param name="Latest">The newest row of the line, whose actor is named.</param>
    /// <param name="People">Everybody behind the line, the newest first.</param>
    /// <param name="IsNew">Whether it arrived after the bell was last opened.</param>
    public sealed record Line(Notification Latest, IReadOnlyList<Guid> People, bool IsNew);

    /// <param name="newestFirst">Rows in the order the bell shows them.</param>
    /// <param name="seenBefore">When the bell was last opened.</param>
    public static IReadOnlyList<Line> Group(IEnumerable<Notification> newestFirst, DateTimeOffset seenBefore)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);

        var lines = new List<(Notification Latest, List<Guid> People, bool IsNew)>();
        var reactions = new Dictionary<(NotificationTarget Target, Guid? TargetId, bool IsNew), int>();

        foreach (var row in newestFirst)
        {
            var isNew = row.OccurredAt > seenBefore;

            if (row.Kind == NotificationKind.ReactionReceived)
            {
                var key = (row.Target, row.TargetId, isNew);

                if (reactions.TryGetValue(key, out var index))
                {
                    if (row.ActorPersonId is { } actor && !lines[index].People.Contains(actor))
                    {
                        lines[index].People.Add(actor);
                    }

                    continue;
                }

                reactions[key] = lines.Count;
            }

            lines.Add((row, row.ActorPersonId is { } first ? [first] : [], isNew));
        }

        return [.. lines.Select(line => new Line(line.Latest, line.People, line.IsNew))];
    }
}
