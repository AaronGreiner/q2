using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.People;

/// <summary>Where a friend request currently stands.</summary>
/// <remarks>
/// Stored as text so the database stays readable and reordering the members
/// cannot silently change what a row means.
///
/// Two members, not four. "Suggested" used to be one of them, which meant the
/// database stored a guess as though it were a relationship; suggestions are
/// now derived from the graph (<see cref="FriendGraph"/>). A declined request
/// is not a member either — the row is deleted, because "who did not want to
/// know you" is not something q2 has any use for keeping (docs/privacy.md).
/// </remarks>
public enum FriendshipStatus
{
    /// <summary>Asked for, not answered yet.</summary>
    Pending,

    /// <summary>Agreed to by both sides.</summary>
    Accepted,
}

/// <summary>
/// A connection between two people, from the moment one of them asked.
/// </summary>
/// <remarks>
/// One row for the pair, holding both ends. The previous model stored a
/// friendship one-sidedly — "this is where <em>you</em> stand with them" — which
/// worked only while there was exactly one person who could ever be "you"
/// (docs/adr/0009-single-known-person.md). With real accounts both sides sign
/// in, and a friendship only one of them can see is not one.
///
/// <see cref="RequesterId"/> and <see cref="AddresseeId"/> keep their meaning
/// after the request is answered: they say who asked whom, which is what makes
/// one pending row renderable as "waiting for them" on one screen and "waiting
/// for you" on the other.
/// </remarks>
public sealed class Friendship
{
    // EF Core materialisation only.
    private Friendship()
    {
    }

    private Friendship(
        Guid id,
        Guid requesterId,
        Guid addresseeId,
        FriendshipStatus status,
        DateTimeOffset requestedAt,
        DateTimeOffset? respondedAt)
    {
        Id = id;
        RequesterId = requesterId;
        AddresseeId = addresseeId;
        Status = status;
        RequestedAt = requestedAt;
        RespondedAt = respondedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Who asked.</summary>
    public Guid RequesterId { get; private set; }

    /// <summary>Who was asked.</summary>
    public Guid AddresseeId { get; private set; }

    public FriendshipStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>When it was accepted. Null while it is still pending.</summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>Somebody asks somebody else.</summary>
    /// <exception cref="DomainValidationException">The two ends are the same person, or missing.</exception>
    public static Friendship Request(Guid id, Guid requesterId, Guid addresseeId, DateTimeOffset requestedAt) =>
        Create(id, requesterId, addresseeId, FriendshipStatus.Pending, requestedAt, respondedAt: null);

    /// <summary>
    /// Rebuilds a row in any state. Used by the seeds, which need an already
    /// accepted friendship without replaying the request that made it one.
    /// </summary>
    /// <exception cref="DomainValidationException">The two ends are the same person, or missing.</exception>
    public static Friendship Create(
        Guid id,
        Guid requesterId,
        Guid addresseeId,
        FriendshipStatus status,
        DateTimeOffset requestedAt,
        DateTimeOffset? respondedAt = null)
    {
        if (requesterId == Guid.Empty || addresseeId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(RequesterId), "A friendship needs two people.");
        }

        if (requesterId == addresseeId)
        {
            throw new DomainValidationException(nameof(AddresseeId), "A person cannot befriend themselves.");
        }

        return new Friendship(id, requesterId, addresseeId, status, requestedAt, respondedAt);
    }

    /// <summary>
    /// Answers the request.
    /// </summary>
    /// <remarks>
    /// Only the addressee may accept, and only while it is pending. Letting the
    /// requester accept their own request would make anybody a friend of
    /// anybody without the other side ever agreeing.
    /// </remarks>
    /// <exception cref="DomainValidationException">There is nothing here for this person to accept.</exception>
    public void Accept(Guid personId, DateTimeOffset acceptedAt)
    {
        if (Status != FriendshipStatus.Pending)
        {
            throw new DomainValidationException(nameof(Status), "Only a pending request can be accepted.");
        }

        if (personId != AddresseeId)
        {
            throw new DomainValidationException(nameof(Status), "Only the person who was asked can accept.");
        }

        Status = FriendshipStatus.Accepted;
        RespondedAt = acceptedAt;
    }

    /// <summary>True when <paramref name="personId"/> is one of the two ends.</summary>
    public bool Involves(Guid personId) => RequesterId == personId || AddresseeId == personId;

    /// <summary>The end that is not <paramref name="personId"/>.</summary>
    /// <exception cref="InvalidOperationException">This person is not part of this friendship.</exception>
    public Guid OtherThan(Guid personId) => personId == RequesterId
        ? AddresseeId
        : personId == AddresseeId
            ? RequesterId
            : throw new InvalidOperationException("That person is not part of this friendship.");

    /// <summary>A request this person still has to answer.</summary>
    public bool IsIncomingFor(Guid personId) =>
        Status == FriendshipStatus.Pending && AddresseeId == personId;

    /// <summary>A request this person sent and is waiting on.</summary>
    public bool IsOutgoingFrom(Guid personId) =>
        Status == FriendshipStatus.Pending && RequesterId == personId;
}
