using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
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

/// <summary>The code from somebody's link, in a body rather than a path.</summary>
/// <remarks>
/// Nullable like every request property here, so a missing field is answered
/// by the service (as a code that means nothing) rather than by model binding.
/// </remarks>
public sealed record InviteCodeRequest(string? Code = null);

/// <summary>
/// Whose link this is, for the page it lands on.
/// </summary>
/// <remarks>
/// A name, initials and a colour — what it takes to say "Anna invites you"
/// and nothing more. No id, no handle, no photograph: whoever holds the link
/// may not have an account yet, and the avatar image is only served to people
/// who are signed in anyway (docs/privacy.md, data minimisation).
///
/// <see cref="Relation"/> is null without a session, and otherwise where the
/// visitor already stands with the sender, so the page can say "that is your
/// own link" or "you are already friends" instead of offering a button that
/// does nothing.
/// </remarks>
public sealed record InvitePreviewResponse(
    string DisplayName,
    string Initials,
    string AvatarColor,
    FriendshipState? Relation);

/// <summary>
/// The way into q2 for somebody who does not know anybody here — and the way
/// to a friendship for somebody who already has an account.
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
///
/// There are two ways to spend a code, and they end in the same row: during
/// registration (<see cref="RedeemAsync"/>), and by somebody who already has an
/// account tapping "accept" on the page the link opens
/// (<see cref="AcceptAsync"/>). See
/// [0033](../../../../docs/adr/0033-invite-links-for-new-and-existing-accounts.md).
/// </remarks>
public sealed class InviteService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    BlockList blockList,
    FriendsService friends,
    Notifier notifier,
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
    /// Who sent this link, and where the visitor stands with them.
    /// </summary>
    /// <remarks>
    /// Answered without a session, because the person following a link usually
    /// has none yet. That reverses what this service used to promise — "there
    /// is no way to look a code up" — and it is safe for the same reason the
    /// link is: at 96 random bits the only way to arrive with a code is to have
    /// been sent it, and whoever was sent it is exactly who the sender wanted
    /// to see their name.
    ///
    /// A blocked pair gets the same 404 as a code that means nothing. A page
    /// that read differently for them would be announcing the block.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">The code means nothing, or the two have blocked each other.</exception>
    public async Task<InvitePreviewResponse> PreviewAsync(string? code, CancellationToken cancellationToken)
    {
        var host = await FindHostAsync(code, cancellationToken) ?? throw NotFound();

        if (!currentPerson.IsSignedIn)
        {
            return new InvitePreviewResponse(host.DisplayName, host.Initials, host.AvatarColor, Relation: null);
        }

        var me = await currentPerson.GetAsync(cancellationToken);

        if (host.Id != me.Id && await blockList.IsHiddenFromMeAsync(host.Id, cancellationToken))
        {
            throw NotFound();
        }

        return new InvitePreviewResponse(
            host.DisplayName,
            host.Initials,
            host.AvatarColor,
            await friends.StateBetweenAsync(me.Id, host.Id, cancellationToken));
    }

    /// <summary>
    /// Makes a friendship with whoever sent the link, for somebody who already
    /// has an account.
    /// </summary>
    /// <remarks>
    /// Idempotent where it can be: already friends is not an error, because
    /// the link was tapped twice or forwarded to somebody who was already in —
    /// and either way what they wanted is true.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">The code means nothing, or the two have blocked each other.</exception>
    /// <exception cref="DomainValidationException">It is the visitor's own link.</exception>
    public async Task<PersonSummary> AcceptAsync(string? code, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var host = await FindHostAsync(code, cancellationToken) ?? throw NotFound();

        if (host.Id == me.Id)
        {
            throw new DomainValidationException("Code", "This is your own invite link.");
        }

        if (await blockList.IsHiddenFromMeAsync(host.Id, cancellationToken))
        {
            throw NotFound();
        }

        if (await BefriendAsync(host.Id, me.Id, cancellationToken))
        {
            await database.SaveChangesAsync(cancellationToken);

            // This person's other devices; the sender hears it through the
            // notification BefriendAsync staged.
            notifier.Touch([me.Id], LiveArea.Friends);
            await notifier.FlushAsync(cancellationToken);

            logger.LogInformation("An invite link was accepted by {PersonId}", me.Id);
        }

        return PersonSummary.From(host, timeProvider.GetUtcNow());
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

        var host = await FindHostAsync(code, cancellationToken);

        if (host is null || host.Id == newcomer.Id)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                logger.LogInformation("An invite code was presented that no longer means anything");
            }

            return false;
        }

        // Staged inside the registration's transaction and delivered by
        // AccountService once that has committed.
        await BefriendAsync(host.Id, newcomer.Id, cancellationToken);

        logger.LogInformation("An invite link was redeemed");
        return true;
    }

    /// <summary>
    /// The one place a link turns into a friendship row. Returns false when
    /// there was nothing to do.
    /// </summary>
    /// <remarks>
    /// Accepted, not pending: whoever sent the link has already said yes by
    /// sending it. A request already waiting between the two — either way
    /// round — is accepted rather than joined by a second row, which the
    /// unique pair index would refuse anyway. When the request was the
    /// visitor's to the sender, the link is the sender's answer to it.
    ///
    /// Stages the notification and leaves saving to the caller, because
    /// registration saves inside its own transaction.
    /// </remarks>
    private async Task<bool> BefriendAsync(Guid hostId, Guid guestId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var existing = await database.Friendships.SingleOrDefaultAsync(
            f => (f.RequesterId == hostId && f.AddresseeId == guestId)
                || (f.RequesterId == guestId && f.AddresseeId == hostId),
            cancellationToken);

        switch (existing)
        {
            case { Status: FriendshipStatus.Accepted }:
                return false;

            case not null:
                existing.Accept(existing.AddresseeId, now);
                break;

            default:
                var friendship = Friendship.Request(idGenerator.NewId(), hostId, guestId, now);
                friendship.Accept(guestId, now);
                database.Friendships.Add(friendship);
                break;
        }

        // Whoever sent the link hears that it worked.
        await notifier.StageAsync(FriendsService.FriendshipStartedBy(guestId), [hostId], cancellationToken);

        return true;
    }

    /// <summary>The person a code belongs to, or null if it means nothing.</summary>
    private async Task<Person?> FindHostAsync(string? code, CancellationToken cancellationToken)
    {
        var trimmed = code?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > Person.MaxInviteCodeLength)
        {
            return null;
        }

        return await database.People
            .AsNoTracking()
            .SingleOrDefaultAsync(person => person.InviteCode == trimmed, cancellationToken);
    }

    /// <summary>
    /// Deliberately not carrying the code: the exception message can reach a
    /// log, and the code is a credential in everything but name.
    /// </summary>
    private static ResourceNotFoundException NotFound() => new("Invite", "link");

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
