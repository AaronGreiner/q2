using Q2.Api.Features.Chats;
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
/// <see cref="CapturedInApp"/> is recorded but never enforced. A picture taken
/// through the camera is the only one whose timing means anything — a file
/// chosen from a gallery can be any age — so friends are told which they are
/// looking at and left to draw their own conclusion. Refusing gallery uploads
/// outright would lock out every phone whose camera permission was once denied,
/// and this app has to work for them too.
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
        bool capturedInApp,
        DateTimeOffset createdAt,
        DateTimeOffset votingDeadline)
    {
        Id = id;
        GoalInstanceId = goalInstanceId;
        UploaderPersonId = uploaderPersonId;
        ImageId = imageId;
        Attempt = attempt;
        CapturedInApp = capturedInApp;
        CreatedAt = createdAt;
        VotingDeadline = votingDeadline;
    }

    public Guid Id { get; private set; }

    /// <summary>The window this is delivered into.</summary>
    public Guid GoalInstanceId { get; private set; }

    /// <summary>Who delivered it. Always the goal's owner, and never a voter.</summary>
    public Guid UploaderPersonId { get; private set; }

    /// <summary>The stored image. Its bytes are reached through the image endpoint, never from here.</summary>
    public Guid ImageId { get; private set; }

    public ProofStatus Status { get; private set; } = ProofStatus.Voting;

    /// <summary>Which delivery this is, counting from one.</summary>
    public int Attempt { get; private set; }

    /// <summary>When the vote closes. Fixed at upload.</summary>
    public DateTimeOffset VotingDeadline { get; private set; }

    /// <summary>True when the camera took it rather than the file picker.</summary>
    public bool CapturedInApp { get; private set; }

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
    /// <exception cref="DomainValidationException">The attempt is outside what the rules allow.</exception>
    public static ProofPhoto Create(
        Guid id,
        Guid goalInstanceId,
        Guid uploaderPersonId,
        Guid imageId,
        int attempt,
        bool capturedInApp,
        DateTimeOffset createdAt)
    {
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

        return new ProofPhoto(
            id,
            goalInstanceId,
            uploaderPersonId,
            imageId,
            attempt,
            capturedInApp,
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
    /// balance never move after the fact (ADR 0028).
    ///
    /// The uploader is refused here rather than in the service because "you
    /// cannot vouch for yourself" is what the type is *for*.
    /// </remarks>
    public bool CastVote(Guid id, Guid voterPersonId, VoteValue value, DateTimeOffset now)
    {
        if (Status != ProofStatus.Voting || voterPersonId == UploaderPersonId)
        {
            return false;
        }

        var existing = _votes.FirstOrDefault(vote => vote.VoterPersonId == voterPersonId);

        if (existing is not null)
        {
            existing.Change(value, now);
            return true;
        }

        _votes.Add(new ProofVote(id, Id, voterPersonId, value, now));
        return true;
    }

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

    internal ProofVote(Guid id, Guid proofPhotoId, Guid voterPersonId, VoteValue value, DateTimeOffset castAt)
    {
        Id = id;
        ProofPhotoId = proofPhotoId;
        VoterPersonId = voterPersonId;
        Value = value;
        CastAt = castAt;
    }

    public Guid Id { get; private set; }

    public Guid ProofPhotoId { get; private set; }

    public Guid VoterPersonId { get; private set; }

    public VoteValue Value { get; private set; }

    /// <summary>When it was last cast — changing a vote moves this too.</summary>
    public DateTimeOffset CastAt { get; private set; }

    /// <summary>Replaces the verdict. Only <see cref="ProofPhoto.CastVote"/> decides whether that is allowed.</summary>
    internal void Change(VoteValue value, DateTimeOffset now)
    {
        Value = value;
        CastAt = now;
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
