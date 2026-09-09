using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Challenges;

/// <summary>
/// The room for today's prompt, and the archive of your own contributions.
/// </summary>
/// <remarks>
/// Two of the six scopings the migration plan puts on the server rather than in
/// a screen (section 7e) live here:
///
/// 1. **The room contains only the viewer's own friends.** Everybody sees a
///    different room, so there is no public surface and nothing to moderate.
///    The cut is made in <see cref="DescribeRoomAsync"/>, in the one place the
///    response is built, rather than in each screen that shows part of it.
/// 2. **The archive contains only the viewer's own contributions.** That one is
///    enforced by the shape of the query: it starts from their entries rather
///    than from the challenges, so other people's pictures are never in the
///    result to be filtered out and there is no filter to forget.
///
/// A third rule sits underneath both. Until somebody has contributed, their
/// friends' image ids are left out of the response
/// (<see cref="Challenge.RevealsTo"/>) — and the picture endpoint applies the
/// same test to the bytes (<see cref="ImageService.CanReadAsync"/>). It is the
/// second half that makes it a rule: a blur in a browser over bytes that were
/// sent anyway is a curtain with a gap in it.
/// </remarks>
public sealed class ChallengeService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    ImageService images,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    Q2Metrics metrics,
    ILogger<ChallengeService> logger)
{
    /// <summary>
    /// How many of your own contributions the archive hands over at once.
    /// </summary>
    /// <remarks>
    /// A grid of squares on a phone: ninety is over a year of taking part every
    /// other day, and far past the point where anybody scrolls. Paging can come
    /// when somebody actually reaches the end of it.
    /// </remarks>
    public const int ArchiveLimit = 90;

    /// <summary>Today's room, or nothing if no challenge is running.</summary>
    public async Task<ChallengeTodayResponse> TodayAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var challenge = await ActiveAsync(now, tracking: false, cancellationToken);

        return new ChallengeTodayResponse(
            challenge is null ? null : await DescribeRoomAsync(challenge, me.Id, now, cancellationToken));
    }

    /// <summary>
    /// Contributes a photograph to today's challenge, replacing whatever this
    /// person had already put in.
    /// </summary>
    /// <exception cref="DomainValidationException">Nothing is running, or the request has no picture.</exception>
    /// <exception cref="ResourceNotFoundException">The image is not theirs, or was not uploaded for this.</exception>
    public async Task<ChallengeRoomResponse> SubmitAsync(
        SubmitChallengeEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var challenge = await ActiveAsync(now, tracking: true, cancellationToken)
            ?? throw new DomainValidationException("Challenge", "No challenge is running right now.");

        if (request.ImageId is not { } imageId)
        {
            throw new DomainValidationException(nameof(request.ImageId), "A contribution needs a photograph.");
        }

        // Theirs, and uploaded for this. Without the check, "contribute this
        // id" would be a way to put somebody else's picture in the room — or to
        // read one, since contributing is also how you earn sight of the rest.
        await images.RequireOwnedAsync(imageId, me.Id, ImagePurpose.ChallengeEntry, cancellationToken);

        var replaced = challenge.EntryOf(me.Id)?.ImageId;

        challenge.Contribute(idGenerator.NewId(), me.Id, imageId, request.CapturedInApp, now);

        // The picture that has just been replaced goes with it. Nothing points
        // at it any more, and leaving it would let a second contribution be a
        // way to spend somebody's storage allowance twice.
        var orphaned = replaced is { } previous && previous != imageId
            ? await images.RemoveOwnedAsync([previous], me.Id, cancellationToken)
            : [];

        await database.SaveChangesAsync(cancellationToken);

        // Bytes after the save: a file removed before the rows are committed is
        // a picture that comes back broken if the commit fails.
        await images.DeleteBytesAsync(orphaned, cancellationToken);

        // No prompt, no person: that one happened, and nothing about whose.
        logger.LogInformation("A challenge contribution was made");
        metrics.CountChallengeEntry(request.CapturedInApp);

        return await DescribeRoomAsync(challenge, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Takes this person's contribution back out of today's room.
    /// </summary>
    /// <remarks>
    /// The picture goes with it, and so does the view: withdrawing covers the
    /// friends' contributions again, because the reciprocity rule has to read
    /// the same in both directions.
    /// </remarks>
    public async Task<ChallengeTodayResponse> WithdrawAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var challenge = await ActiveAsync(now, tracking: true, cancellationToken)
            ?? throw new DomainValidationException("Challenge", "No challenge is running right now.");

        var withdrawn = challenge.Withdraw(me.Id)
            ?? throw new DomainValidationException("Challenge", "You have not contributed to this one.");

        var orphaned = await images.RemoveOwnedAsync([withdrawn.ImageId], me.Id, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await images.DeleteBytesAsync(orphaned, cancellationToken);

        logger.LogInformation("A challenge contribution was withdrawn");

        return new ChallengeTodayResponse(await DescribeRoomAsync(challenge, me.Id, now, cancellationToken));
    }

    /// <summary>
    /// Adds or takes back a reaction on a contribution in today's room.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">
    /// No such contribution, or it is not this person's to see — which is the
    /// same answer, deliberately. A 403 would confirm that somebody's picture
    /// exists.
    /// </exception>
    public async Task<ChallengeEntryResponse> ReactAsync(
        Guid entryId,
        ReactToChallengeEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind is not { } kind)
        {
            throw new DomainValidationException(nameof(request.Kind), "A reaction needs a kind.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var challenge = await ActiveAsync(now, tracking: true, cancellationToken)
            ?? throw new ResourceNotFoundException("ChallengeEntry", entryId);

        var entry = challenge.Entries.SingleOrDefault(candidate => candidate.Id == entryId)
            ?? throw new ResourceNotFoundException("ChallengeEntry", entryId);

        /*
         * Exactly what the room shows, and nothing else.
         *
         * Three things have to hold: the contribution is the viewer's own or a
         * friend's, and the viewer has contributed themselves. Reacting is
         * seeing — anything reachable here that the room does not show would be
         * a way to confirm that a stranger took part.
         */
        if (!challenge.RevealsTo(me.Id)
            || (entry.PersonId != me.Id && !await IsFriendAsync(me.Id, entry.PersonId, cancellationToken)))
        {
            throw new ResourceNotFoundException("ChallengeEntry", entryId);
        }

        entry.ToggleReaction(idGenerator.NewId(), me.Id, kind);
        await database.SaveChangesAsync(cancellationToken);

        var people = await LoadPeopleAsync([entry.PersonId], cancellationToken);

        return Describe(entry, me.Id, people, now, revealed: true);
    }

    /// <summary>
    /// Every challenge this person has taken part in, newest first.
    /// </summary>
    /// <remarks>
    /// Their own contributions and no others, and the query starts from those
    /// rather than from the challenges — so there is no filter that could be
    /// forgotten. The room is deliberately transient: nobody gets to build a
    /// lasting collection of other people's pictures, while their own memory is
    /// untouched.
    /// </remarks>
    public async Task<IReadOnlyList<ChallengeArchiveEntryResponse>> ArchiveAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var rows = await database.ChallengeEntries
            .AsNoTracking()
            .Where(entry => entry.PersonId == me.Id)
            .Join(
                database.Challenges,
                entry => entry.ChallengeId,
                challenge => challenge.Id,
                (entry, challenge) => new { Entry = entry, Challenge = challenge })

            // By the challenge's day rather than by when the picture was taken:
            // the archive is a list of prompts you answered, and two entries
            // from the same day cannot exist.
            .OrderByDescending(row => row.Challenge.Day)
            .Take(ArchiveLimit)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var people = await LoadPeopleAsync([me.Id], cancellationToken);

        return
        [
            .. rows.Select(row => new ChallengeArchiveEntryResponse(
                Describe(row.Challenge),
                Describe(row.Entry, me.Id, people, now, revealed: true))),
        ];
    }

    /// <summary>The challenge that is running, if one is.</summary>
    /// <remarks>
    /// The newest match rather than the only one. In a queue built by
    /// <see cref="ChallengeQueueWorker"/> the days tile without overlapping and
    /// there is at most one — but a seeded world builds its day in UTC while
    /// the queue builds days in the deployment's zone, so near a zone boundary
    /// the two can overlap by a few hours. Taking the most recently published
    /// one makes that a harmless off-by-an-evening instead of an exception on
    /// somebody's start screen.
    ///
    /// <see cref="Challenge.Entries"/> comes along because every caller needs
    /// either the viewer's own contribution or all of them, and one round trip
    /// is cheaper than deciding which.
    /// </remarks>
    private async Task<Challenge?> ActiveAsync(
        DateTimeOffset now,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var challenges = tracking ? database.Challenges : database.Challenges.AsNoTracking();

        return await challenges
            .Include(challenge => challenge.Entries)
            .ThenInclude(entry => entry.Reactions)
            .Where(challenge => challenge.PublishedAt <= now && challenge.ExpiresAt > now)
            .OrderByDescending(challenge => challenge.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ChallengeRoomResponse> DescribeRoomAsync(
        Challenge challenge,
        Guid viewerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revealed = challenge.RevealsTo(viewerId);
        var friendIds = await FriendIdsAsync(viewerId, cancellationToken);

        var friendEntries = challenge.Entries
            .Where(entry => entry.PersonId != viewerId && friendIds.Contains(entry.PersonId))

            // Oldest first, like a chat: the room fills up over the course of
            // the day and reads in the order it happened.
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .ToList();

        var own = challenge.EntryOf(viewerId);

        var people = await LoadPeopleAsync(
            [.. friendEntries.Select(entry => entry.PersonId).Append(viewerId)],
            cancellationToken);

        return new ChallengeRoomResponse(
            Describe(challenge),
            own is null ? null : Describe(own, viewerId, people, now, revealed: true),
            [.. friendEntries.Select(entry => Describe(entry, viewerId, people, now, revealed))],
            friendIds.Count,
            revealed);
    }

    private static ChallengeResponse Describe(Challenge challenge) =>
        new(challenge.Id, challenge.Prompt, challenge.PublishedAt, challenge.ExpiresAt);

    /// <param name="revealed">
    /// False while the room is covered. The picture's id is then left out
    /// entirely rather than sent for a client to hide — and so are the
    /// reactions, because there is nothing yet to react to.
    /// </param>
    private static ChallengeEntryResponse Describe(
        ChallengeEntry entry,
        Guid viewerId,
        IReadOnlyDictionary<Guid, Person> people,
        DateTimeOffset now,
        bool revealed)
    {
        var reactions = revealed
            ? entry.Reactions
                .GroupBy(reaction => reaction.Kind)
                .OrderBy(group => group.Key)
                .Select(group => new ReactionSummaryResponse(
                    group.Key,
                    group.Count(),
                    group.Any(reaction => reaction.PersonId == viewerId)))
                .ToList()
            : [];

        return new ChallengeEntryResponse(
            entry.Id,
            PersonSummary.From(people[entry.PersonId], now),
            revealed ? entry.ImageId : null,
            entry.CapturedInApp,
            entry.CreatedAt,
            entry.PersonId == viewerId,
            reactions);
    }

    /// <summary>The people this viewer is actually friends with.</summary>
    private async Task<HashSet<Guid>> FriendIdsAsync(Guid viewerId, CancellationToken cancellationToken) =>
    [
        .. await database.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted
                && (friendship.RequesterId == viewerId || friendship.AddresseeId == viewerId))
            .Select(friendship => friendship.RequesterId == viewerId
                ? friendship.AddresseeId
                : friendship.RequesterId)
            .ToListAsync(cancellationToken),
    ];

    private async Task<bool> IsFriendAsync(Guid viewerId, Guid otherId, CancellationToken cancellationToken) =>
        await database.Friendships
            .AsNoTracking()
            .AnyAsync(
                friendship => friendship.Status == FriendshipStatus.Accepted
                    && ((friendship.RequesterId == viewerId && friendship.AddresseeId == otherId)
                        || (friendship.RequesterId == otherId && friendship.AddresseeId == viewerId)),
                cancellationToken);

    private async Task<Dictionary<Guid, Person>> LoadPeopleAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        await database.People
            .AsNoTracking()
            .Where(person => ids.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);
}
