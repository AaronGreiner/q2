using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;

namespace Q2.Api.Features.Challenges;

/// <summary>The prompt itself.</summary>
/// <param name="PublishedAt">The moment it appeared, the same for everybody (#136).</param>
/// <param name="ExpiresAt">
/// The end of its local day, exclusive. The last moment a contribution is taken.
/// </param>
/// <param name="MomentEndsAt">
/// The end of "the moment", the first stretch after it appeared (#136): what
/// the card counts down to while it lasts. After it, a contribution is still
/// taken and is said to be late. Null for a challenge without a moment.
/// </param>
public sealed record ChallengeResponse(
    Guid Id,
    string Prompt,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? MomentEndsAt);

/// <summary>
/// One contribution, as this viewer is allowed to see it.
/// </summary>
/// <param name="ImageId">
/// <c>null</c> while the room is covered. Not "sent but hidden": a viewer who
/// has not contributed never receives the id at all, so there is nothing for a
/// browser to un-blur — see <see cref="Challenge.RevealsTo"/>.
/// </param>
/// <param name="Source">
/// Live, from the phone's pictures, or unknown — the same three values as a
/// proof's. Contributions from before #64 are unknown.
/// </param>
/// <param name="IsLate">Contributed after the moment — taken, and said to be "zu spät" (#136).</param>
/// <param name="Media">Whether it is a photograph or a clip (#152).</param>
/// <param name="PosterImageId">For a clip: its still. Null while the room is covered, like the picture.</param>
/// <param name="InsetImageId">The front camera's picture or clip, when both cameras were used. Null while covered.</param>
/// <param name="InsetMedia">Whether that is a photograph or a clip. Null without one, and while covered.</param>
/// <param name="InsetPosterImageId">For a front clip: its still. Null while covered.</param>
/// <remarks>
/// The author travels even while the picture does not, and that is deliberate:
/// seeing who is already in is the reason to join, and it is the half of the
/// room that costs nobody anything.
/// </remarks>
public sealed record ChallengeEntryResponse(
    Guid Id,
    PersonSummary Author,
    Guid? ImageId,
    ProofSource Source,
    DateTimeOffset CreatedAt,
    bool IsMine,
    IReadOnlyList<ReactionSummaryResponse> Reactions,
    bool IsLate,
    MediaKind Media,
    Guid? PosterImageId,
    Guid? InsetImageId,
    MediaKind? InsetMedia,
    Guid? InsetPosterImageId);

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
/// <param name="Source">
/// The route the app used. Left out, the contribution is stored as
/// <see cref="ProofSource.Unknown"/>.
/// </param>
/// <remarks>
/// Nullable so an empty body produces a field error rather than a binding
/// failure, like every other request in this API.
///
/// There was a <c>capturedInApp</c> flag here until #64, sent as true for every
/// picture, gallery or not. Builds that still send it are treated like a proof
/// from before #61: the flag is ignored, and the contribution is unknown rather
/// than believed to be live.
/// </remarks>
/// <param name="PosterImageId">With a clip, and only then: a still from it.</param>
/// <param name="InsetImageId">The front camera's picture or clip, when both cameras were used (#152).</param>
/// <param name="InsetPosterImageId">With a front clip, and only then: a still from it.</param>
public sealed record SubmitChallengeEntryRequest(
    Guid? ImageId = null,
    ProofSource? Source = null,
    Guid? PosterImageId = null,
    Guid? InsetImageId = null,
    Guid? InsetPosterImageId = null);

/// <summary>Request body for reacting to one.</summary>
public sealed record ReactToChallengeEntryRequest(KudosKind? Kind = null);
