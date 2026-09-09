using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>What became of one window.</summary>
/// <remarks>
/// Persisted as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum GoalInstanceStatus
{
    /// <summary>Still running, or over but not yet resolved.</summary>
    Open,

    /// <summary>Delivered: every required proof came in before the deadline.</summary>
    Done,

    /// <summary>The deadline passed with proofs still outstanding.</summary>
    Missed,

    /// <summary>
    /// Set aside — by a pause, or because the goal was closed while it ran.
    /// </summary>
    /// <remarks>
    /// Neither delivered nor missed, and therefore in neither half of the
    /// balance and no break in the streak: a pause is not a result. It has no
    /// <see cref="GoalInstance.ResolvedAt"/> for the same reason — nothing
    /// about it was resolved.
    /// </remarks>
    Paused,
}

/// <summary>
/// One window in which a goal has to be delivered — the heart of the whole
/// model.
/// </summary>
/// <remarks>
/// This is what q2 was missing. Before it, a goal counted steps: a number you
/// turned up yourself, which could not be late and could not be failed. A
/// window has a beginning, an end and an outcome, and everything the product is
/// about hangs off it — the streak that breaks, the balance that says "5
/// verpasst", the warning that goes out in the evening, the history grid.
///
/// <see cref="StartsAt"/> and <see cref="DueAt"/> are instants, converted from
/// whole local days by <see cref="Q2.Api.Infrastructure.Time.LocalCalendar"/>
/// when the window is created. They are stored rather than recomputed, so
/// somebody moving to another country does not retroactively change whether
/// last Tuesday was late.
///
/// <see cref="ConfirmedProofs"/> is a count rather than a flag because "3× pro
/// Woche" is a real commitment: a window can be two-thirds delivered, and both
/// the person and their friends need to see that while it still matters.
/// </remarks>
public sealed class GoalInstance
{
    private readonly List<ProofPhoto> _proofs = [];

    // EF Core materialisation only.
    private GoalInstance()
    {
    }

    private GoalInstance(
        Guid id,
        Guid goalId,
        DateOnly startsOn,
        DateOnly dueOn,
        DateTimeOffset startsAt,
        DateTimeOffset dueAt,
        int requiredProofs)
    {
        Id = id;
        GoalId = goalId;
        StartsOn = startsOn;
        DueOn = dueOn;
        StartsAt = startsAt;
        DueAt = dueAt;
        RequiredProofs = requiredProofs;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    /// <summary>
    /// The local days this window covers.
    /// </summary>
    /// <remarks>
    /// Kept alongside the instants because they are what people are shown and
    /// what the history grid is laid out on, and deriving them back out of an
    /// instant would need the person's zone at every read — including reads by
    /// their friends, who may be in another one.
    /// </remarks>
    public DateOnly StartsOn { get; private set; }

    public DateOnly DueOn { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>The deadline: the last instant of <see cref="DueOn"/>.</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>How many proofs this window takes. At least one.</summary>
    public int RequiredProofs { get; private set; }

    /// <summary>How many have been delivered so far.</summary>
    public int ConfirmedProofs { get; private set; }

    public GoalInstanceStatus Status { get; private set; } = GoalInstanceStatus.Open;

    /// <summary>When it was resolved, either way. Null while it is still open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// When this window's friends were warned that it was about to be missed.
    /// Null while they have not been.
    /// </summary>
    /// <remarks>
    /// The key that makes "at most one warning per window" a property of the
    /// window rather than of whoever remembered. It lives here rather than in
    /// the job because the job holds no state between runs — and because a
    /// second copy of "have we told them yet" is a second thing that can be
    /// wrong (<see cref="GoalRisk"/>).
    /// </remarks>
    public DateTimeOffset? RiskNotifiedAt { get; private set; }

    /// <summary>Every photograph delivered into this window, believed or not.</summary>
    public IReadOnlyList<ProofPhoto> Proofs => _proofs;

    /// <summary>What is still outstanding, never below zero.</summary>
    public int RemainingProofs => Math.Max(0, RequiredProofs - ConfirmedProofs);

    /// <summary>The photograph friends are looking at right now, if any.</summary>
    public ProofPhoto? PendingProof =>
        _proofs.FirstOrDefault(proof => proof.Status == ProofStatus.Voting);

    /// <summary>
    /// Which attempt the next photograph would be.
    /// </summary>
    /// <remarks>
    /// Counted from the last <em>believed</em> one, not from the start of the
    /// window. On "three times a week" that is what makes each delivery its own
    /// chance: two friends doubting Monday's photograph must not use up the
    /// retries for Wednesday's.
    /// </remarks>
    public int NextAttempt =>
        _proofs.Count - _proofs.FindLastIndex(proof => proof.Status == ProofStatus.Confirmed);

    /// <summary>True when a photograph may be delivered into this window now.</summary>
    public bool AcceptsProof =>
        Status == GoalInstanceStatus.Open
        && PendingProof is null
        && ConfirmedProofs < RequiredProofs
        && NextAttempt <= ProofVoting.MaxAttempts;

    /// <summary>True while this window is the one to deliver into.</summary>
    public bool IsCurrentAt(DateTimeOffset now) =>
        Status == GoalInstanceStatus.Open && now <= DueAt;

    internal static GoalInstance Create(
        Guid id,
        Guid goalId,
        GoalWindow window,
        DateTimeOffset startsAt,
        DateTimeOffset dueAt)
    {
        if (window.RequiredProofs < 1)
        {
            throw new DomainValidationException(
                nameof(RequiredProofs),
                "A window has to require at least one proof.");
        }

        if (dueAt < startsAt)
        {
            throw new DomainValidationException(nameof(DueAt), "A window cannot end before it starts.");
        }

        return new GoalInstance(id, goalId, window.Start, window.End, startsAt, dueAt, window.RequiredProofs);
    }

    /// <summary>
    /// Adds a photograph for friends to look at. Returns null when this window
    /// is not taking one.
    /// </summary>
    public ProofPhoto? SubmitProof(
        Guid id,
        Guid uploaderPersonId,
        Guid imageId,
        bool capturedInApp,
        DateTimeOffset now)
    {
        if (!AcceptsProof)
        {
            return null;
        }

        var proof = ProofPhoto.Create(id, Id, uploaderPersonId, imageId, NextAttempt, capturedInApp, now);
        _proofs.Add(proof);
        return proof;
    }

    /// <summary>
    /// Applies a decided vote to the window. Returns true when anything moved.
    /// </summary>
    /// <remarks>
    /// The one place a vote becomes a consequence, and the reason it is here
    /// rather than in a service: whether a window is delivered is the window's
    /// own business.
    ///
    /// A rejection ends the window immediately rather than leaving it open to
    /// run out. Two separate majorities have said the thing did not happen, so
    /// waiting for the deadline would only postpone the same answer — and on a
    /// weekly quota it would leave a window open that can no longer be filled.
    /// </remarks>
    public bool ApplyProofOutcome(ProofPhoto proof, VotingResult result, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proof);

        if (!proof.Resolve(result, now))
        {
            return false;
        }

        if (result == VotingResult.Confirmed)
        {
            return RecordProof(now);
        }

        if (result == VotingResult.Rejected && Status == GoalInstanceStatus.Open)
        {
            Status = GoalInstanceStatus.Missed;
            ResolvedAt = now;
        }

        // A retry moves nothing on the window itself: the photograph is gone,
        // the window is still open, and one attempt is left.
        return true;
    }

    /// <summary>
    /// Counts one believed proof, and closes the window when that was the last
    /// one it needed. Returns false when there was nothing left to record.
    /// </summary>
    /// <remarks>
    /// Internal, because from stage 4 the only thing that may call it is a
    /// confirmed vote (<see cref="ApplyProofOutcome"/>). It used to be the
    /// owner pressing a button, which is exactly the self-report this product
    /// exists to replace.
    /// </remarks>
    internal bool RecordProof(DateTimeOffset now)
    {
        if (Status != GoalInstanceStatus.Open || ConfirmedProofs >= RequiredProofs)
        {
            return false;
        }

        ConfirmedProofs++;

        if (ConfirmedProofs >= RequiredProofs)
        {
            Status = GoalInstanceStatus.Done;
            ResolvedAt = now;
        }

        return true;
    }

    /// <summary>
    /// Sets this window aside, so its deadline stops counting. Returns false
    /// when there was nothing to set aside.
    /// </summary>
    /// <remarks>
    /// Refused while a photograph is being voted on, and that is the whole
    /// reason a pause cannot be requested then either: friends who are halfway
    /// through deciding whether they believe something must not have the
    /// question withdrawn from under them, and the outcome they were about to
    /// reach would have nowhere to land.
    /// </remarks>
    internal bool Suspend()
    {
        if (Status != GoalInstanceStatus.Open || PendingProof is not null)
        {
            return false;
        }

        Status = GoalInstanceStatus.Paused;
        return true;
    }

    /// <summary>
    /// Puts a set-aside window back, with its original deadline. Returns false
    /// when it was not set aside.
    /// </summary>
    /// <remarks>
    /// Deliberately without a new deadline. An objection says the pause should
    /// never have happened, so the window continues as it was — and if that
    /// deadline has meanwhile passed, the next maintenance run makes it a miss.
    /// That is the honest consequence of an objection that carried, and it is
    /// not reproduced here.
    /// </remarks>
    internal bool Resume()
    {
        if (Status != GoalInstanceStatus.Paused)
        {
            return false;
        }

        Status = GoalInstanceStatus.Open;
        return true;
    }

    /// <summary>
    /// Records that this window's friends have now been warned. Returns false
    /// when they already had been, so a job that runs every ten minutes warns
    /// once rather than sixty times an evening.
    /// </summary>
    public bool NotifyRisk(DateTimeOffset now)
    {
        if (RiskNotifiedAt is not null || Status != GoalInstanceStatus.Open)
        {
            return false;
        }

        RiskNotifiedAt = now;
        return true;
    }

    /// <summary>
    /// Marks an expired window as missed. Returns false when it was not
    /// expired, or was already resolved.
    /// </summary>
    /// <remarks>
    /// Idempotent on purpose: the maintenance job may run twice over the same
    /// day after a restart, and the second run must do nothing at all rather
    /// than break a streak a second time.
    /// </remarks>
    public bool Miss(DateTimeOffset now)
    {
        if (Status != GoalInstanceStatus.Open || now <= DueAt)
        {
            return false;
        }

        Status = GoalInstanceStatus.Missed;
        ResolvedAt = now;
        return true;
    }
}
