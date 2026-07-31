using Q2.Api.Features.People;

namespace Q2.Api.UnitTests.People;

/// <summary>
/// Mutual friends and suggestions, which are now derived rather than stored.
/// </summary>
/// <remarks>
/// These used to be columns a seed wrote by hand, which meant they were true
/// exactly until somebody's friendships changed. They are answers about the
/// shape of the graph, so they are computed from it — and this is where that
/// computation is checked, without a database in the way.
/// </remarks>
public class FriendGraphTests
{
    private static readonly DateTimeOffset When = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Me = new("00000000-0000-4000-8000-000000000001");
    private static readonly Guid Jonas = new("00000000-0000-4000-8000-000000000002");
    private static readonly Guid Lena = new("00000000-0000-4000-8000-000000000003");
    private static readonly Guid Lukas = new("00000000-0000-4000-8000-000000000004");
    private static readonly Guid Stranger = new("00000000-0000-4000-8000-000000000005");

    private static Friendship Accepted(Guid one, Guid other) =>
        Friendship.Create(Guid.CreateVersion7(), one, other, FriendshipStatus.Accepted, When, When);

    private static Friendship Pending(Guid from, Guid to) =>
        Friendship.Request(Guid.CreateVersion7(), from, to, When);

    [Fact]
    public void AFriendshipCountsFromBothEnds()
    {
        var graph = new FriendGraph([Accepted(Me, Jonas)]);

        Assert.Contains(Jonas, graph.FriendsOf(Me));
        Assert.Contains(Me, graph.FriendsOf(Jonas));
    }

    [Fact]
    public void APendingRequestIsNotAFriendship()
    {
        var graph = new FriendGraph([Pending(Me, Jonas)]);

        Assert.Empty(graph.FriendsOf(Me));
        Assert.Empty(graph.FriendsOf(Jonas));
    }

    [Fact]
    public void MutualFriendsAreTheOnesBothPeopleKnow()
    {
        var graph = new FriendGraph([
            Accepted(Me, Jonas),
            Accepted(Me, Lena),
            Accepted(Lukas, Jonas),
            Accepted(Lukas, Lena),
            Accepted(Lukas, Stranger),
        ]);

        Assert.Equal(2, graph.MutualCount(Me, Lukas));
        Assert.Equal(2, graph.MutualCount(Lukas, Me));
    }

    [Fact]
    public void SomebodyWithNoFriendsHasNoneInCommonWithAnyone()
    {
        var graph = new FriendGraph([Accepted(Me, Jonas)]);

        Assert.Equal(0, graph.MutualCount(Me, Stranger));
        Assert.Equal(0, graph.MutualCount(Stranger, Me));
    }

    [Fact]
    public void SuggestionsAreTheFriendsOfYourFriends()
    {
        var graph = new FriendGraph([
            Accepted(Me, Jonas),
            Accepted(Jonas, Lukas),
        ]);

        var suggestions = graph.SuggestionsFor(Me, new HashSet<Guid> { Jonas, Me }, limit: 10);

        Assert.Single(suggestions);
        Assert.Equal(Lukas, suggestions[0].PersonId);
        Assert.Equal(1, suggestions[0].MutualFriends);
    }

    [Fact]
    public void SomebodyReachableThroughTwoFriendsIsSuggestedAhead()
    {
        var graph = new FriendGraph([
            Accepted(Me, Jonas),
            Accepted(Me, Lena),
            Accepted(Jonas, Lukas),
            Accepted(Lena, Lukas),
            Accepted(Jonas, Stranger),
        ]);

        var suggestions = graph.SuggestionsFor(Me, new HashSet<Guid> { Jonas, Lena, Me }, limit: 10);

        Assert.Equal(Lukas, suggestions[0].PersonId);
        Assert.Equal(2, suggestions[0].MutualFriends);
        Assert.Equal(Stranger, suggestions[1].PersonId);
        Assert.Equal(1, suggestions[1].MutualFriends);
    }

    [Fact]
    public void NobodyAlreadyConnectedToIsSuggested()
    {
        // Suggesting somebody whose request is sitting unanswered on the same
        // screen would be absurd, and so would suggesting a friend.
        var graph = new FriendGraph([
            Accepted(Me, Jonas),
            Accepted(Me, Lukas),
            Accepted(Jonas, Lukas),
        ]);

        Assert.Empty(graph.SuggestionsFor(Me, new HashSet<Guid> { Jonas, Lukas, Me }, limit: 10));
    }

    [Fact]
    public void YouAreNeverSuggestedToYourself()
    {
        var graph = new FriendGraph([Accepted(Me, Jonas)]);

        Assert.DoesNotContain(
            graph.SuggestionsFor(Me, new HashSet<Guid> { Jonas }, limit: 10),
            suggestion => suggestion.PersonId == Me);
    }

    [Fact]
    public void TheLimitIsRespected()
    {
        var graph = new FriendGraph([
            Accepted(Me, Jonas),
            Accepted(Jonas, Lena),
            Accepted(Jonas, Lukas),
            Accepted(Jonas, Stranger),
        ]);

        Assert.Equal(2, graph.SuggestionsFor(Me, new HashSet<Guid> { Jonas, Me }, limit: 2).Count);
    }

    [Fact]
    public void AGraphBuiltFromNothingAnswersNothing()
    {
        var graph = new FriendGraph([]);

        Assert.Empty(graph.FriendsOf(Me));
        Assert.Equal(0, graph.MutualCount(Me, Jonas));
        Assert.Empty(graph.SuggestionsFor(Me, new HashSet<Guid>(), limit: 10));
    }
}
