namespace Q2.Api.Features.People;

/// <summary>
/// How a person appears anywhere they are mentioned — a chat header, an avatar
/// stack on a goal, a row on the leaderboard.
/// </summary>
/// <remarks>
/// One shape for all of them on purpose: the alternative is four almost-equal
/// records that drift apart, and an avatar that is a different colour depending
/// on which screen drew it.
/// </remarks>
/// <param name="IsOnline">
/// Derived server-side from the last time this person was seen, so every client
/// agrees — the browser's clock is not part of the contract.
/// </param>
public sealed record PersonSummary(
    Guid Id,
    string DisplayName,
    string Handle,
    string Initials,
    string AvatarColor,
    bool IsOnline)
{
    public static PersonSummary From(Person person, DateTimeOffset now) => new(
        person.Id,
        person.DisplayName,
        person.Handle,
        person.Initials,
        person.AvatarColor,
        person.IsOnlineAt(now));
}

/// <summary>
/// Where the signed-in person stands with somebody else.
/// </summary>
/// <remarks>
/// Sent instead of a set of booleans so the client has one thing to switch on
/// when it decides which buttons a row gets. The states are exclusive by
/// construction: there is at most one friendship row per pair.
/// </remarks>
public enum FriendshipState
{
    /// <summary>No connection at all. The row offers "add".</summary>
    None,

    /// <summary>You asked and are waiting. The row offers "withdraw".</summary>
    RequestSent,

    /// <summary>They asked and you have not answered. The row offers "accept" and "decline".</summary>
    RequestReceived,

    /// <summary>Friends. The row offers "message" and "remove".</summary>
    Friends,

    /// <summary>You. Nothing to offer.</summary>
    Self,
}

/// <summary>Somebody you are already connected to.</summary>
/// <param name="Streak">Their current streak, so the list can show who is on a roll.</param>
public sealed record FriendResponse(PersonSummary Person, int Streak, DateTimeOffset? LastSeenAt);

/// <summary>Somebody who has asked to be your friend.</summary>
public sealed record FriendRequestResponse(PersonSummary Person, int MutualFriends, DateTimeOffset RequestedAt);

/// <summary>A request you sent that has not been answered yet.</summary>
public sealed record SentRequestResponse(PersonSummary Person, DateTimeOffset RequestedAt);

/// <summary>Somebody q2 thinks you might know, and why.</summary>
/// <remarks>
/// Derived from the friend graph rather than stored: a suggestion is a
/// statement about the data as it stands now, and a stored one would go stale
/// the moment somebody's friendships changed.
/// </remarks>
public sealed record FriendSuggestionResponse(PersonSummary Person, int MutualFriends);

/// <summary>A person found by search, and where you stand with them.</summary>
public sealed record PersonSearchResultResponse(
    PersonSummary Person,
    FriendshipState State,
    int MutualFriends);

/// <summary>Everything the friends screen shows, in one read.</summary>
/// <remarks>
/// One response rather than four endpoints: the screen is useless without all
/// of it, and four round trips on a phone connection is four chances to show a
/// half-drawn page.
/// </remarks>
public sealed record FriendsResponse(
    IReadOnlyList<FriendResponse> Friends,
    IReadOnlyList<FriendRequestResponse> Requests,
    IReadOnlyList<SentRequestResponse> SentRequests,
    IReadOnlyList<FriendSuggestionResponse> Suggestions);
