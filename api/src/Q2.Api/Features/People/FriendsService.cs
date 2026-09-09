using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.People;

/// <summary>
/// Who you know, who wants to know you, and who you might.
/// </summary>
/// <remarks>
/// Every read here starts from the signed-in person and only ever widens along
/// edges they are part of. Nothing in this service can answer a question about
/// two other people's relationship, which is deliberate: the friend graph is
/// personal data, and "who is Lena friends with" is Lena's to answer.
/// </remarks>
public sealed class FriendsService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    BlockList blockList,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<FriendsService> logger)
{
    /// <summary>How many people to offer as suggestions.</summary>
    public const int SuggestionLimit = 6;

    /// <summary>How many search results to answer with.</summary>
    public const int SearchLimit = 20;

    /// <summary>Shortest search term that is worth a query.</summary>
    public const int MinimumSearchLength = 2;

    /// <summary>
    /// The whole friends screen, as it looks with nothing typed into it.
    /// </summary>
    /// <remarks>
    /// No filter parameter. The screen has one search box and it means one
    /// thing — find a person, anywhere in q2 — which is
    /// <see cref="SearchAsync"/>. A second, narrower kind of searching on the
    /// same control was the reason the old screen could not add anybody it had
    /// not already been told about.
    /// </remarks>
    public async Task<FriendsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var mine = await MyFriendshipsAsync(me.Id, cancellationToken);
        var connectedIds = mine.Select(f => f.OtherThan(me.Id)).ToHashSet();
        var friendIds = mine
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Select(f => f.OtherThan(me.Id))
            .ToHashSet();

        // One hop further out: the friendships of my friends are what a
        // suggestion and a mutual count are both computed from.
        var graph = new FriendGraph(await database.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted
                && (friendIds.Contains(f.RequesterId) || friendIds.Contains(f.AddresseeId)))
            .ToListAsync(cancellationToken));

        /*
         * Blocked people are excluded by being treated as already connected.
         *
         * A suggestion is the one place somebody hidden could come back on
         * screen without either of them doing anything: the friendship is gone,
         * so the graph would happily offer them as "a friend of a friend"
         * tomorrow. Feeding them in here rather than filtering afterwards keeps
         * the mutual counts honest and leaves one list to reason about.
         */
        var hidden = await blockList.HiddenFromMeAsync(cancellationToken);
        var excluded = new HashSet<Guid>(connectedIds) { me.Id };
        excluded.UnionWith(hidden);

        var suggestions = graph.SuggestionsFor(me.Id, excluded, SuggestionLimit);

        var wanted = connectedIds.Concat(suggestions.Select(s => s.PersonId)).ToHashSet();
        var people = await LoadPeopleAsync(wanted, cancellationToken);
        var streaks = await StreaksAsync(friendIds, today, cancellationToken);

        var friends = mine
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Select(f => people.GetValueOrDefault(f.OtherThan(me.Id)))
            .OfType<Person>()
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(person => new FriendResponse(
                PersonSummary.From(person, now),
                streaks.GetValueOrDefault(person.Id),
                person.LastSeenAt))
            .ToList();

        var requests = mine
            .Where(f => f.IsIncomingFor(me.Id) && people.ContainsKey(f.RequesterId))
            .Select(f => new FriendRequestResponse(
                PersonSummary.From(people[f.RequesterId], now),
                graph.MutualCount(me.Id, f.RequesterId),
                f.RequestedAt))
            .OrderByDescending(r => r.RequestedAt)
            .ThenBy(r => r.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sent = mine
            .Where(f => f.IsOutgoingFrom(me.Id) && people.ContainsKey(f.AddresseeId))
            .Select(f => new SentRequestResponse(PersonSummary.From(people[f.AddresseeId], now), f.RequestedAt))
            .OrderByDescending(r => r.RequestedAt)
            .ThenBy(r => r.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var offered = suggestions
            .Where(s => people.ContainsKey(s.PersonId))
            .Select(s => new FriendSuggestionResponse(
                PersonSummary.From(people[s.PersonId], now),
                s.MutualFriends))
            .ToList();

        logger.LogDebug("Loaded friends for {PersonId}: {FriendCount} friend(s)", me.Id, friends.Count);

        return new FriendsResponse(friends, requests, sent, offered);
    }

    /// <summary>
    /// Finds people by name or handle.
    /// </summary>
    /// <remarks>
    /// The whole directory is searchable, which is what makes it possible to
    /// add somebody who is not already a friend of a friend. A result carries
    /// no more than a row needs: a name, an avatar, and where the two of you
    /// stand. Not an email address — that is the credential, and searching by
    /// one would turn this into a way to check whether a given address has an
    /// account here.
    /// </remarks>
    public async Task<IReadOnlyList<PersonSearchResultResponse>> SearchAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var term = query?.Trim();

        // Two characters, so an empty box does not ask the server for
        // everybody who has ever signed up.
        if (term is null || term.Length < MinimumSearchLength)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var pattern = $"%{Escape(term)}%";

        // The directory is searchable by everybody, which makes this the most
        // likely way a blocked person would reappear — by being looked up.
        var hidden = await blockList.HiddenFromMeAsync(cancellationToken);

        var matches = await database.People
            .AsNoTracking()
            .Where(p => p.Id != me.Id
                && !hidden.Contains(p.Id)
                && (EF.Functions.Like(p.DisplayName, pattern, LikeEscape)
                    || EF.Functions.Like(p.Handle, pattern, LikeEscape)))
            .OrderBy(p => p.DisplayName)
            .ThenBy(p => p.Id)
            .Take(SearchLimit)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            return [];
        }

        var mine = await MyFriendshipsAsync(me.Id, cancellationToken);
        var byOther = mine.ToDictionary(f => f.OtherThan(me.Id));

        var friendIds = mine
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Select(f => f.OtherThan(me.Id))
            .ToHashSet();

        // My friendships plus the matches' own, which is exactly what a mutual
        // count needs and no more.
        var matchIds = matches.Select(p => p.Id).ToList();
        var graph = new FriendGraph(await database.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted
                && (friendIds.Contains(f.RequesterId) || friendIds.Contains(f.AddresseeId)
                    || matchIds.Contains(f.RequesterId) || matchIds.Contains(f.AddresseeId)))
            .ToListAsync(cancellationToken));

        return
        [
            .. matches.Select(person => new PersonSearchResultResponse(
                PersonSummary.From(person, now),
                StateFor(byOther.GetValueOrDefault(person.Id), me.Id),
                graph.MutualCount(me.Id, person.Id))),
        ];
    }

    /// <summary>
    /// Asks somebody to be friends.
    /// </summary>
    /// <remarks>
    /// Asking somebody who has already asked you accepts their request rather
    /// than creating a second row facing the other way. Two people tapping
    /// "add" within the same minute is a friendship, not a conflict.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such person.</exception>
    /// <exception cref="DomainValidationException">You are already connected.</exception>
    public async Task<PersonSearchResultResponse> RequestAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (personId == me.Id)
        {
            throw new DomainValidationException("PersonId", "You cannot send yourself a friend request.");
        }

        var person = await FindPersonAsync(personId, cancellationToken);

        // 404 rather than a sentence: a refusal that reads differently for a
        // blocked person than for a stranger is a notification.
        if (await blockList.IsHiddenFromMeAsync(personId, cancellationToken))
        {
            throw new ResourceNotFoundException("Person", personId);
        }

        var existing = await FindBetweenAsync(me.Id, personId, cancellationToken);

        switch (existing)
        {
            case { Status: FriendshipStatus.Accepted }:
                throw new DomainValidationException("PersonId", "You are already friends with this person.");

            case not null when existing.IsIncomingFor(me.Id):
                existing.Accept(me.Id, now);
                break;

            case not null:
                throw new DomainValidationException("PersonId", "You have already asked this person.");

            default:
                database.Friendships.Add(Friendship.Request(idGenerator.NewId(), me.Id, personId, now));
                break;
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Friend request from {PersonId} to {OtherPersonId}", me.Id, personId);

        return await DescribeAsync(person, me.Id, now, cancellationToken);
    }

    /// <summary>Accepts a request somebody sent you.</summary>
    /// <exception cref="ResourceNotFoundException">No such person, or no request from them.</exception>
    /// <exception cref="DomainValidationException">There was nothing to accept.</exception>
    public async Task<FriendResponse> AcceptAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var friendship = await FindBetweenAsync(me.Id, personId, cancellationToken)
            ?? throw new ResourceNotFoundException("Friend request", personId);

        friendship.Accept(me.Id, now);
        await database.SaveChangesAsync(cancellationToken);

        var person = await database.People
            .AsNoTracking()
            .Include(p => p.CheckIns)
            .SingleOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new ResourceNotFoundException("Person", personId);

        logger.LogInformation("Friend request accepted by {PersonId}", me.Id);

        return new FriendResponse(PersonSummary.From(person, now), person.StreakOn(today), person.LastSeenAt);
    }

    /// <summary>
    /// Turns a request down.
    /// </summary>
    /// <remarks>
    /// The row is deleted rather than kept as "declined". Keeping it would mean
    /// storing a record of who somebody did not want to know, which is not
    /// information q2 has any use for (docs/privacy.md). The honest consequence
    /// is that the other person can ask again later.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No request from that person.</exception>
    /// <exception cref="DomainValidationException">That request is not yours to decline.</exception>
    public async Task DeclineAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var friendship = await FindBetweenAsync(me.Id, personId, cancellationToken)
            ?? throw new ResourceNotFoundException("Friend request", personId);

        if (!friendship.IsIncomingFor(me.Id))
        {
            throw new DomainValidationException("PersonId", "There is no pending request from this person.");
        }

        database.Friendships.Remove(friendship);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Friend request declined by {PersonId}", me.Id);
    }

    /// <summary>Takes back a request you sent.</summary>
    /// <exception cref="ResourceNotFoundException">No request to that person.</exception>
    /// <exception cref="DomainValidationException">There was nothing of yours to take back.</exception>
    public async Task WithdrawAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var friendship = await FindBetweenAsync(me.Id, personId, cancellationToken)
            ?? throw new ResourceNotFoundException("Friend request", personId);

        if (!friendship.IsOutgoingFrom(me.Id))
        {
            throw new DomainValidationException("PersonId", "There is no request of yours to take back.");
        }

        database.Friendships.Remove(friendship);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Friend request withdrawn by {PersonId}", me.Id);
    }

    /// <summary>
    /// Ends a friendship.
    /// </summary>
    /// <remarks>
    /// Ends it for both people, because it was one row for both of them. The
    /// conversations they had are left alone: a message somebody wrote is
    /// theirs, and deleting a shared history because one side walked away is
    /// not this action's decision to make.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">You are not friends with that person.</exception>
    public async Task RemoveAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var friendship = await FindBetweenAsync(me.Id, personId, cancellationToken);

        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            throw new ResourceNotFoundException("Friendship", personId);
        }

        database.Friendships.Remove(friendship);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Friendship ended by {PersonId}", me.Id);
    }

    /// <summary>Everybody <paramref name="personId"/> has an accepted friendship with.</summary>
    /// <remarks>
    /// The one place other features ask "who are my friends" — the feed,
    /// the feed and the group-chat member list all need the same answer, and
    /// three copies of this query would be three chances to forget that a
    /// friendship has two ends.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> FriendIdsAsync(Guid personId, CancellationToken cancellationToken)
    {
        var friendships = await database.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted
                && (f.RequesterId == personId || f.AddresseeId == personId))
            .ToListAsync(cancellationToken);

        return [.. friendships.Select(f => f.OtherThan(personId))];
    }

    /// <summary>The character that turns a LIKE wildcard back into a literal.</summary>
    private const string LikeEscape = "\\";

    /// <summary>
    /// Makes a search term safe to put inside a LIKE pattern. Without this a
    /// person typing "%" would match everybody, and "_" everybody with a name.
    /// </summary>
    private static string Escape(string term) => term
        .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
        .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
        .Replace("_", LikeEscape + "_", StringComparison.Ordinal);

    /// <summary>
    /// Where one person stands with another.
    /// </summary>
    /// <remarks>
    /// Public because a profile screen needs it to decide which buttons a
    /// person gets, and there is exactly one correct answer to "are we
    /// friends" — a second implementation would be a second answer.
    /// </remarks>
    public async Task<FriendshipState> StateBetweenAsync(
        Guid meId,
        Guid otherId,
        CancellationToken cancellationToken)
    {
        if (meId == otherId)
        {
            return FriendshipState.Self;
        }

        var mine = await MyFriendshipsAsync(meId, cancellationToken);

        return StateFor(mine.SingleOrDefault(friendship => friendship.Involves(otherId)), meId);
    }

    private static FriendshipState StateFor(Friendship? friendship, Guid meId) => friendship switch
    {
        null => FriendshipState.None,
        { Status: FriendshipStatus.Accepted } => FriendshipState.Friends,
        _ when friendship.IsIncomingFor(meId) => FriendshipState.RequestReceived,
        _ => FriendshipState.RequestSent,
    };

    private async Task<PersonSearchResultResponse> DescribeAsync(
        Person person,
        Guid meId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mine = await MyFriendshipsAsync(meId, cancellationToken);

        return new PersonSearchResultResponse(
            PersonSummary.From(person, now),
            StateFor(mine.SingleOrDefault(f => f.Involves(person.Id)), meId),
            new FriendGraph(mine).MutualCount(meId, person.Id));
    }

    private Task<List<Friendship>> MyFriendshipsAsync(Guid meId, CancellationToken cancellationToken) =>
        database.Friendships
            .AsNoTracking()
            .Where(f => f.RequesterId == meId || f.AddresseeId == meId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The one row between two people, whichever way round it was created.
    /// Tracked, because every caller either changes or removes it.
    /// </summary>
    private Task<Friendship?> FindBetweenAsync(Guid meId, Guid otherId, CancellationToken cancellationToken) =>
        database.Friendships.SingleOrDefaultAsync(
            f => (f.RequesterId == meId && f.AddresseeId == otherId)
                || (f.RequesterId == otherId && f.AddresseeId == meId),
            cancellationToken);

    private async Task<Person> FindPersonAsync(Guid personId, CancellationToken cancellationToken) =>
        await database.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken)
        ?? throw new ResourceNotFoundException("Person", personId);

    private async Task<IReadOnlyDictionary<Guid, Person>> LoadPeopleAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Person>();
        }

        return await database.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> StreaksAsync(
        IReadOnlyCollection<Guid> personIds,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var checkIns = await database.DailyCheckIns
            .AsNoTracking()
            .Where(c => personIds.Contains(c.PersonId))
            .ToListAsync(cancellationToken);

        return checkIns
            .GroupBy(c => c.PersonId)
            .ToDictionary(group => group.Key, group => Streak.Count(group.Select(c => c.Date), today));
    }
}
