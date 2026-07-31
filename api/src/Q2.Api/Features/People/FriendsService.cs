using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.People;

/// <summary>
/// Who you know, who wants to know you, and who you might.
/// </summary>
public sealed class FriendsService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    TimeProvider timeProvider,
    ILogger<FriendsService> logger)
{
    /// <summary>
    /// The whole friends screen.
    /// </summary>
    /// <param name="search">
    /// Optional name filter. It applies to friends and suggestions but never to
    /// requests: hiding somebody's pending request behind a search box is how
    /// it stays unanswered forever.
    /// </param>
    public async Task<FriendsResponse> GetAsync(string? search, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var term = search?.Trim();

        var links = await database.Friendships
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var personIds = links.Select(l => l.PersonId).ToList();
        var people = await database.People
            .AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var checkIns = await database.DailyCheckIns
            .AsNoTracking()
            .Where(c => personIds.Contains(c.PersonId))
            .ToListAsync(cancellationToken);

        bool Matches(Guid personId) =>
            string.IsNullOrEmpty(term)
            || (people.TryGetValue(personId, out var person)
                && person.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase));

        var friends = links
            .Where(l => l.Status == FriendshipStatus.Accepted && people.ContainsKey(l.PersonId) && Matches(l.PersonId))
            .Select(l => people[l.PersonId])
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(p => new FriendResponse(
                PersonSummary.From(p, now),
                Streak.Count(checkIns.Where(c => c.PersonId == p.Id).Select(c => c.Date), today),
                p.LastSeenAt))
            .ToList();

        var requests = links
            .Where(l => l.Status == FriendshipStatus.Requested && people.ContainsKey(l.PersonId))
            .Select(l => new FriendRequestResponse(l.Id, PersonSummary.From(people[l.PersonId], now), l.MutualFriends))
            .OrderByDescending(r => r.MutualFriends)
            .ThenBy(r => r.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suggestions = links
            .Where(l => l.Status is FriendshipStatus.Suggested or FriendshipStatus.Invited
                && people.ContainsKey(l.PersonId)
                && Matches(l.PersonId))
            .Select(l => new FriendSuggestionResponse(
                l.Id,
                PersonSummary.From(people[l.PersonId], now),
                l.MutualFriends,
                l.Status == FriendshipStatus.Invited))
            .OrderByDescending(s => s.MutualFriends)
            .ThenBy(s => s.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The identity behind "me" is not part of this response, but it is what
        // decided its contents — worth one line when a support case asks why
        // somebody saw the list they saw.
        logger.LogDebug("Loaded friends for {PersonId}: {FriendCount} friend(s)", me.Id, friends.Count);

        return new FriendsResponse(friends, requests, suggestions);
    }

    /// <exception cref="ResourceNotFoundException">No such request.</exception>
    /// <exception cref="DomainValidationException">There was nothing to accept.</exception>
    public async Task<FriendResponse> AcceptAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await FindAsync(id, cancellationToken);
        link.Accept();
        await database.SaveChangesAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var person = await database.People
            .AsNoTracking()
            .Include(p => p.CheckIns)
            .SingleOrDefaultAsync(p => p.Id == link.PersonId, cancellationToken)
            ?? throw new ResourceNotFoundException("Person", link.PersonId);

        logger.LogInformation("Friend request {FriendshipId} accepted", link.Id);

        return new FriendResponse(PersonSummary.From(person, now), person.StreakOn(today), person.LastSeenAt);
    }

    /// <summary>
    /// Turns a request down.
    /// </summary>
    /// <remarks>
    /// The row is deleted rather than kept as "declined". Keeping it would mean
    /// storing a record of who somebody did not want to know, which is not
    /// information q2 has any use for (docs/privacy.md).
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such request.</exception>
    public async Task DeclineAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await FindAsync(id, cancellationToken);

        if (link.Status != FriendshipStatus.Requested)
        {
            throw new DomainValidationException("Status", "Only a pending request can be declined.");
        }

        database.Friendships.Remove(link);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Friend request {FriendshipId} declined", id);
    }

    /// <exception cref="ResourceNotFoundException">No such suggestion.</exception>
    /// <exception cref="DomainValidationException">There was nothing to ask for.</exception>
    public async Task<FriendSuggestionResponse> InviteAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await FindAsync(id, cancellationToken);
        link.Invite();
        await database.SaveChangesAsync(cancellationToken);

        var person = await database.People
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == link.PersonId, cancellationToken)
            ?? throw new ResourceNotFoundException("Person", link.PersonId);

        logger.LogInformation("Friend request sent for {FriendshipId}", id);

        return new FriendSuggestionResponse(
            link.Id,
            PersonSummary.From(person, timeProvider.GetUtcNow()),
            link.MutualFriends,
            IsInvited: true);
    }

    private async Task<Friendship> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Friendships.SingleOrDefaultAsync(f => f.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Friendship", id);
}
