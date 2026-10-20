using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Chats;

/// <summary>
/// Reads what hangs off a goal's windows — photographs with their votes and
/// reactions, logged uses, spot checks — for the windows a read needs, rather
/// than for every window the goal ever had (#127).
/// </summary>
/// <remarks>
/// A goal's conversation and the chat list read the goal itself whole: one
/// narrow row per window, because the streak each kept window carries and the
/// balance are counted over the whole chain, and they stay derived rather than
/// stored (AGENTS.md section 4). What grows with every day is what hangs off
/// those rows, and a page of the thread shows thirty lines of it at most.
///
/// **Why a page is still exact.** Everything a window's details produce in the
/// timeline — a photograph delivered, a use logged, a relapse with its count —
/// happens while the window is open, so no later than its deadline. Once the
/// details of the newest windows are read, every line that could still be
/// missing is at or before the newest deadline among the windows not read
/// (<see cref="Horizon"/>). A page whose oldest line is later than that is
/// therefore the page a whole read would have cut; one that reaches further
/// back reads the next windows and is cut again. Opening a thread or the list
/// reads a page's worth; scrolling back to a goal's first day reads all of it,
/// as before.
///
/// Tracked, so that each detail lands in the window the goal already holds:
/// the goal is read tracked for the same reason, and nothing here is changed or
/// saved.
/// </remarks>
public sealed class GoalWindowDetails(Q2DbContext database)
{
    private readonly HashSet<Guid> _read = [];

    /// <summary>Reads the details of these windows, those not read already.</summary>
    public async Task ReadAsync(IEnumerable<GoalInstance> windows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var ids = windows.Select(window => window.Id).Where(_read.Add).ToList();

        if (ids.Count == 0)
        {
            return;
        }

        await database.ProofPhotos
            .Include(proof => proof.Votes)
            .Include(proof => proof.Reactions)
            .Where(proof => ids.Contains(proof.GoalInstanceId))
            .LoadAsync(cancellationToken);

        await database.Set<DontUse>()
            .Where(use => ids.Contains(use.GoalInstanceId))
            .LoadAsync(cancellationToken);

        await database.Set<SpotCheck>()
            .Where(check => ids.Contains(check.GoalInstanceId))
            .LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the details of the newest <paramref name="count"/> windows of
    /// <paramref name="goal"/> not read yet that began by <paramref name="by"/>.
    /// </summary>
    public Task ReadNewestAsync(Goal goal, int count, DateTimeOffset? by, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return ReadAsync(
            Unread(goal, by)
                .OrderByDescending(window => window.DueAt)
                .ThenByDescending(window => window.Id)
                .Take(count),
            cancellationToken);
    }

    /// <summary>
    /// The latest moment a line from an unread window's details can have, among
    /// the windows that began by <paramref name="by"/>; null when all of them
    /// have been read.
    /// </summary>
    public DateTimeOffset? Horizon(Goal goal, DateTimeOffset? by)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return Unread(goal, by).Select(window => (DateTimeOffset?)window.DueAt).Max();
    }

    /// <remarks>
    /// "Began by" includes the instant itself: a line at exactly a page's
    /// boundary can still fall before it.
    /// </remarks>
    private IEnumerable<GoalInstance> Unread(Goal goal, DateTimeOffset? by) =>
        goal.Instances.Where(window => !_read.Contains(window.Id) && (by is not { } end || window.StartsAt <= end));
}
