namespace Q2.Api.Features.People;

/// <summary>
/// Who is friends with whom, as a structure that can be asked questions.
/// </summary>
/// <remarks>
/// "How many friends do we have in common" and "who might I know" are both
/// questions about the shape of the graph, not about any one row. Expressing
/// them in SQL would mean a self-join per candidate; expressing them here means
/// one pure, unit-testable type and a single query to fill it.
///
/// It is built from whichever rows the caller loaded, and knows nothing about
/// the ones it was not given. <see cref="FriendsService"/> is what decides that
/// scope — which is the point: a graph that quietly reached for more rows would
/// be a graph that answers about people the caller may not see.
/// </remarks>
public sealed class FriendGraph
{
    private static readonly HashSet<Guid> None = [];

    private readonly Dictionary<Guid, HashSet<Guid>> _accepted = [];

    public FriendGraph(IEnumerable<Friendship> friendships)
    {
        foreach (var friendship in friendships.Where(f => f.Status == FriendshipStatus.Accepted))
        {
            Link(friendship.RequesterId, friendship.AddresseeId);
            Link(friendship.AddresseeId, friendship.RequesterId);
        }
    }

    /// <summary>Everybody <paramref name="personId"/> has an accepted friendship with.</summary>
    public IReadOnlySet<Guid> FriendsOf(Guid personId) =>
        _accepted.TryGetValue(personId, out var friends) ? friends : None;

    /// <summary>How many friends two people have in common.</summary>
    public int MutualCount(Guid personId, Guid otherId)
    {
        var mine = FriendsOf(personId);

        if (mine.Count == 0)
        {
            return 0;
        }

        // Counted from the smaller set: a person with three friends and one
        // with three hundred should cost three lookups, not three hundred.
        var theirs = FriendsOf(otherId);
        var (smaller, larger) = mine.Count <= theirs.Count ? (mine, theirs) : (theirs, mine);

        return smaller.Count(larger.Contains);
    }

    /// <summary>
    /// People <paramref name="personId"/> might know: the friends of their
    /// friends, most mutual connections first.
    /// </summary>
    /// <param name="exclude">
    /// Everybody already connected in any way — friends, and both directions of
    /// a pending request. Suggesting somebody whose request is sitting
    /// unanswered on the same screen would be absurd.
    /// </param>
    public IReadOnlyList<(Guid PersonId, int MutualFriends)> SuggestionsFor(
        Guid personId,
        IReadOnlySet<Guid> exclude,
        int limit)
    {
        var candidates = new Dictionary<Guid, int>();

        foreach (var friendId in FriendsOf(personId))
        {
            foreach (var candidate in FriendsOf(friendId))
            {
                if (candidate == personId || exclude.Contains(candidate))
                {
                    continue;
                }

                // One friend in common is one path to them, so counting the
                // paths *is* counting the mutual friends.
                candidates[candidate] = candidates.GetValueOrDefault(candidate) + 1;
            }
        }

        return
        [
            .. candidates
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Take(limit)
                .Select(entry => (entry.Key, entry.Value)),
        ];
    }

    private void Link(Guid from, Guid to)
    {
        if (!_accepted.TryGetValue(from, out var friends))
        {
            friends = [];
            _accepted[from] = friends;
        }

        friends.Add(to);
    }
}
