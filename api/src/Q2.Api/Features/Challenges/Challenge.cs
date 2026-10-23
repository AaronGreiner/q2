using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;
using Q2.Api.Features.Settings;
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

    private Challenge(
        Guid id,
        DateOnly day,
        string prompt,
        string promptEnglish,
        DateTimeOffset publishedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? momentEndsAt)
    {
        Id = id;
        Day = day;
        Prompt = prompt;
        PromptEnglish = promptEnglish;
        PublishedAt = publishedAt;
        ExpiresAt = expiresAt;
        MomentEndsAt = momentEndsAt;
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

    /// <summary>The wording of the task, in German — the product's own language.</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>
    /// The same task in English (#105). Null only on a row written before
    /// prompts had to come in both languages, until the queue finds its pair
    /// in the configuration (<see cref="GiveEnglish"/>); such a row reads in
    /// German for everybody.
    /// </summary>
    /// <remarks>
    /// A column beside <see cref="Prompt"/> rather than a table of
    /// translations: there are two languages, and a third is the moment the
    /// frontend's catalogue changes shape too (#11).
    /// </remarks>
    public string? PromptEnglish { get; private set; }

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

    /// <summary>
    /// The end of "the moment": the first stretch after it appears, when it is
    /// the thing everybody does at once (#136). A contribution after it is
    /// still taken until <see cref="ExpiresAt"/>, and said to be late.
    /// </summary>
    /// <remarks>
    /// Null for a challenge without a moment — every row from before #136,
    /// which went live at midnight, and the seeded ones, which stand for a
    /// challenge that is simply running, so no test depends on the hour.
    /// </remarks>
    public DateTimeOffset? MomentEndsAt { get; private set; }

    /// <summary>Whether a contribution made at <paramref name="contributedAt"/> came after the moment.</summary>
    public bool IsLate(DateTimeOffset contributedAt) => MomentEndsAt is { } end && contributedAt > end;

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

    /// <summary>Publishes a prompt for one day, in both languages.</summary>
    /// <exception cref="DomainValidationException">A prompt or the window is not usable.</exception>
    public static Challenge Create(
        Guid id,
        DateOnly day,
        string prompt,
        string promptEnglish,
        DateTimeOffset publishedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? momentEndsAt = null)
    {
        var german = Usable(prompt, nameof(Prompt));
        var english = Usable(promptEnglish, nameof(PromptEnglish));

        if (expiresAt <= publishedAt)
        {
            throw new DomainValidationException(
                nameof(ExpiresAt),
                "A challenge has to end after it starts.");
        }

        // The moment lies inside the day: it cannot end before it began, nor
        // after the challenge does.
        var moment = momentEndsAt is { } end
            ? (end < publishedAt ? publishedAt : end > expiresAt ? expiresAt : end)
            : (DateTimeOffset?)null;

        return new Challenge(id, day, german, english, publishedAt, expiresAt, moment);
    }

    /// <summary>Whether a text is fit to be one language's prompt.</summary>
    public static bool IsUsablePrompt(string? prompt) =>
        prompt?.Trim().Length is >= MinPromptLength and <= MaxPromptLength;

    /// <summary>The prompt as somebody who reads q2 in <paramref name="language"/> is shown it.</summary>
    public string PromptIn(LanguagePreference language) =>
        language == LanguagePreference.English && PromptEnglish is { } english ? english : Prompt;

    /// <summary>
    /// Gives a row from before #105 its English prompt. Returns false when it
    /// already had one, so running the queue twice changes nothing twice.
    /// </summary>
    /// <exception cref="DomainValidationException">The text is not a usable prompt.</exception>
    public bool GiveEnglish(string promptEnglish)
    {
        if (PromptEnglish is not null)
        {
            return false;
        }

        PromptEnglish = Usable(promptEnglish, nameof(PromptEnglish));
        return true;
    }

    private static string Usable(string? prompt, string field)
    {
        if (!IsUsablePrompt(prompt))
        {
            throw new DomainValidationException(
                field,
                $"A prompt is between {MinPromptLength} and {MaxPromptLength} characters.");
        }

        return prompt!.Trim();
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
    /// <param name="parts">A clip, or both cameras, when it was taken with them (#152). Left out, one photograph.</param>
    /// <exception cref="DomainValidationException">The parts do not fit together, or a clip or both cameras did not come live.</exception>
    public ChallengeEntry Contribute(
        Guid id,
        Guid personId,
        Guid imageId,
        ProofSource source,
        DateTimeOffset now,
        MediaParts? parts = null)
    {
        parts ??= MediaParts.Photo;

        if (!IsActiveAt(now))
        {
            throw new DomainValidationException("Challenge", "This challenge is not taking contributions.");
        }

        if (imageId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(ChallengeEntry.ImageId), "A contribution needs a photograph.");
        }

        parts.Validate(imageId, source == ProofSource.Live);

        if (EntryOf(personId) is { } existing)
        {
            existing.Replace(imageId, source, now, parts);
            return existing;
        }

        var entry = new ChallengeEntry(id, Id, personId, imageId, source, now, parts);
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
/// <see cref="Source"/> is recorded for the same reason it is on a proof, and
/// with the same values, and matters slightly more: the prompt asks for a
/// moment, and a picture out of a gallery misses it by definition. It stays a
/// label rather than a refusal — a phone whose camera permission was once
/// denied has to be able to join in too (docs/adr/0036-live-or-screenshot.md).
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
        ProofSource source,
        DateTimeOffset createdAt,
        MediaParts parts)
    {
        Id = id;
        ChallengeId = challengeId;
        PersonId = personId;
        ImageId = imageId;
        Source = source;
        CreatedAt = createdAt;
        Take(parts);
    }

    public Guid Id { get; private set; }

    public Guid ChallengeId { get; private set; }

    /// <summary>Who contributed it.</summary>
    public Guid PersonId { get; private set; }

    /// <summary>The stored picture or clip. Its bytes are reached through the image endpoint, never from here.</summary>
    public Guid ImageId { get; private set; }

    /// <summary>Whether <see cref="ImageId"/> is a photograph or a clip (#152).</summary>
    public MediaKind Media { get; private set; }

    /// <summary>For a clip: a still from it, shown before it plays and on a tile.</summary>
    public Guid? PosterImageId { get; private set; }

    /// <summary>The front camera's picture or clip, when both cameras were used.</summary>
    public Guid? InsetImageId { get; private set; }

    /// <summary>Whether <see cref="InsetImageId"/> is a photograph or a clip. Null without one.</summary>
    public MediaKind? InsetMedia { get; private set; }

    /// <summary>For a front clip: a still from it.</summary>
    public Guid? InsetPosterImageId { get; private set; }

    /// <summary>The parts beside the main picture, as <see cref="MediaParts"/> checks them.</summary>
    public MediaParts Parts => new(Media, PosterImageId, InsetImageId, InsetMedia ?? MediaKind.Photo, InsetPosterImageId);

    /// <summary>Every picture it carries: the main one and its parts.</summary>
    public IEnumerable<Guid> PictureIds => Parts.OtherIds.Prepend(ImageId);

    /// <summary>
    /// Which route the app used: the camera, or a picture from the phone.
    /// <see cref="ProofSource.Unknown"/> for contributions from before #64,
    /// whose flag was true for every picture.
    /// </summary>
    public ProofSource Source { get; private set; }

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
    internal void Replace(Guid imageId, ProofSource source, DateTimeOffset now, MediaParts parts)
    {
        ImageId = imageId;
        Source = source;
        CreatedAt = now;
        Take(parts);
        _reactions.Clear();
    }

    private void Take(MediaParts parts)
    {
        Media = parts.Media;
        PosterImageId = parts.PosterImageId;
        InsetImageId = parts.InsetImageId;
        InsetMedia = parts.StoredInsetMedia;
        InsetPosterImageId = parts.InsetPosterImageId;
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
