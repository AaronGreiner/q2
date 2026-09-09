using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>What became of a pause.</summary>
/// <remarks>
/// Persisted as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum PauseStatus
{
    /// <summary>Running. No deadline elapses and no window opens.</summary>
    Active,

    /// <summary>Over — it ran its course, or its owner ended it early.</summary>
    Ended,

    /// <summary>Lifted by the objections of the people it was announced to.</summary>
    Overturned,
}

/// <summary>
/// A justified time out from one's own goal.
/// </summary>
/// <remarks>
/// Illness, holiday, an arm in plaster. Without this a window that could not
/// possibly have been delivered still breaks a streak, and the balance carries
/// a "verpasst" that says nothing true about the person.
///
/// It covers **whole local days**, like everything else with a deadline in this
/// application: "drei Tage Pause" is today and the two days after it, ending at
/// the last instant of <see cref="EndsOn"/> in the owner's zone
/// (docs/adr/0016-windows-instead-of-steps.md). A pause measured in hours would
/// make "bis wann darf ich" a question about the clock somebody happened to
/// press the button at.
///
/// The costs that keep it from becoming a skip button live in
/// <see cref="PauseRules"/>, and the objections in <see cref="Vetoes"/> are
/// **anonymous** — the same rule as doubting a photograph, and for the same
/// reason: somebody who has to put their name to "I do not believe you are ill"
/// never does it, and a mechanism nobody uses protects nobody.
/// </remarks>
public sealed class GoalPause
{
    private readonly List<PauseVeto> _vetoes = [];

    // EF Core materialisation only.
    private GoalPause()
    {
        Reason = string.Empty;
    }

    private GoalPause(
        Guid id,
        Guid goalId,
        Guid personId,
        string reason,
        Guid? goalInstanceId,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        GoalId = goalId;
        PersonId = personId;
        Reason = reason;
        GoalInstanceId = goalInstanceId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        StartsAt = startsAt;
        EndsAt = endsAt;
        CreatedAt = createdAt;
        Status = PauseStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    /// <summary>
    /// Who set the goal aside. Always its owner — only the person who delivers
    /// can stop delivering.
    /// </summary>
    public Guid PersonId { get; private set; }

    /// <summary>
    /// Why. Compulsory, and read by everybody invited to the goal.
    /// </summary>
    /// <remarks>
    /// User content: never logged and never sent to Sentry (docs/privacy.md).
    /// </remarks>
    public string Reason { get; private set; }

    /// <summary>
    /// The window this set aside, when there was one running.
    /// </summary>
    /// <remarks>
    /// Held on to so an objection puts <em>that</em> window back and not a later
    /// one — by the time the objections arrive, the goal may well have moved on.
    /// </remarks>
    public Guid? GoalInstanceId { get; private set; }

    /// <summary>First local day the pause covers. Always the day it was asked for.</summary>
    public DateOnly StartsOn { get; private set; }

    /// <summary>Last local day it covers, inclusive.</summary>
    public DateOnly EndsOn { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>The last instant of <see cref="EndsOn"/> in the owner's zone.</summary>
    public DateTimeOffset EndsAt { get; private set; }

    public PauseStatus Status { get; private set; }

    /// <summary>When it was asked for, which is not the same as when it starts.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The objections raised, never shown with a name.</summary>
    public IReadOnlyList<PauseVeto> Vetoes => _vetoes;

    public int VetoCount => _vetoes.Count;

    /// <summary>How many whole local days this pause covers.</summary>
    public int Days => EndsOn.DayNumber - StartsOn.DayNumber + 1;

    /// <summary>True while this pause is holding the goal.</summary>
    public bool IsActiveAt(DateTimeOffset now) => Status == PauseStatus.Active && now <= EndsAt;

    /// <summary>
    /// True when this pause covered that local day.
    /// </summary>
    /// <remarks>
    /// An overturned pause covers nothing: the objections said it should not
    /// have happened, so the days it claimed go back to counting.
    /// </remarks>
    public bool Covers(DateOnly day) =>
        Status != PauseStatus.Overturned && StartsOn <= day && day <= EndsOn;

    /// <summary>True when this person has already objected.</summary>
    public bool HasVetoFrom(Guid personId) => _vetoes.Any(veto => veto.PersonId == personId);

    internal static GoalPause Create(
        Guid id,
        Guid goalId,
        Guid personId,
        string? reason,
        Guid? goalInstanceId,
        DateOnly startsOn,
        int days,
        DateTimeOffset startsAt,
        Func<DateOnly, DateTimeOffset> endOfDay,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(endOfDay);

        var errors = new Dictionary<string, string[]>();

        if (!PauseRules.IsReasonValid(reason))
        {
            errors[nameof(Reason)] =
                [$"A pause needs a reason of at least {PauseRules.MinReasonLength} characters."];
        }
        else if (reason!.Trim().Length > PauseRules.MaxReasonLength)
        {
            errors[nameof(Reason)] =
                [$"A reason may be at most {PauseRules.MaxReasonLength} characters long."];
        }

        if (days is < PauseRules.MinDays or > PauseRules.MaxDays)
        {
            errors[nameof(Days)] =
                [$"A pause runs for between {PauseRules.MinDays} and {PauseRules.MaxDays} days."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var endsOn = startsOn.AddDays(days - 1);

        return new GoalPause(
            id,
            goalId,
            personId,
            reason!.Trim(),
            goalInstanceId,
            startsOn,
            endsOn,
            startsAt,
            endOfDay(endsOn),
            createdAt);
    }

    /// <summary>
    /// Adds or takes back one person's objection. Returns true when the pause
    /// has now been overturned.
    /// </summary>
    /// <remarks>
    /// A toggle rather than a one-way vote, unlike doubting a photograph. That
    /// vote is a judgement on something that either happened or did not; this
    /// one is about somebody's circumstances, and taking back "I do not believe
    /// you" has to stay possible until it has had a consequence.
    /// </remarks>
    internal bool ToggleVeto(Guid id, Guid personId, int participantCount, DateTimeOffset now)
    {
        if (Status != PauseStatus.Active)
        {
            return false;
        }

        var existing = _vetoes.FirstOrDefault(veto => veto.PersonId == personId);

        if (existing is not null)
        {
            _vetoes.Remove(existing);
            return false;
        }

        _vetoes.Add(new PauseVeto(id, Id, personId, now));

        if (!PauseRules.IsOverturned(_vetoes.Count, participantCount))
        {
            return false;
        }

        Status = PauseStatus.Overturned;
        EndsAt = now;
        return true;
    }

    /// <summary>
    /// Ends a running pause early, on the owner's own say-so. Returns false
    /// when it was not running.
    /// </summary>
    /// <remarks>
    /// It stops the pause covering any <em>further</em> days; the days it has
    /// already covered stay covered, today included. So the goal picks up again
    /// with its next window rather than with the one it was excused from.
    ///
    /// The suspended window is deliberately <em>not</em> put back. Coming back
    /// early is a good thing, and a person who does it should not be handed the
    /// deadline they were excused from — only an objection does that, because
    /// only an objection says the excuse was never valid.
    /// </remarks>
    internal bool EndEarly(DateOnly today, DateTimeOffset now)
    {
        if (Status != PauseStatus.Active)
        {
            return false;
        }

        Status = PauseStatus.Ended;
        EndsOn = today < StartsOn ? StartsOn : today;
        EndsAt = now;
        return true;
    }

    /// <summary>
    /// Closes a pause whose last day has passed. Returns false when there was
    /// nothing to close, so a job that runs every ten minutes writes once.
    /// </summary>
    internal bool Expire(DateTimeOffset now)
    {
        if (Status != PauseStatus.Active || now <= EndsAt)
        {
            return false;
        }

        Status = PauseStatus.Ended;
        return true;
    }
}

/// <summary>
/// One person's objection to a pause.
/// </summary>
/// <remarks>
/// The name is stored because somebody may only object once and may take it
/// back — not because it is ever shown. No contract carries it; see
/// <see cref="GoalPause.Vetoes"/>.
/// </remarks>
public sealed class PauseVeto
{
    // EF Core materialisation only.
    private PauseVeto()
    {
    }

    internal PauseVeto(Guid id, Guid goalPauseId, Guid personId, DateTimeOffset createdAt)
    {
        Id = id;
        GoalPauseId = goalPauseId;
        PersonId = personId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid GoalPauseId { get; private set; }

    public Guid PersonId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
