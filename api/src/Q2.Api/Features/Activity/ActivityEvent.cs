using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Activity;

/// <summary>
/// What kind of thing happened. The feed composes its sentence from this plus
/// <see cref="ActivityEvent.Subject"/> and <see cref="ActivityEvent.Amount"/>,
/// rather than storing a ready-made line of text — a stored sentence could not
/// be translated, and the app already ships in more than one language.
/// </summary>
public enum ActivityKind
{
    /// <summary>Ticked a task off. <c>Subject</c> is the task title.</summary>
    TaskCompleted,

    /// <summary>Reached a streak. <c>Amount</c> is the number of days.</summary>
    StreakReached,

    /// <summary>Got further on a goal. <c>Subject</c> is the goal, <c>Amount</c> the percentage.</summary>
    GoalProgress,

    /// <summary>Started something new. <c>Subject</c> is the goal title.</summary>
    GoalCreated,
}

/// <summary>
/// One entry in the friends' activity feed.
/// </summary>
/// <remarks>
/// A recorded fact, not a notification: the feed is read, never delivered, so
/// there is no per-recipient state and nothing to fan out on write.
/// </remarks>
public sealed class ActivityEvent
{
    public const int MaxSubjectLength = 120;

    private readonly List<ActivityKudos> _kudos = [];

    // EF Core materialisation only.
    private ActivityEvent()
    {
    }

    private ActivityEvent(
        Guid id,
        Guid actorPersonId,
        ActivityKind kind,
        string? subject,
        int? amount,
        int kudosCount,
        Guid? sourceId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        ActorPersonId = actorPersonId;
        Kind = kind;
        Subject = subject;
        Amount = amount;
        KudosCount = kudosCount;
        SourceId = sourceId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Who did it.</summary>
    public Guid ActorPersonId { get; private set; }

    public ActivityKind Kind { get; private set; }

    /// <summary>
    /// The goal or task this is about. User content — never logged, never sent
    /// to Sentry (docs/privacy.md).
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>A number the sentence needs: days of streak, percent reached.</summary>
    public int? Amount { get; private set; }

    /// <summary>
    /// How many kudos this has, in total.
    /// </summary>
    /// <remarks>
    /// Stored rather than derived from <see cref="Kudos"/>. The rows only exist
    /// for people q2 actually knows about, and a seeded event is meant to look
    /// like it was cheered by a crowd; deriving the number would silently
    /// reduce every one of them to the handful of modelled friends.
    /// <see cref="GiveKudos"/> and <see cref="WithdrawKudos"/> keep the two in
    /// step from there on.
    /// </remarks>
    public int KudosCount { get; private set; }

    /// <summary>
    /// The goal or task this came from.
    /// </summary>
    /// <remarks>
    /// Not a foreign key, and deliberately so: the feed is a record of what
    /// happened, and deleting a goal must not rewrite the past. It exists so
    /// that un-ticking a task can withdraw exactly the entry that ticking it
    /// published, without matching on a title somebody may have edited since.
    /// </remarks>
    public Guid? SourceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public IReadOnlyList<ActivityKudos> Kudos => _kudos;

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static ActivityEvent Create(
        Guid id,
        Guid actorPersonId,
        ActivityKind kind,
        string? subject,
        int? amount,
        int kudosCount,
        DateTimeOffset occurredAt,
        Guid? sourceId = null)
    {
        var errors = new Dictionary<string, string[]>();

        var normalisedSubject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        if (normalisedSubject is { Length: > MaxSubjectLength })
        {
            errors[nameof(Subject)] = [$"A subject may be at most {MaxSubjectLength} characters long."];
        }

        if (kudosCount < 0)
        {
            errors[nameof(KudosCount)] = ["Kudos cannot be negative."];
        }

        if (kind is ActivityKind.TaskCompleted or ActivityKind.GoalCreated && normalisedSubject is null)
        {
            errors[nameof(Subject)] = ["This kind of activity needs a subject."];
        }

        if (kind is ActivityKind.StreakReached && amount is null)
        {
            errors[nameof(Amount)] = ["This kind of activity needs an amount."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new ActivityEvent(id, actorPersonId, kind, normalisedSubject, amount, kudosCount, sourceId, occurredAt);
    }

    public bool HasKudosFrom(Guid personId) => _kudos.Any(k => k.PersonId == personId);

    /// <summary>Adds a kudos. Returns false when that person already gave one.</summary>
    public bool GiveKudos(Guid id, Guid personId)
    {
        if (HasKudosFrom(personId))
        {
            return false;
        }

        _kudos.Add(new ActivityKudos(id, Id, personId));
        KudosCount++;
        return true;
    }

    /// <summary>Takes a kudos back. Returns false when there was none to take.</summary>
    public bool WithdrawKudos(Guid personId)
    {
        var existing = _kudos.FirstOrDefault(k => k.PersonId == personId);
        if (existing is null)
        {
            return false;
        }

        _kudos.Remove(existing);
        KudosCount = Math.Max(0, KudosCount - 1);
        return true;
    }
}

/// <summary>One person's kudos on one activity.</summary>
public sealed class ActivityKudos
{
    // EF Core materialisation only.
    private ActivityKudos()
    {
    }

    internal ActivityKudos(Guid id, Guid activityEventId, Guid personId)
    {
        Id = id;
        ActivityEventId = activityEventId;
        PersonId = personId;
    }

    public Guid Id { get; private set; }

    public Guid ActivityEventId { get; private set; }

    public Guid PersonId { get; private set; }
}
