using Q2.Api.Features.Chats;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;

namespace Q2.Api.Features.Challenges;

/// <summary>The prompt itself.</summary>
/// <param name="ExpiresAt">
/// The end of its local day, exclusive. What the room counts down to.
/// </param>
public sealed record ChallengeResponse(
    Guid Id,
    string Prompt,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// One contribution, as this viewer is allowed to see it.
/// </summary>
/// <param name="ImageId">
/// <c>null</c> while the room is covered. Not "sent but hidden": a viewer who
/// has not contributed never receives the id at all, so there is nothing for a
/// browser to un-blur — see <see cref="Challenge.RevealsTo"/>.
/// </param>
/// <param name="CapturedInApp">
/// Whether the camera took it. Null-ish cases do not arise: it is only sent
/// with the picture it describes.
/// </param>
/// <remarks>
/// The author travels even while the picture does not, and that is deliberate:
/// seeing who is already in is the reason to join, and it is the half of the
/// room that costs nobody anything.
/// </remarks>
public sealed record ChallengeEntryResponse(
    Guid Id,
    PersonSummary Author,
    Guid? ImageId,
    bool CapturedInApp,
    DateTimeOffset CreatedAt,
    bool IsMine,
    IReadOnlyList<ReactionSummaryResponse> Reactions);

/// <summary>
/// The room, composed for exactly one viewer.
/// </summary>
/// <param name="Entries">
/// Contributions by <em>this viewer's friends</em>, oldest first — the room
/// fills up over the day, like a chat. Never anybody else's: the scoping is in
/// the service, so no screen can widen it by forgetting a filter.
/// </param>
/// <param name="FriendCount">
/// How many friends the viewer has, which is the denominator in "3 von 12
/// dabei". Without it the numerator says nothing.
/// </param>
/// <param name="IsRevealed">
/// Whether the pictures are in this payload. False until the viewer has
/// contributed.
/// </param>
public sealed record ChallengeRoomResponse(
    ChallengeResponse Challenge,
    ChallengeEntryResponse? OwnEntry,
    IReadOnlyList<ChallengeEntryResponse> Entries,
    int FriendCount,
    bool IsRevealed);

/// <summary>
/// What is on today — possibly nothing.
/// </summary>
/// <remarks>
/// A wrapper rather than a 204 or a naked <c>null</c> body, because "no
/// challenge today" is an ordinary state and not an error or an empty response.
/// A typed client gets one nullable property to check; the alternatives make it
/// depend on how its fetch layer treats a body that is not there.
/// </remarks>
public sealed record ChallengeTodayResponse(ChallengeRoomResponse? Room);

/// <summary>One of your own contributions, in the archive, with its prompt.</summary>
/// <remarks>
/// The prompt is not optional context. A photograph of a desk says nothing in
/// six months; "zeig deinen Arbeitsplatz" does.
/// </remarks>
public sealed record ChallengeArchiveEntryResponse(
    ChallengeResponse Challenge,
    ChallengeEntryResponse Entry);

/// <summary>Request body for contributing.</summary>
/// <remarks>
/// Nullable so an empty body produces a field error rather than a binding
/// failure, like every other request in this API.
/// </remarks>
public sealed record SubmitChallengeEntryRequest(Guid? ImageId = null, bool CapturedInApp = false);

/// <summary>Request body for reacting to one.</summary>
public sealed record ReactToChallengeEntryRequest(KudosKind? Kind = null);
