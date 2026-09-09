using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.People;

/// <summary>
/// Answers "who must this person not see?".
/// </summary>
/// <remarks>
/// The counterpart to <see cref="CurrentPerson"/>, and it exists for the same
/// reason: there is exactly one correct answer to the question, and a second
/// implementation of it would be a second answer. Every read that can put a
/// person on a screen asks this one type.
///
/// **The answer is symmetric.** A <see cref="Block"/> row has a direction —
/// somebody pressed the button and only they can take it back — but what
/// follows from it does not. Whoever was blocked stops seeing the person who
/// blocked them too. A one-way block would leave the person who acted still
/// visible to the one they were getting away from, which is exactly the half
/// that matters; it would also announce the block, because a profile that
/// vanishes from one side only is a profile that has obviously done something.
///
/// Cached for the lifetime of the request, like the current person: a single
/// screen asks this three or four times and the answer cannot change underneath
/// one request.
/// </remarks>
public sealed class BlockList(Q2DbContext database, CurrentPerson currentPerson)
{
    private HashSet<Guid>? _cached;

    /// <summary>
    /// Everybody the signed-in person must not see, in either direction.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> HiddenFromMeAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var meId = await currentPerson.GetIdAsync(cancellationToken);
        _cached = await HiddenFromAsync(database, meId, cancellationToken);
        return _cached;
    }

    /// <summary>Whether these two are hidden from each other.</summary>
    public async Task<bool> IsHiddenFromMeAsync(Guid personId, CancellationToken cancellationToken) =>
        (await HiddenFromMeAsync(cancellationToken)).Contains(personId);

    /// <summary>
    /// Everybody <paramref name="personId"/> must not see.
    /// </summary>
    /// <remarks>
    /// Static and taking its context, so the maintenance job and anything else
    /// without a session can ask the same question about somebody else without
    /// a second copy of the "both directions" rule.
    /// </remarks>
    public static async Task<HashSet<Guid>> HiddenFromAsync(
        Q2DbContext database,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var rows = await database.Blocks
            .AsNoTracking()
            .Where(block => block.BlockerPersonId == personId || block.BlockedPersonId == personId)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(block => block.OtherThan(personId))];
    }
}
