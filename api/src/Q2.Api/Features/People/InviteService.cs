using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.People;

/// <summary>Somebody's invite code, for the client to build a link out of.</summary>
/// <remarks>
/// The code only. The server does not know which host the app is served from,
/// and a link with the wrong origin in it is worse than no link at all.
/// </remarks>
public sealed record InviteResponse(string Code);

/// <summary>
/// The way into q2 for somebody who does not know anybody here.
/// </summary>
/// <remarks>
/// This is the smaller half of the answer to the gap both projects had: a new
/// account has no friends, and almost everything in q2 is something you do
/// where friends can see. The daily challenge
/// ([0021](../../../../docs/adr/0021-daily-challenge.md)) is what there is to
/// *do* on the first day; an invite link is how the first friend arrives.
///
/// **Redeeming a link makes a friendship rather than a request**, and that is
/// deliberate. The person who sent the link has already said yes by sending it,
/// and a pending request neither of them can act on until one of them opens the
/// app is exactly the dead end this feature exists to remove. It is safe
/// precisely because the code is unguessable: the only way to arrive with one
/// is to have been given it.
/// </remarks>
public sealed class InviteService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<InviteService> logger)
{
    /// <summary>This person's code, making one the first time it is asked for.</summary>
    public async Task<InviteResponse> GetAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        if (me.InviteCode is { Length: > 0 } existing)
        {
            return new InviteResponse(existing);
        }

        return new InviteResponse(await IssueAsync(me, cancellationToken));
    }

    /// <summary>
    /// Replaces the code, which is what makes a leaked link recoverable.
    /// </summary>
    /// <remarks>
    /// The friendships already made through the old link stay. They were real
    /// invitations that were accepted; revoking a link is about who can still
    /// use it, not about undoing who already did.
    /// </remarks>
    public async Task<InviteResponse> RegenerateAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        logger.LogInformation("An invite link was replaced");

        return new InviteResponse(await IssueAsync(me, cancellationToken));
    }

    /// <summary>
    /// Turns a code into a friendship with whoever owns it.
    /// </summary>
    /// <remarks>
    /// Called during registration, inside the same transaction that creates the
    /// person — so somebody who arrives through a link is never left as an
    /// account with nobody, which is the state the link was meant to prevent.
    ///
    /// A code that means nothing is ignored rather than refused. Registration
    /// is the worst possible moment to fail over a stale link somebody was
    /// forwarded: the account is what they came for, and the friendship is the
    /// bonus.
    /// </remarks>
    public async Task<bool> RedeemAsync(string? code, Person newcomer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newcomer);

        var trimmed = code?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var host = await database.People
            .AsNoTracking()
            .SingleOrDefaultAsync(person => person.InviteCode == trimmed, cancellationToken);

        if (host is null || host.Id == newcomer.Id)
        {
            logger.LogInformation("An invite code was presented that no longer means anything");
            return false;
        }

        // Accepted, not pending: whoever sent the link has already said yes.
        var friendship = Friendship.Request(idGenerator.NewId(), host.Id, newcomer.Id, timeProvider.GetUtcNow());
        friendship.Accept(newcomer.Id, timeProvider.GetUtcNow());

        database.Friendships.Add(friendship);

        logger.LogInformation("An invite link was redeemed");
        return true;
    }

    /// <summary>
    /// Writes a fresh code, retrying if the database says it is taken.
    /// </summary>
    /// <remarks>
    /// A collision at 96 bits does not happen; a unique index that can throw
    /// eventually does. The loop is two lines and removes the question.
    /// </remarks>
    private async Task<string> IssueAsync(Person person, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var code = Invites.NewCode();

            var taken = await database.People
                .AsNoTracking()
                .AnyAsync(candidate => candidate.InviteCode == code, cancellationToken);

            if (!taken)
            {
                person.SetInviteCode(code);
                await database.SaveChangesAsync(cancellationToken);
                return code;
            }

            if (attempt >= Invites.Attempts)
            {
                throw new InvalidOperationException(
                    "Could not find an unused invite code, which should not be possible.");
            }
        }
    }
}
