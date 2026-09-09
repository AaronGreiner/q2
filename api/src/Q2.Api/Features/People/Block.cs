using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.People;

/// <summary>
/// One person has decided not to be reachable by another.
/// </summary>
/// <remarks>
/// **An entity of its own rather than a status on <see cref="Friendship"/>**,
/// and that is the decision this type exists to record. Blocking somebody
/// *ends* the friendship; it is not a state the friendship can be in. Modelled
/// as a status, "blocked" would have to answer questions that make no sense —
/// who requested it, when it was accepted — and every query that asks "are we
/// friends" would need a second branch for a row that means the opposite.
///
/// **It works in both directions.** The row records who pressed the button, but
/// what follows from it does not: whoever was blocked no longer sees the person
/// who blocked them either. A one-way block would leave the person who acted
/// visible to the one they were getting away from, which is the half that
/// matters. <see cref="BlockList.HiddenFromAsync"/> is the one place that turns a
/// row into that symmetric answer.
///
/// **Only the person who set it can lift it**, which is why the direction is
/// stored at all. Somebody who has been blocked must not be able to unblock
/// themselves, and without the direction there would be nothing to check.
/// </remarks>
public sealed class Block
{
    // EF Core materialisation only.
    private Block()
    {
    }

    private Block(Guid id, Guid blockerPersonId, Guid blockedPersonId, DateTimeOffset createdAt)
    {
        Id = id;
        BlockerPersonId = blockerPersonId;
        BlockedPersonId = blockedPersonId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Who pressed the button. The only person who may take it back.</summary>
    public Guid BlockerPersonId { get; private set; }

    /// <summary>Who is blocked. Never told about it — see <see cref="BlockService"/>.</summary>
    public Guid BlockedPersonId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <exception cref="DomainValidationException">The two ends are the same person.</exception>
    public static Block Create(Guid id, Guid blockerPersonId, Guid blockedPersonId, DateTimeOffset createdAt)
    {
        if (blockerPersonId == blockedPersonId)
        {
            throw new DomainValidationException(nameof(BlockedPersonId), "You cannot block yourself.");
        }

        if (blockerPersonId == Guid.Empty || blockedPersonId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(BlockedPersonId), "A block needs two people.");
        }

        return new Block(id, blockerPersonId, blockedPersonId, createdAt);
    }

    /// <summary>The other end of this block, seen from <paramref name="personId"/>.</summary>
    public Guid OtherThan(Guid personId) =>
        personId == BlockerPersonId ? BlockedPersonId : BlockerPersonId;

    /// <summary>Whether this row stands between these two people, either way round.</summary>
    public bool Between(Guid oneId, Guid otherId) =>
        (BlockerPersonId == oneId && BlockedPersonId == otherId)
        || (BlockerPersonId == otherId && BlockedPersonId == oneId);
}
