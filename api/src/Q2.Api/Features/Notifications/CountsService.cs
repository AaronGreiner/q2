using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// Every number the app puts on a badge, from one place.
/// </summary>
/// <remarks>
/// These used to ride along on the profile, and the tab bar learnt about a new
/// message whenever some page happened to reload that. They are their own read
/// now because they have a way of arriving that the profile does not: the live
/// connection pushes a fresh set whenever one of them moves, and the profile
/// has no business being re-sent for that
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
///
/// Taking a person id rather than asking <see cref="CurrentPerson"/> is the
/// point: the numbers are computed for whoever something just happened to,
/// who is usually not the person whose request caused it.
///
/// Every count is **derived**, never stored, like every other number in q2.
/// </remarks>
public sealed class CountsService(Q2DbContext database, TimeProvider timeProvider)
{
    public async Task<CountsResponse> ForAsync(Guid personId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hidden = await BlockList.HiddenFromAsync(database, personId, cancellationToken);

        return new CountsResponse(
            await UnreadChatsAsync(personId, hidden, cancellationToken),
            await PendingRequestsAsync(personId, cancellationToken),
            await UnseenNotificationsAsync(personId, hidden, now, cancellationToken),
            await ProofService.WaitingForVote(database, personId, now).CountAsync(cancellationToken));
    }

    /// <summary>
    /// Conversations with something from somebody else after this person's
    /// read marker — minus a direct thread with somebody hidden by a block,
    /// which the chat list does not show and a badge must not count.
    /// </summary>
    private async Task<int> UnreadChatsAsync(
        Guid personId,
        IReadOnlySet<Guid> hidden,
        CancellationToken cancellationToken)
    {
        var mine = await database.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.PersonId == personId)
            .Select(participant => new { participant.ConversationId, participant.LastReadAt })
            .ToListAsync(cancellationToken);

        if (mine.Count == 0)
        {
            return 0;
        }

        var ids = mine.Select(participant => participant.ConversationId).ToList();

        // The newest message from somebody else in each of them, which is all
        // "is anything unread" needs to know.
        var newest = await database.ChatMessages
            .AsNoTracking()
            .Where(message => ids.Contains(message.ConversationId) && message.SenderPersonId != personId)
            .GroupBy(message => message.ConversationId)
            .Select(group => new { ConversationId = group.Key, SentAt = group.Max(message => message.SentAt) })
            .ToListAsync(cancellationToken);

        var unread = newest
            .Where(latest => mine.Single(participant => participant.ConversationId == latest.ConversationId).LastReadAt is not { } read
                || latest.SentAt > read)
            .Select(latest => latest.ConversationId)
            .ToList();

        if (unread.Count == 0 || hidden.Count == 0)
        {
            return unread.Count;
        }

        var hiddenDirect = await database.Conversations
            .AsNoTracking()
            .CountAsync(
                conversation => unread.Contains(conversation.Id)
                    && conversation.Kind == ConversationKind.Direct
                    && conversation.Participants.Any(participant =>
                        participant.PersonId != personId && hidden.Contains(participant.PersonId)),
                cancellationToken);

        return unread.Count - hiddenDirect;
    }

    /// <summary>Requests waiting for *this* person's answer, not requests in general.</summary>
    private Task<int> PendingRequestsAsync(Guid personId, CancellationToken cancellationToken) =>
        database.Friendships
            .AsNoTracking()
            .CountAsync(
                friendship => friendship.Status == FriendshipStatus.Pending && friendship.AddresseeId == personId,
                cancellationToken);

    /// <summary>
    /// The lines the bell would draw as new — counted with the bell's own
    /// grouping, so five reactions to one photograph are one, exactly as the
    /// bell shows them.
    /// </summary>
    private async Task<int> UnseenNotificationsAsync(
        Guid personId,
        IReadOnlySet<Guid> hidden,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seen = await database.People
            .AsNoTracking()
            .Where(person => person.Id == personId)
            .Select(person => person.NotificationsSeenAt)
            .SingleOrDefaultAsync(cancellationToken) ?? DateTimeOffset.MinValue;

        var rows = await InboxService.RecentAsync(database, personId, now, cancellationToken);

        return InboxLines
            .Group(rows.Where(row => row.ActorPersonId is not { } actor || !hidden.Contains(actor)), seen)
            .Count(line => line.IsNew);
    }
}
