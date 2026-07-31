using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.People;

/// <summary>
/// Where a friend request currently stands.
/// </summary>
/// <remarks>
/// Stored as text so the database stays readable and reordering the members
/// cannot silently change what a row means.
/// </remarks>
public enum FriendshipStatus
{
    /// <summary>Somebody asked to be friends and is waiting for an answer.</summary>
    Requested,

    /// <summary>The request was sent and has not been answered yet.</summary>
    Invited,

    /// <summary>Accepted, in both directions.</summary>
    Accepted,

    /// <summary>Not connected; offered as somebody you might know.</summary>
    Suggested,
}

/// <summary>
/// The connection between the signed-in person and somebody else.
/// </summary>
/// <remarks>
/// Deliberately one-sided: there is exactly one signed-in person in this
/// version, so a row means "this is where <em>you</em> stand with
/// <see cref="PersonId"/>". Modelling the symmetric pair would double every
/// write for a second side nobody can log in as
/// (docs/adr/0009-single-known-person.md). It becomes a pair of rows, or a
/// composite key, on the day accounts arrive — a migration, not a rewrite.
/// </remarks>
public sealed class Friendship
{
    // EF Core materialisation only.
    private Friendship()
    {
    }

    private Friendship(Guid id, Guid personId, FriendshipStatus status, int mutualFriends)
    {
        Id = id;
        PersonId = personId;
        Status = status;
        MutualFriends = mutualFriends;
    }

    public Guid Id { get; private set; }

    /// <summary>The other person. Never the signed-in one.</summary>
    public Guid PersonId { get; private set; }

    public FriendshipStatus Status { get; private set; }

    /// <summary>How many friends the two have in common. Shown under a request.</summary>
    public int MutualFriends { get; private set; }

    public static Friendship Create(Guid id, Guid personId, FriendshipStatus status, int mutualFriends = 0)
    {
        if (mutualFriends < 0)
        {
            throw new DomainValidationException(nameof(MutualFriends), "Mutual friends cannot be negative.");
        }

        return new Friendship(id, personId, status, mutualFriends);
    }

    /// <summary>
    /// Answers an incoming request. Only a <see cref="FriendshipStatus.Requested"/>
    /// row can be accepted — accepting a suggestion would make somebody your
    /// friend without them ever agreeing.
    /// </summary>
    /// <exception cref="DomainValidationException">There is nothing to accept.</exception>
    public void Accept()
    {
        if (Status != FriendshipStatus.Requested)
        {
            throw new DomainValidationException(nameof(Status), "Only a pending request can be accepted.");
        }

        Status = FriendshipStatus.Accepted;
    }

    /// <summary>
    /// Turns a suggestion into a sent request.
    /// </summary>
    /// <exception cref="DomainValidationException">There is nothing to ask for.</exception>
    public void Invite()
    {
        if (Status != FriendshipStatus.Suggested)
        {
            throw new DomainValidationException(nameof(Status), "Only a suggestion can be turned into a request.");
        }

        Status = FriendshipStatus.Invited;
    }
}
