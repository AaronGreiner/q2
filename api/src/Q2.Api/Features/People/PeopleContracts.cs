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

/// <summary>Somebody you are already connected to.</summary>
/// <param name="Streak">Their current streak, so the list can show who is on a roll.</param>
public sealed record FriendResponse(PersonSummary Person, int Streak, DateTimeOffset? LastSeenAt);

/// <summary>Somebody who has asked to be your friend.</summary>
public sealed record FriendRequestResponse(Guid Id, PersonSummary Person, int MutualFriends);

/// <summary>Somebody q2 thinks you might know.</summary>
/// <param name="IsInvited">True once you have sent the request and are waiting.</param>
public sealed record FriendSuggestionResponse(Guid Id, PersonSummary Person, int MutualFriends, bool IsInvited);

/// <summary>Everything the friends screen shows, in one read.</summary>
/// <remarks>
/// One response rather than three endpoints: the screen is useless without all
/// three sections, and three round trips on a phone connection is three chances
/// to show a half-drawn page.
/// </remarks>
public sealed record FriendsResponse(
    IReadOnlyList<FriendResponse> Friends,
    IReadOnlyList<FriendRequestResponse> Requests,
    IReadOnlyList<FriendSuggestionResponse> Suggestions);
