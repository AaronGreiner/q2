using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.People;

/// <summary>
/// Getting away from somebody, and coming back from it.
/// </summary>
/// <remarks>
/// Three rules, and each one is the difference between a working block and a
/// button that looks like one:
///
/// 1. **It ends the friendship.** Not "sets it to blocked" — the row goes. A
///    friendship that survives a block is a friendship that reappears the
///    moment anything forgets to check, and every read in this API that asks
///    "are we friends" would need a second branch for a row meaning the
///    opposite (<see cref="Block"/>).
/// 2. **It works in both directions**, through <see cref="BlockList"/>. The person
///    blocked stops seeing the person who blocked them too.
/// 3. **It is never announced.** Nothing tells the blocked person, and
///    everything they try answers the way it would for a stranger — 404, not
///    403. A refusal that names the reason is a notification.
///
/// What it deliberately does <em>not</em> do is delete anything. Conversations
/// are hidden rather than removed, so lifting a block puts the thread back
/// where it was; erasing somebody's messages is what deleting an account does
/// (<see cref="Q2.Api.Features.Accounts.AccountService"/>), and it is not a
/// decision one person gets to make about another's copy of a shared history.
/// </remarks>
public sealed class BlockService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    BlockList blockList,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<BlockService> logger)
{
    /// <summary>
    /// Blocks somebody, and ends whatever connection there was.
    /// </summary>
    /// <remarks>
    /// Idempotent: blocking somebody already blocked changes nothing and
    /// succeeds, because a client that lost the response should be able to
    /// press it again.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such person.</exception>
    /// <exception cref="DomainValidationException">It is you.</exception>
    public async Task<IReadOnlyList<PersonSummary>> BlockAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        if (personId == me.Id)
        {
            throw new DomainValidationException("PersonId", "You cannot block yourself.");
        }

        var exists = await database.People
            .AsNoTracking()
            .AnyAsync(person => person.Id == personId, cancellationToken);

        if (!exists)
        {
            throw new ResourceNotFoundException("Person", personId);
        }

        var now = timeProvider.GetUtcNow();

        var already = await database.Blocks.SingleOrDefaultAsync(
            block => block.BlockerPersonId == me.Id && block.BlockedPersonId == personId,
            cancellationToken);

        if (already is null)
        {
            database.Blocks.Add(Block.Create(idGenerator.NewId(), me.Id, personId, now));
        }

        /*
         * The friendship goes, in whatever state it was in.
         *
         * Including a request either of them had sent: leaving a pending
         * request alive would put the blocked person back on the other's
         * friends screen as something to answer.
         */
        var connection = await database.Friendships.SingleOrDefaultAsync(
            friendship => (friendship.RequesterId == me.Id && friendship.AddresseeId == personId)
                || (friendship.RequesterId == personId && friendship.AddresseeId == me.Id),
            cancellationToken);

        if (connection is not null)
        {
            database.Friendships.Remove(connection);
        }

        await database.SaveChangesAsync(cancellationToken);

        // No names, and not even which direction: that somebody blocked
        // somebody is the most sensitive thing this feature knows.
        logger.LogInformation("A person was blocked");

        return await ListAsync(cancellationToken);
    }

    /// <summary>
    /// Lifts a block. Only the person who set it can.
    /// </summary>
    /// <remarks>
    /// The friendship does not come back. It was ended, not suspended, and
    /// restoring it would mean deciding on the other person's behalf that they
    /// still want it.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">You have not blocked that person.</exception>
    public async Task<IReadOnlyList<PersonSummary>> UnblockAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        // Only rows this person set. Somebody who has been blocked must not be
        // able to unblock themselves, which is the reason Block stores a
        // direction at all.
        var block = await database.Blocks.SingleOrDefaultAsync(
            candidate => candidate.BlockerPersonId == me.Id && candidate.BlockedPersonId == personId,
            cancellationToken)
            ?? throw new ResourceNotFoundException("Block", personId);

        database.Blocks.Remove(block);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("A block was lifted");

        return await ListAsync(cancellationToken);
    }

    /// <summary>
    /// The people this person has blocked, by name.
    /// </summary>
    /// <remarks>
    /// **Only the ones they blocked**, never the ones who blocked them. The
    /// second list would be the announcement this feature is built to avoid,
    /// and it is not information anybody is entitled to about somebody who has
    /// walked away from them.
    /// </remarks>
    public async Task<IReadOnlyList<PersonSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var blocked = await database.Blocks
            .AsNoTracking()
            .Where(block => block.BlockerPersonId == me.Id)
            .Select(block => block.BlockedPersonId)
            .ToListAsync(cancellationToken);

        if (blocked.Count == 0)
        {
            return [];
        }

        var people = await database.People
            .AsNoTracking()
            .Where(person => blocked.Contains(person.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. people
                .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(person => PersonSummary.From(person, now)),
        ];
    }

    /// <summary>
    /// Refuses when the other person is hidden, the way a stranger's request
    /// for something that does not exist is refused.
    /// </summary>
    /// <remarks>
    /// The one line every feature that can reach a person calls, so "a block is
    /// a 404 and never a 403" is written once. A 403 would confirm both that
    /// the person exists and that they did something about you.
    /// </remarks>
    public async Task GuardAsync(Guid personId, string resource, CancellationToken cancellationToken)
    {
        if (await blockList.IsHiddenFromMeAsync(personId, cancellationToken))
        {
            throw new ResourceNotFoundException(resource, personId);
        }
    }
}
