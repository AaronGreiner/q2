using Q2.Api.Features.Chats;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Challenges;

/// <summary>
/// One day's prompt, the same one for everybody.
/// </summary>
/// <remarks>
/// The light counterweight to the rest of q2, and every rule about it follows
/// from that one sentence. A challenge pays into no streak, knows no "missed"
/// and appears in no balance: whoever joins in gains something, whoever sits it
/// out loses nothing.
///
/// From which follows, immediately, that there is <em>no vote</em>. Doubting
/// exists here for one purpose — to keep a streak from being won with a
/// borrowed photograph ([0018](../../../../docs/adr/0018-proof-and-vote.md)) —
/// and with no streak there is nothing to protect. A contribution is seen and
/// applauded, not examined.
///
/// It is deliberately <em>not</em> a goal. A goal has one owner who delivers
/// and invited friends who judge; a challenge has any number of people who
/// deliver and nobody who judges. Modelling it as a goal with the vote switched
/// off would put a "you may not vote here" branch through every rule the vote
/// has.
///
/// **A challenge is one row for everybody, on the deployment's clock.** Windows
/// are counted in each person's own zone, because a deadline that decides
/// somebody's streak has to be theirs; a challenge decides nothing, and "the
/// task of the day, for all of us at the same time" is worth more than a prompt
/// that turns over at a different moment for each reader. The zone is
/// <c>Q2:TimeZone</c> — see <see cref="ChallengeQueue"/>.
/// </remarks>
public sealed class Challenge
{
    /// <summary>The shortest prompt worth publishing.</summary>
    public const int MinPromptLength = 3;

    /// <summary>
    /// The longest. A prompt is read at a glance on a phone and stands at the
    /// top of the room in large type; beyond this it stops being a prompt and
    /// becomes an instruction manual.
    /// </summary>
    public const int MaxPromptLength = 160;

    private readonly List<ChallengeEntry> _entries = [];

    // EF Core materialisation only.
    private Challenge()
    {
    }

    private Challenge(Guid id, DateOnly day, string prompt, DateTimeOffset publishedAt, DateTimeOffset expiresAt)
    {
        Id = id;
        Day = day;
        Prompt = prompt;
        PublishedAt = publishedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The local day this belongs to, in the deployment's zone.
    /// </summary>
    /// <remarks>
    /// Stored beside the two instants rather than derived from them, for the
    /// same reason <see cref="Q2.Api.Features.Goals.GoalInstance.StartsOn"/> is:
    /// it is the key the queue is keyed on, and deriving it back out of an
    /// instant would need the zone at every read. It is also what the unique
    /// index hangs off — one prompt per day is an invariant, not a convention.
    /// </remarks>
    public DateOnly Day { get; private set; }

    /// <summary>The wording of the task.</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>
    /// When it goes live. In the future for everything still queued.
    /// </summary>
    /// <remarks>
    /// The queue is rows, not a plan: a challenge a week out already exists,
    /// with its prompt and its day, and becomes the current one by the clock
    /// passing it. That is what makes a daily editorial shift unnecessary —
    /// nothing has to happen at midnight for there to be a challenge in the
    /// morning. From stage 9 this is also when the push goes out.
    /// </remarks>
    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>The end of its day — the last moment a contribution is taken.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the notification for it went out. Null until it has.</summary>
    /// <remarks>
    /// A challenge exists days before it runs, so "published" and "announced"
    /// are different moments and only the second one is worth waking a phone
    /// for. Recorded on the row rather than inferred from the clock, because
    /// the job that sends it runs hourly and two passes must not mean two
    /// notifications — the same idempotence every other background pass in q2
    /// has.
    /// </remarks>
    public DateTimeOffset? AnnouncedAt { get; private set; }

    public IReadOnlyList<ChallengeEntry> Entries => _entries;

    /// <summary>Publishes a prompt for one day.</summary>
    /// <exception cref="DomainValidationException">The prompt or the window is not usable.</exception>
    public static Challenge Create(
        Guid id,
        DateOnly day,
        string prompt,
        DateTimeOffset publishedAt,
        DateTimeOffset expiresAt)
    {
        var trimmed = (prompt ?? string.Empty).Trim();

        if (trimmed.Length < MinPromptLength || trimmed.Length > MaxPromptLength)
        {
            throw new DomainValidationException(
                nameof(Prompt),
                $"A prompt is between {MinPromptLength} and {MaxPromptLength} characters.");
        }

        if (expiresAt <= publishedAt)
        {
            throw new DomainValidationException(
                nameof(ExpiresAt),
                "A challenge has to end after it starts.");
        }

        return new Challenge(id, day, trimmed, publishedAt, expiresAt);
    }

    /// <summary>Whether it is running: published, and not yet over.</summary>
    public bool IsActiveAt(DateTimeOffset now) => PublishedAt <= now && now < ExpiresAt;

    /// <summary>
    /// Marks it as announced. Returns false when it already was, so running the
    /// job twice sends one notification.
    /// </summary>
    public bool Announce(DateTimeOffset now)
    {
        if (AnnouncedAt is not null)
        {
            return false;
        }

        AnnouncedAt = now;
        return true;
    }

    /// <summary>This person's contribution, if they have made one.</summary>
    public ChallengeEntry? EntryOf(Guid personId) =>
        _entries.FirstOrDefault(entry => entry.PersonId == personId);

    /// <summary>
    /// Whether this person has earned the sight of everybody else's.
    /// </summary>
    /// <remarks>
    /// The reciprocity rule, named once. Without the hurdle the room would fill
    /// with spectators — a few people showing themselves to a silent majority,
    /// which is exactly the gradient the rest of the product avoids.
    ///
    /// It is a rule and not a display choice, so it is answered here and obeyed
    /// by <see cref="ChallengeService"/> not <em>selecting</em> the pictures at
    /// all. A blur in the browser over bytes that were sent anyway would be a
    /// curtain with a gap in it.
    /// </remarks>
    public bool RevealsTo(Guid personId) => EntryOf(personId) is not null;

    /// <summary>
    /// Takes somebody's contribution, replacing the one they had.
    /// </summary>
    /// <remarks>
    /// Exactly one per person, and a second picture replaces the first rather
    /// than adding to a gallery: the challenge is a moment, not a collection.
    /// </remarks>
    /// <exception cref="DomainValidationException">It is not running.</exception>
    public ChallengeEntry Contribute(
        Guid id,
        Guid personId,
        Guid imageId,
        bool capturedInApp,
        DateTimeOffset now)
    {
        if (!IsActiveAt(now))
        {
            throw new DomainValidationException("Challenge", "This challenge is not taking contributions.");
        }

        if (imageId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(ChallengeEntry.ImageId), "A contribution needs a photograph.");
        }

        if (EntryOf(personId) is { } existing)
        {
            existing.Replace(imageId, capturedInApp, now);
            return existing;
        }

        var entry = new ChallengeEntry(id, Id, personId, imageId, capturedInApp, now);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Takes somebody's contribution back. Returns it, or null if there was
    /// none, so the caller can dispose of the picture that is now unreferenced.
    /// </summary>
    /// <remarks>
    /// Withdrawing covers the friends' pictures again — the reciprocity rule
    /// reads the same in both directions, and an exit that kept the view would
    /// be the spectator's route back in.
    /// </remarks>
    public ChallengeEntry? Withdraw(Guid personId)
    {
        if (EntryOf(personId) is not { } entry)
        {
            return null;
        }

        _entries.Remove(entry);
        return entry;
    }
}

/// <summary>
/// One person's contribution to one day's prompt.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Q2.Api.Features.Proofs.ProofPhoto"/>. That
/// carries a status, a second attempt and a voting deadline, and all three
/// would be meaningless here: there is nothing to win and so nothing to check.
/// What is left is the picture, who took it, and encouragement.
///
/// <see cref="CapturedInApp"/> is recorded for the same reason it is on a
/// proof, and matters slightly more: the prompt asks for a moment, and a
/// picture out of a gallery misses it by definition. It stays a label rather
/// than a refusal — a phone whose camera permission was once denied has to be
/// able to join in too.
/// </remarks>
public sealed class ChallengeEntry
{
    private readonly List<ChallengeReaction> _reactions = [];

    // EF Core materialisation only.
    private ChallengeEntry()
    {
    }

    internal ChallengeEntry(
        Guid id,
        Guid challengeId,
        Guid personId,
        Guid imageId,
        bool capturedInApp,
        DateTimeOffset createdAt)
    {
        Id = id;
        ChallengeId = challengeId;
        PersonId = personId;
        ImageId = imageId;
        CapturedInApp = capturedInApp;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ChallengeId { get; private set; }

    /// <summary>Who contributed it.</summary>
    public Guid PersonId { get; private set; }

    /// <summary>The stored picture. Its bytes are reached through the image endpoint, never from here.</summary>
    public Guid ImageId { get; private set; }

    /// <summary>True when the camera took it rather than the file picker.</summary>
    public bool CapturedInApp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ChallengeReaction> Reactions => _reactions;

    /// <summary>
    /// Puts a different picture in its place.
    /// </summary>
    /// <remarks>
    /// The applause goes with the old picture. Carrying it over would mean
    /// somebody's "stark" standing under a photograph they never saw, which is
    /// the one way a reaction here could become dishonest.
    /// </remarks>
    internal void Replace(Guid imageId, bool capturedInApp, DateTimeOffset now)
    {
        ImageId = imageId;
        CapturedInApp = capturedInApp;
        CreatedAt = now;
        _reactions.Clear();
    }

    /// <summary>
    /// Adds or takes back one person's reaction. Returns the kind now standing,
    /// or null when it was taken back.
    /// </summary>
    /// <remarks>
    /// One per person, and the same kind twice takes it back — the behaviour
    /// kudos and a proof reaction already have. All three kinds are approving,
    /// which is what lets a reaction be offered here at all: there is no verdict
    /// on a challenge, so a reaction that could be negative would be the only
    /// way to be unpleasant in the room.
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

        _reactions.Add(new ChallengeReaction(id, Id, personId, kind));
        return kind;
    }
}

/// <summary>One person's encouragement on one contribution.</summary>
/// <remarks>
/// A third reaction table, and the migration plan proposed the opposite: one
/// <c>Reaction</c> row with a bare <c>TargetId</c>, once a third kind of target
/// appeared. It was not taken, and the reason is worth writing down, because
/// <see cref="Q2.Api.Features.Proofs.ProofReaction"/> says here is where it
/// would happen.
///
/// A shared <c>TargetId</c> is a foreign key to nothing. Today each of the
/// three tables has a real one and cascades with the thing it hangs off, so a
/// deleted message, a deleted photograph and a withdrawn contribution take
/// their reactions with them without anybody remembering to. One shared table
/// would trade that for three manual clean-up paths and a column the database
/// cannot check — a poor trade for one table fewer, in a codebase whose rule is
/// that invariants live in the model. See
/// [0021](../../../../docs/adr/0021-daily-challenge.md).
/// </remarks>
public sealed class ChallengeReaction
{
    // EF Core materialisation only.
    private ChallengeReaction()
    {
    }

    internal ChallengeReaction(Guid id, Guid challengeEntryId, Guid personId, KudosKind kind)
    {
        Id = id;
        ChallengeEntryId = challengeEntryId;
        PersonId = personId;
        Kind = kind;
    }

    public Guid Id { get; private set; }

    public Guid ChallengeEntryId { get; private set; }

    public Guid PersonId { get; private set; }

    public KudosKind Kind { get; private set; }
}
