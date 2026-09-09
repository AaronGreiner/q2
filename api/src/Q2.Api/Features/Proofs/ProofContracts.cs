using Q2.Api.Features.Chats;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// How a vote stands, as the person asking is allowed to see it.
/// </summary>
/// <param name="ConfirmedBy">
/// Who believed it, by name. Agreeing with a friend costs nothing to admit.
/// </param>
/// <param name="DoubtCount">
/// How many doubted it — a number, never names, and the API does not hold the
/// names back only in this shape: it never selects them at all.
/// </param>
/// <param name="MyVote">
/// What the person asking voted, or null if they have not. Sent so a client
/// never has to work out whether the "confirm" button is still theirs to press.
/// </param>
/// <param name="CanIVote">
/// Whether this is theirs to vote on at all. False for the person who
/// delivered it, for anybody not on the goal, and once they have voted.
/// </param>
/// <remarks>
/// **Doubt is anonymous, and that is a product rule rather than a display
/// choice.** Doubting is publicly calling a friend's word into question; if it
/// carried a name, most people would confirm out of politeness and the whole
/// check would be theatre. It is enforced here — in the one place the response
/// is built — and there is an integration test that no doubter's id or name
/// appears anywhere in the payload.
/// </remarks>
public sealed record VoteSummaryResponse(
    int ConfirmCount,
    int DoubtCount,
    IReadOnlyList<PersonSummary> ConfirmedBy,
    VoteValue? MyVote,
    bool CanIVote);

/// <summary>A photograph delivered against a window, as somebody may see it.</summary>
/// <param name="ImageId">
/// Fetched from <c>/api/images/{id}</c>, which checks who is asking. The bytes
/// never travel in this response.
/// </param>
/// <param name="CapturedInApp">
/// Whether the camera took it. A picture chosen from a gallery can be any age,
/// so the client says which it is looking at rather than pretending they are
/// the same kind of evidence.
/// </param>
/// <param name="ExpiresAt">When the vote closes and the votes cast decide it.</param>
public sealed record ProofResponse(
    Guid Id,
    Guid GoalId,
    Guid GoalInstanceId,
    PersonSummary Uploader,
    Guid ImageId,
    ProofStatus Status,
    int Attempt,
    int AttemptsLeft,
    bool CapturedInApp,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    VoteSummaryResponse Votes,
    IReadOnlyList<ReactionSummaryResponse> Reactions);

/// <summary>Reactions on a photograph, rolled up.</summary>
/// <remarks>
/// Deliberately separate from the vote. A reaction is encouragement between
/// friends; a vote is a verdict. Somebody who applauds has not confirmed
/// anything, and somebody who confirms need not applaud.
///
/// All three kinds are approving, and that is what lets them be offered
/// anywhere without ever becoming a way to kick somebody who is already down.
/// </remarks>
public sealed record ReactionSummaryResponse(KudosKind Kind, int Count, bool IsMine);

/// <summary>One card in the swipe feed.</summary>
/// <param name="GoalTitle">
/// What was promised, so the picture can be judged against something.
/// </param>
public sealed record FeedProofResponse(ProofResponse Proof, string GoalTitle, string GoalIcon);

/// <summary>Request body for delivering a photograph.</summary>
/// <remarks>
/// Nullable so an empty body produces a field error rather than a binding
/// failure, like every other request in this API.
/// </remarks>
public sealed record SubmitProofRequest(Guid? ImageId = null, bool CapturedInApp = false);

/// <summary>Request body for voting on one.</summary>
public sealed record CastVoteRequest(VoteValue? Value = null);

/// <summary>Request body for reacting to one.</summary>
public sealed record ReactToProofRequest(KudosKind? Kind = null);
