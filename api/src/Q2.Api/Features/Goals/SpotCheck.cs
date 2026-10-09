namespace Q2.Api.Features.Goals;

/// <summary>What became of one spot check.</summary>
/// <remarks>Persisted as text, like every enum here.</remarks>
public enum SpotCheckStatus
{
    /// <summary>Drawn for a moment that has not come yet. Never shown to anybody.</summary>
    Scheduled,

    /// <summary>Issued: the owner has three minutes to take a live photograph.</summary>
    Open,

    /// <summary>A photograph came in time; its friends' vote decides the rest.</summary>
    Answered,

    /// <summary>No photograph within the three minutes. The window is missed.</summary>
    Missed,

    /// <summary>
    /// Its three minutes passed before it could be issued — the server was not
    /// running. Decided as nothing at all, and not issued again.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Missed"/>: the owner was never asked, so they cannot have
    /// failed to answer. Not issued late either: a check announced an hour after
    /// its moment is a check the owner could have prepared for
    /// (docs/adr/0038-spot-checks.md).
    /// </remarks>
    Voided,
}

/// <summary>
/// A live photograph a Don't's owner is asked for at a moment they cannot plan
/// for — "show me what you are doing right now".
/// </summary>
/// <remarks>
/// At most one per day of a running window, drawn by the server between
/// <see cref="SpotCheckRules.EarliestHour"/> and
/// <see cref="SpotCheckRules.LatestHour"/> in the owner's zone and outside their
/// quiet hours, and never announced in advance: <see cref="ScheduledAt"/> leaves
/// the server only once the check is open. The photograph is a proof like any
/// other, voted on by the goal's friends; a refused one or none at all misses
/// the window, a believed one leaves it to its limit (docs/adr/0038-spot-checks.md).
/// </remarks>
public sealed class SpotCheck
{
    // EF Core materialisation only.
    private SpotCheck()
    {
    }

    internal SpotCheck(Guid id, Guid goalInstanceId, DateOnly day, DateTimeOffset scheduledAt)
    {
        Id = id;
        GoalInstanceId = goalInstanceId;
        Day = day;
        ScheduledAt = scheduledAt;
    }

    public Guid Id { get; private set; }

    public Guid GoalInstanceId { get; private set; }

    /// <summary>The owner's local day it was drawn for. One per day.</summary>
    public DateOnly Day { get; private set; }

    /// <summary>The moment drawn. Secret until it comes.</summary>
    public DateTimeOffset ScheduledAt { get; private set; }

    /// <summary>When the owner was asked. Null until then.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>Three minutes after <see cref="IssuedAt"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public SpotCheckStatus Status { get; private set; } = SpotCheckStatus.Scheduled;

    /// <summary>The photograph that answered it, when one did.</summary>
    public Guid? ProofPhotoId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>True while the owner can still answer it.</summary>
    public bool IsOpenAt(DateTimeOffset now) => Status == SpotCheckStatus.Open && now <= ExpiresAt;

    /// <summary>
    /// Asks the owner, if its moment has come and its three minutes have not
    /// already gone by. Returns false otherwise.
    /// </summary>
    /// <remarks>
    /// The three minutes run from the moment it is issued, not from the moment
    /// drawn: whatever runs it may be a few seconds late, and those seconds
    /// must not come out of the owner's time.
    /// </remarks>
    internal bool Issue(DateTimeOffset now)
    {
        if (Status != SpotCheckStatus.Scheduled
            || now < ScheduledAt
            || now > ScheduledAt + SpotCheckRules.AnswerTime)
        {
            return false;
        }

        Status = SpotCheckStatus.Open;
        IssuedAt = now;
        ExpiresAt = now + SpotCheckRules.AnswerTime;
        return true;
    }

    /// <summary>
    /// Settles a check whose time is up: unanswered becomes missed, never
    /// issued becomes void. Returns the status it moved to, or null when
    /// nothing moved.
    /// </summary>
    internal SpotCheckStatus? Settle(DateTimeOffset now)
    {
        if (Status == SpotCheckStatus.Scheduled && now > ScheduledAt + SpotCheckRules.AnswerTime)
        {
            Status = SpotCheckStatus.Voided;
            ResolvedAt = now;
            return Status;
        }

        if (Status == SpotCheckStatus.Open && now > ExpiresAt)
        {
            Status = SpotCheckStatus.Missed;
            ResolvedAt = now;
            return Status;
        }

        return null;
    }

    /// <summary>Moves a check that has not been asked yet to another moment. Diagnostics only.</summary>
    internal bool Reschedule(DateTimeOffset at)
    {
        if (Status != SpotCheckStatus.Scheduled)
        {
            return false;
        }

        ScheduledAt = at;
        return true;
    }

    /// <summary>Records the photograph that answered it. Returns false when it is not open.</summary>
    internal bool Answer(Guid proofPhotoId, DateTimeOffset now)
    {
        if (!IsOpenAt(now))
        {
            return false;
        }

        Status = SpotCheckStatus.Answered;
        ProofPhotoId = proofPhotoId;
        ResolvedAt = now;
        return true;
    }
}

/// <summary>The rules a spot check is drawn and answered by, in one place.</summary>
public static class SpotCheckRules
{
    /// <summary>No check before eight in the owner's morning.</summary>
    public const int EarliestHour = 8;

    /// <summary>And none from ten in the evening.</summary>
    public const int LatestHour = 22;

    /// <summary>How long the owner has to answer.</summary>
    public static readonly TimeSpan AnswerTime = TimeSpan.FromMinutes(3);
}
