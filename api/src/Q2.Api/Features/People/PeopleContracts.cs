namespace Q2.Api.Features.People;

/// <summary>
/// How a person appears anywhere they are mentioned — a chat header, an avatar
/// stack on a goal, a line in the feed.
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
/// <param name="AvatarImageId">
/// Their photograph, when they have one. Null is not a missing value: it means
/// the initials and the colour beside it are what to draw. Those two are sent
/// either way, so a client never has a person it cannot render — including
/// while the picture is still on its way.
/// </param>
public sealed record PersonSummary(
    Guid Id,
    string DisplayName,
    string Handle,
    string Initials,
    string AvatarColor,
    bool IsOnline,
    Guid? AvatarImageId)
{
    public static PersonSummary From(Person person, DateTimeOffset now) => new(
        person.Id,
        person.DisplayName,
        person.Handle,
        person.Initials,
        person.AvatarColor,
        person.IsOnlineAt(now),
        person.AvatarImageId);
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

/// <summary>
/// How much somebody has delivered and how much they have missed.
/// </summary>
/// <param name="Done">Windows delivered.</param>
/// <param name="Missed">Windows whose deadline passed with something outstanding.</param>
/// <remarks>
/// The two numbers only mean anything beside each other. "47" is a boast;
/// "47 · 5" is a record, and the second number is the whole reason this product
/// is not a habit tracker.
///
/// **It is always scoped to who is asking.** On somebody else's profile it
/// covers only the goals the two of them share — see
/// <see cref="Profile.ProfileService"/>. Anything wider would publish the
/// contents of a stranger's week to somebody who was never let in on it, and
/// the scoping lives in the query rather than in a filter afterwards so there
/// is nothing for a later change to forget.
/// </remarks>
public sealed record BalanceResponse(int Done, int Missed)
{
    /// <summary>Nothing shared, and therefore nothing to show.</summary>
    public static readonly BalanceResponse Empty = new(0, 0);
}

/// <summary>Somebody else's profile, as the person asking may see it.</summary>
/// <param name="Balance">
/// Counted over the goals the two of them share, and over nothing else. Zero
/// when they share none — which is not the same as "this person has never
/// missed anything", and the screen says so in words.
/// </param>
/// <param name="SharedGoals">
/// How many goals the two of them are on together. What lets the client tell
/// "nothing shared" apart from "shared, and a clean record".
/// </param>
/// <param name="State">Where the person asking stands with them.</param>
public sealed record PersonProfileResponse(
    PersonSummary Person,
    FriendshipState State,
    int Streak,
    int KudosReceived,
    int GoalsCompleted,
    BalanceResponse Balance,
    int SharedGoals);
