using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Proofs;

/// <summary>What became of one photograph.</summary>
/// <remarks>
/// Persisted as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum ProofStatus
{
    /// <summary>Friends are still looking at it, or the deadline has not passed.</summary>
    Voting,

    /// <summary>Believed. It counts towards its window.</summary>
    Confirmed,

    /// <summary>Disputed, with no attempt left. The window is not delivered by it.</summary>
    Rejected,
}

/// <summary>
/// A photograph delivered against a window, and the votes on it.
/// </summary>
/// <remarks>
/// This is the change the whole migration exists for. Until now a window was
/// closed by its owner pressing a button — the self-report the source project
/// was built to replace. From here it is closed by other people believing a
/// picture, and that is a different product: it can be refused, it can be
/// retried once, and it can cost somebody their streak.
///
/// The deadline is stored rather than computed on read. A vote that started
/// last night has a fixed end, and deriving it from "now minus twelve hours"
/// would quietly move every open vote whenever the constant changed.
///
/// <see cref="Source"/> is recorded but never enforced. A picture taken
/// through the camera is the only one whose timing means anything — a file
/// chosen from a gallery can be any age — so friends are told which they are
/// looking at and left to draw their own conclusion. Refusing screenshots
/// outright would lock out every commitment that only shows on a screen, and
/// every phone whose camera permission was once denied
/// (docs/adr/0036-live-or-screenshot.md).
///
/// It is still called a photograph, but since #124 and #125 it may be more
/// than one: the main picture can be a short clip (<see cref="Media"/>, with a
/// still to show before it plays), and beside it there can be the front
/// camera's picture of whoever took it (<see cref="InsetImageId"/>) — or, where
/// both cameras filmed at once, the front camera's own clip (#151). Both only
/// ever come from the app's camera, which is why either one requires
/// <see cref="ProofSource.Live"/> (docs/adr/0060-video-and-both-cameras.md).
/// </remarks>
public sealed class ProofPhoto
{
    private readonly List<ProofVote> _votes = [];
    private readonly List<ProofReaction> _reactions = [];

    // EF Core materialisation only.
    private ProofPhoto()
    {
    }

    private ProofPhoto(
        Guid id,
        Guid goalInstanceId,
        Guid uploaderPersonId,
        Guid imageId,
        int attempt,
        ProofSource source,
        ProofParts parts,
        DateTimeOffset createdAt,
        DateTimeOffset votingDeadline)
    {
        Id = id;
        GoalInstanceId = goalInstanceId;
        UploaderPersonId = uploaderPersonId;
        ImageId = imageId;
        Attempt = attempt;
        Source = source;
        Media = parts.Media;
        PosterImageId = parts.PosterImageId;
        InsetImageId = parts.InsetImageId;
        InsetMedia = parts.InsetImageId is null ? null : parts.InsetMedia;
        InsetPosterImageId = parts.InsetPosterImageId;
        CreatedAt = createdAt;
        VotingDeadline = votingDeadline;
    }

    public Guid Id { get; private set; }

    /// <summary>The window this is delivered into.</summary>
    public Guid GoalInstanceId { get; private set; }

    /// <summary>Who delivered it. Always the goal's owner, and never a voter.</summary>
    public Guid UploaderPersonId { get; private set; }

    /// <summary>
    /// The stored picture or clip. Its bytes are reached through the image
    /// endpoint, never from here.
    /// </summary>
    public Guid ImageId { get; private set; }

    /// <summary>Whether <see cref="ImageId"/> is a photograph or a clip. Set once, at upload.</summary>
    public MediaKind Media { get; private set; }

    /// <summary>
    /// For a clip: a still from it, shown before it plays and wherever a clip
    /// cannot play — a tile in a grid. Taken by the phone while recording,
    /// because the server never decodes video. Null for a photograph.
    /// </summary>
    public Guid? PosterImageId { get; private set; }

    /// <summary>
    /// The front camera's picture, taken with the main one: who was there
    /// (#125). Null when only one camera was used.
    /// </summary>
    public Guid? InsetImageId { get; private set; }

    /// <summary>
    /// Whether <see cref="InsetImageId"/> is a photograph or a clip filmed at
    /// the same moment as the main one (#151). Null without a front picture.
    /// </summary>
    public MediaKind? InsetMedia { get; private set; }

    /// <summary>For a front clip: a still from it, as <see cref="PosterImageId"/> is for the main one.</summary>
    public Guid? InsetPosterImageId { get; private set; }

    public ProofStatus Status { get; private set; } = ProofStatus.Voting;

    /// <summary>Which delivery this is, counting from one.</summary>
    public int Attempt { get; private set; }

    /// <summary>When the vote closes. Fixed at upload.</summary>
    public DateTimeOffset VotingDeadline { get; private set; }

    /// <summary>
    /// Whether the camera took it or it was picked from the phone's pictures.
    /// Set once, at upload, and never changed: there is no setter to reach.
    /// </summary>
    public ProofSource Source { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When it was decided, either way. Null while the vote runs.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    public IReadOnlyList<ProofVote> Votes => _votes;

    public IReadOnlyList<ProofReaction> Reactions => _reactions;

    /// <summary>The values cast so far, which is all the rules need.</summary>
    public IReadOnlyCollection<VoteValue> CastValues => [.. _votes.Select(vote => vote.Value)];

    public int ConfirmCount => _votes.Count(vote => vote.Value == VoteValue.Confirm);

    public int DoubtCount => _votes.Count(vote => vote.Value == VoteValue.Doubt);

    /// <summary>True once the deadline has passed and nobody has closed it yet.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => Status == ProofStatus.Voting && now > VotingDeadline;

    /// <summary>
    /// Records a delivery. <paramref name="attempt"/> is 1 for the first and 2
    /// for the one allowed after a dispute.
    /// </summary>
    /// <param name="parts">
    /// Whether the main picture is a clip, its still, and the front camera's
    /// picture or clip. Left out, it is a single photograph.
    /// </param>
    /// <exception cref="DomainValidationException">
    /// The attempt is outside what the rules allow, or the parts do not fit
    /// together.
    /// </exception>
    public static ProofPhoto Create(
        Guid id,
        Guid goalInstanceId,
        Guid uploaderPersonId,
        Guid imageId,
        int attempt,
        ProofSource source,
        DateTimeOffset createdAt,
        ProofParts? parts = null)
    {
        parts ??= ProofParts.Photo;

        if (attempt < 1 || attempt > ProofVoting.MaxAttempts)
        {
            throw new DomainValidationException(
                nameof(Attempt),
                $"A window may be proved at most {ProofVoting.MaxAttempts} times.");
        }

        if (imageId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(ImageId), "A proof needs a photograph.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new DomainValidationException(nameof(Source), "That is not a way a picture can arrive.");
        }

        parts.Validate(imageId, source);

        return new ProofPhoto(
            id,
            goalInstanceId,
            uploaderPersonId,
            imageId,
            attempt,
            source,
            parts,
            createdAt,
            createdAt.AddHours(ProofVoting.VotingWindowHours));
    }

    /// <summary>
    /// Records one person's vote, or changes the one they cast. Returns false
    /// when it is not theirs to cast or the vote has closed.
    /// </summary>
    /// <remarks>
    /// One vote per person, which they may change while the vote runs. It was
    /// once final, on the argument that a vote revised after seeing the tally is
    /// a negotiation. What that missed is the far commoner case: a swipe in the
    /// wrong direction, or a second look at the picture in its conversation. A
    /// changed vote is still one vote, it still counts once, and the moment the
    /// photograph is decided it is fixed — so the verdict, the streak and the
    /// balance never move after the fact (ADR 0030).
    ///
    /// The uploader is refused here rather than in the service because "you
    /// cannot vouch for yourself" is what the type is *for*.
    /// </remarks>
    /// <param name="reason">With a doubt, what was missing — optional, and dropped with a confirmation.</param>
    public bool CastVote(Guid id, Guid voterPersonId, VoteValue value, DateTimeOffset now, DoubtReason? reason = null)
    {
        if (Status != ProofStatus.Voting || voterPersonId == UploaderPersonId)
        {
            return false;
        }

        var existing = _votes.FirstOrDefault(vote => vote.VoterPersonId == voterPersonId);

        if (existing is not null)
        {
            existing.Change(value, now, reason);
            return true;
        }

        _votes.Add(new ProofVote(id, Id, voterPersonId, value, now, reason));
        return true;
    }

    /// <summary>What this person said was missing, if they doubted it and said.</summary>
    public DoubtReason? DoubtReasonOf(Guid personId) =>
        _votes.FirstOrDefault(vote => vote.VoterPersonId == personId)?.Reason;

    /// <summary>
    /// The reasons doubters gave, counted, most given first (#111). Counts and
    /// nothing else: which voter gave which is exactly what this never says.
    /// </summary>
    public IReadOnlyList<(DoubtReason Reason, int Count)> DoubtReasons =>
    [
        .. _votes
            .Where(vote => vote.Value == VoteValue.Doubt && vote.Reason is not null)
            .GroupBy(vote => vote.Reason!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (group.Key, group.Count())),
    ];

    /// <summary>What this person voted, if they did.</summary>
    public VoteValue? VoteOf(Guid personId) =>
        _votes.FirstOrDefault(vote => vote.VoterPersonId == personId)?.Value;

    /// <summary>Who confirmed it. Never who doubted it — see <see cref="ProofVote"/>.</summary>
    public IEnumerable<Guid> ConfirmedBy =>
        _votes.Where(vote => vote.Value == VoteValue.Confirm).Select(vote => vote.VoterPersonId);

    /// <summary>
    /// Adds or removes one person's reaction. Returns the kind now standing, or
    /// null when it was taken back.
    /// </summary>
    /// <remarks>
    /// One reaction per person, and tapping the same one again takes it back —
    /// the same behaviour kudos already has, because it is the one people
    /// expect and because it is the only way to undo a mistap.
    ///
    /// A reaction stays possible after the vote has closed, and that is
    /// deliberate: applauding somebody's finished week is the point. What must
    /// never become possible is the opposite, which is why all three kinds are
    /// approving — a rejected proof simply gets no reaction surface at all
    /// ("kein Nachtreten").
    /// </remarks>
    public KudosKind? ToggleReaction(Guid id, Guid personId, KudosKind kind)
    {
        var existing = _reactions.FirstOrDefault(reaction => reaction.PersonId == personId);

        if (existing is not null)
        {
            _reactions.Remove(existing);

            if (existing.Kind == kind)
            {
                return null;
            }
        }

        _reactions.Add(new ProofReaction(id, Id, personId, kind));
        return kind;
    }

    /// <summary>
    /// Closes the vote. Returns false when it was already closed, so running
    /// the maintenance job twice does nothing the second time.
    /// </summary>
    public bool Resolve(VotingResult result, DateTimeOffset now)
    {
        if (Status != ProofStatus.Voting || result == VotingResult.Running)
        {
            return false;
        }

        // A retry is a rejection of *this* photograph; whether another may
        // follow is the window's business, not this one's.
        Status = result == VotingResult.Confirmed ? ProofStatus.Confirmed : ProofStatus.Rejected;
        ResolvedAt = now;
        return true;
    }
}

/// <summary>
/// One person's verdict on one photograph.
/// </summary>
/// <remarks>
/// Named <c>ProofVote</c> rather than <c>Vote</c> because a bare "vote" will
/// mean something else the moment this product has anything else to vote on.
///
/// The row holds who cast it, and that is unavoidable — it is what stops
/// somebody voting twice. What matters is that the *API* never gives the name
/// of a doubter back out: doubting is publicly questioning a friend, and if it
/// were attributable most people would confirm out of politeness and the whole
/// check would be worthless. Confirmations are named, because agreeing costs
/// nothing to admit. The rule lives in the contract mapping, and there is a
/// test that the names are absent.
/// </remarks>
public sealed class ProofVote
{
    // EF Core materialisation only.
    private ProofVote()
    {
    }

    internal ProofVote(Guid id, Guid proofPhotoId, Guid voterPersonId, VoteValue value, DateTimeOffset castAt, DoubtReason? reason = null)
    {
        Id = id;
        ProofPhotoId = proofPhotoId;
        VoterPersonId = voterPersonId;
        Value = value;
        CastAt = castAt;
        Reason = value == VoteValue.Doubt ? reason : null;
    }

    public Guid Id { get; private set; }

    public Guid ProofPhotoId { get; private set; }

    public Guid VoterPersonId { get; private set; }

    public VoteValue Value { get; private set; }

    /// <summary>When it was last cast — changing a vote moves this too.</summary>
    public DateTimeOffset CastAt { get; private set; }

    /// <summary>
    /// What a doubter said was missing, if they said (#111). Only ever on a
    /// doubt, and only ever counted for the owner — never with the voter's
    /// name, which is why it lives on the row that already cannot be shown.
    /// </summary>
    public DoubtReason? Reason { get; private set; }

    /// <summary>Replaces the verdict. Only <see cref="ProofPhoto.CastVote"/> decides whether that is allowed.</summary>
    internal void Change(VoteValue value, DateTimeOffset now, DoubtReason? reason = null)
    {
        Value = value;
        CastAt = now;
        Reason = value == VoteValue.Doubt ? reason : null;
    }
}

/// <summary>
/// One person's encouragement on one photograph.
/// </summary>
/// <remarks>
/// Deliberately not the same table as <see cref="MessageReaction"/>, and it is
/// worth saying why, because two near-identical types is normally a smell. They
/// hang off different things and are read in different queries, and merging
/// them means a `TargetId` that is a foreign key to nothing. The migration plan
/// proposed exactly that once the daily challenge brought a third target; it
/// was weighed then and not done, because each table's real foreign key is what
/// lets a reaction go with the thing it hangs off — see ADR 0021.
/// </remarks>
public sealed class ProofReaction
{
    // EF Core materialisation only.
    private ProofReaction()
    {
    }

    internal ProofReaction(Guid id, Guid proofPhotoId, Guid personId, KudosKind kind)
    {
        Id = id;
        ProofPhotoId = proofPhotoId;
        PersonId = personId;
        Kind = kind;
    }

    public Guid Id { get; private set; }

    public Guid ProofPhotoId { get; private set; }

    public Guid PersonId { get; private set; }

    public KudosKind Kind { get; private set; }
}
