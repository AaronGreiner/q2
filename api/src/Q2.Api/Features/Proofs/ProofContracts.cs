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
/// Whether this is theirs to vote on — to cast a vote, or, with
/// <paramref name="MyVote"/> set, to change it. False for the person who
/// delivered it, for anybody not on the goal, and once the vote has closed or
/// its deadline has passed.
/// </param>
/// <param name="DoubtCanRefuse">
/// Whether doubt can refuse this photograph at all. False when only one person
/// may vote on it: refusing takes two who doubt (<see cref="ProofVoting.DoubtCanRefuse"/>,
/// #97), and a lone checker is told so on the card instead of being left to
/// think their doubt decides it.
/// </param>
/// <remarks>
/// **Doubt is anonymous, and that is a product rule rather than a display
/// choice.** Doubting is publicly calling a friend's word into question; if it
/// carried a name, most people would confirm out of politeness and the whole
/// check would be theatre. It is enforced here — in the one place the response
/// is built — and there is an integration test that no doubter's id or name
/// appears anywhere in the payload.
/// </remarks>
/// <param name="MyDoubtReason">What the reader said was missing, when they doubted and said (#111). Their own, so they can change it.</param>
/// <param name="DoubtReasons">
/// What doubters said was missing, counted, most given first — for the
/// photograph's owner only, and empty for everybody else (#111). Never who
/// said which: a count is all this ever is.
/// </param>
public sealed record VoteSummaryResponse(
    int ConfirmCount,
    int DoubtCount,
    IReadOnlyList<PersonSummary> ConfirmedBy,
    VoteValue? MyVote,
    bool CanIVote,
    bool DoubtCanRefuse,
    DoubtReason? MyDoubtReason,
    IReadOnlyList<DoubtReasonCountResponse> DoubtReasons);

/// <summary>One reason doubters gave, and how many gave it.</summary>
public sealed record DoubtReasonCountResponse(DoubtReason Reason, int Count);

/// <summary>A photograph delivered against a window, as somebody may see it.</summary>
/// <param name="ImageId">
/// Fetched from <c>/api/images/{id}</c>, which checks who is asking. The bytes
/// never travel in this response.
/// </param>
/// <param name="Source">
/// Whether the camera took it or it was picked from the phone's pictures. A
/// picture chosen from a gallery can be any age, so every place that shows a
/// proof says which it is rather than pretending they are the same kind of
/// evidence. <see cref="ProofSource.Unknown"/> for proofs from before it was
/// recorded.
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
    ProofSource Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    VoteSummaryResponse Votes,
    IReadOnlyList<ReactionSummaryResponse> Reactions);

/// <summary>What delivering a photograph answers: the photograph, and where it now is.</summary>
/// <param name="ConversationId">
/// The goal's own conversation, where the photograph is shown and voted on —
/// and so where the person who just delivered it should look for it. Null only
/// for a goal nobody else is on that was created before a friend was required:
/// there is nowhere to show it but the goal
/// ([0027](../../../../docs/adr/0027-goal-conversations.md)).
/// </param>
public sealed record DeliveredProofResponse(ProofResponse Proof, Guid? ConversationId);

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
/// <param name="GoalDescription">
/// What counts, in the owner's words — "mindestens 5 km", "ohne Handy" — when
/// they said. The agreement a checker judges the photograph against (#95).
/// </param>
public sealed record FeedProofResponse(ProofResponse Proof, string GoalTitle, string? GoalDescription, string GoalIcon);

/// <summary>One of your own photographs, as your profile's gallery shows it.</summary>
/// <param name="ImageId">Fetched from <c>/api/images/{id}</c>, like every other picture.</param>
/// <param name="Status">
/// Every outcome, rejected included. Nobody but the person who delivered it
/// reads this list, so it is their whole record rather than a highlight reel —
/// and "kein Nachtreten" is about other people, not about looking back at
/// your own week.
/// </param>
/// <param name="GoalTitle">
/// What it was delivered against. A photograph of a desk says nothing in six
/// months; "Schreibtisch aufräumen" does.
/// </param>
/// <param name="Source">Live or screenshot, labelled on the tile like everywhere else.</param>
/// <remarks>
/// Deliberately thin: no votes, no reactions, no deadline. The gallery is a
/// look back, and the vote itself is one tap away on the goal.
/// </remarks>
public sealed record OwnProofResponse(
    Guid Id,
    Guid ImageId,
    ProofStatus Status,
    ProofSource Source,
    DateTimeOffset CreatedAt,
    Guid GoalId,
    string GoalTitle,
    string GoalIcon);

/// <summary>Request body for delivering a photograph.</summary>
/// <param name="Source">
/// The route the app used. Left out, the proof is stored as
/// <see cref="ProofSource.Unknown"/>.
/// </param>
/// <remarks>
/// Nullable so an empty body produces a field error rather than a binding
/// failure, like every other request in this API.
///
/// There was a <c>capturedInApp</c> flag here until #61. Builds already
/// installed from TestFlight still send it, always as true, whatever the
/// picture was; it is ignored like any property the API does not know, and
/// their proofs are stored as unknown rather than believed to be live.
/// </remarks>
public sealed record SubmitProofRequest(Guid? ImageId = null, ProofSource? Source = null);

/// <summary>Request body for answering a spot check: a live photograph, uploaded as a proof image first.</summary>
public sealed record AnswerSpotCheckRequest(Guid? ImageId = null);

/// <summary>Request body for voting on one.</summary>
/// <param name="Reason">With a doubt: what was missing (#111). Optional, anonymous, and ignored with a confirmation.</param>
public sealed record CastVoteRequest(VoteValue? Value = null, DoubtReason? Reason = null);

/// <summary>Request body for reacting to one.</summary>
public sealed record ReactToProofRequest(KudosKind? Kind = null);
