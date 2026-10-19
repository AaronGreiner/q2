namespace Q2.Api.Features.Goals;

/// <summary>
/// What the owner of a running goal asks it to become.
/// </summary>
/// <remarks>
/// Null leaves a field as it stands — and "as it stands" includes a change that
/// is already waiting, so an edit of the title alone does not take back the
/// schedule change made yesterday. To take that back, the owner asks for the
/// current value again, or withdraws it.
/// </remarks>
/// <param name="Description">Null to leave it; an empty string to remove it.</param>
/// <param name="TargetDate">A one-off only: the day it is due by.</param>
/// <param name="Limit">A Don't only.</param>
/// <param name="ProofMode">A Don't only.</param>
/// <param name="CheckerIds">
/// Everybody who should check the goal, not a difference: whoever is on it now
/// and missing here is removed, whoever is here and not on it is added.
/// </param>
public sealed record GoalEdit(
    string? Title = null,
    string? Description = null,
    string? Icon = null,
    GoalSchedule? Schedule = null,
    DateOnly? TargetDate = null,
    int? Limit = null,
    DontProofMode? ProofMode = null,
    IReadOnlyCollection<Guid>? CheckerIds = null);

/// <summary>
/// The half of an edit that waits for the next window: everything the goal's
/// windows are held to, the agreement they are judged against, and who judges.
/// </summary>
/// <param name="Schedule">The new schedule, or null when it stays.</param>
/// <param name="Limit">A Don't's new limit, or null when it stays.</param>
/// <param name="ProofMode">A Don't's new proof mode, or null when it stays.</param>
/// <param name="TargetDate">A one-off's later day, or null. An earlier day does not wait.</param>
/// <param name="Description">The new description when <paramref name="DescriptionChanged"/>; null for none.</param>
/// <param name="RemovedCheckers">Who stops checking, in id order.</param>
public sealed record DeferredTerms(
    GoalSchedule? Schedule,
    int? Limit,
    DontProofMode? ProofMode,
    DateOnly? TargetDate,
    bool DescriptionChanged,
    string? Description,
    IReadOnlyList<Guid> RemovedCheckers)
{
    /// <summary>True when nothing waits.</summary>
    public bool IsEmpty =>
        Schedule is null
        && Limit is null
        && ProofMode is null
        && TargetDate is null
        && !DescriptionChanged
        && RemovedCheckers.Count == 0;

    /// <summary>
    /// Whether two edits ask for the same thing. Value equality, which the
    /// record's own would not give: the schedule is an entity and the list a
    /// reference.
    /// </summary>
    public bool SameAs(DeferredTerms other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return (Schedule is null ? other.Schedule is null : other.Schedule is not null && Schedule.SameAs(other.Schedule))
            && Limit == other.Limit
            && ProofMode == other.ProofMode
            && TargetDate == other.TargetDate
            && DescriptionChanged == other.DescriptionChanged
            && Description == other.Description
            && RemovedCheckers.SequenceEqual(other.RemovedCheckers);
    }
}

/// <summary>
/// An edit, worked out against the goal and not yet carried out: what applies
/// now, what waits, and whether the checkers can object to it.
/// </summary>
/// <remarks>
/// Its own step so the sheet can be told exactly this before anything is saved
/// (<c>POST /api/goals/{id}/change/preview</c>) by the same code that will then
/// save it.
/// </remarks>
/// <param name="TitleTo">The new title, or null when it stays.</param>
/// <param name="IconTo">The new icon, or null when it stays.</param>
/// <param name="TargetDateFrom">A one-off's day before the edit: what a moved day moves from.</param>
/// <param name="TargetDateTo">A one-off's earlier day, which applies at once; null otherwise.</param>
/// <param name="CheckersAdded">Who starts checking now.</param>
/// <param name="Deferred">What waits for the next window, or null when nothing does.</param>
/// <param name="Objectable">Whether what waits makes the goal easier (<see cref="GoalChangeRules"/>).</param>
/// <param name="KeepsPending">
/// The change already waiting asks for exactly what this edit's waiting half
/// does, so it stays — with its objections and its clock — rather than being
/// replaced by an identical one that would start the day again.
/// </param>
/// <param name="WithdrawsPending">A change is waiting and this edit wants none of it any more.</param>
public sealed record GoalEditPlan(
    string TitleFrom,
    string? TitleTo,
    string IconFrom,
    string? IconTo,
    DateOnly? TargetDateFrom,
    DateOnly? TargetDateTo,
    IReadOnlyList<Guid> CheckersAdded,
    DeferredTerms? Deferred,
    bool Objectable,
    bool KeepsPending,
    bool WithdrawsPending)
{
    /// <summary>True when the edit applies something now.</summary>
    public bool HasImmediate => TitleTo is not null || IconTo is not null || TargetDateTo is not null || CheckersAdded.Count > 0;

    /// <summary>True when the edit leaves a new change waiting.</summary>
    public bool HasNewDeferred => Deferred is not null && !KeepsPending;

    /// <summary>True when saving it would change anything at all.</summary>
    public bool HasChanges => HasImmediate || HasNewDeferred || WithdrawsPending;
}
