namespace Q2.Api.Features.Goals;

/// <summary>What became of the part of an edit that waits for the next window.</summary>
/// <remarks>
/// Persisted as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum GoalChangeStatus
{
    /// <summary>Nothing in the edit waited: all of it applied the moment it was made.</summary>
    None,

    /// <summary>Waiting for the window it applies with, and open to objection if it makes things easier.</summary>
    Pending,

    /// <summary>In force since <see cref="GoalChange.AppliesOn"/>.</summary>
    Applied,

    /// <summary>Lifted by the objections of the people who check the goal. The goal stayed as it was.</summary>
    Overturned,

    /// <summary>Taken back by the owner before it applied.</summary>
    Withdrawn,

    /// <summary>Replaced by a later edit, or dropped because the goal stopped. Says nothing of its own.</summary>
    Superseded,
}

/// <summary>
/// One edit of a running goal: what it changed, from what to what, and what
/// became of the part that had to wait.
/// </summary>
/// <remarks>
/// A goal is changed in front of the people who check it, so every edit leaves
/// a row, and the goal's conversation derives its lines from these rows rather
/// than from messages somebody wrote (<see cref="GoalTimeline"/>,
/// docs/adr/0044-editing-a-running-goal.md).
///
/// **Two halves.** The title, the category, a checker added and an earlier day for
/// a one-off apply the moment the edit is saved: none of them lets the owner
/// off anything. Everything else — the schedule, a Don't's limit and proof
/// mode, a later day, the description and a checker removed — waits for the
/// next window, because a window keeps the terms it opened with. If any of it
/// makes the goal easier (<see cref="GoalChangeRules"/>), it is
/// <see cref="Objectable"/>: the checkers can lift it the way they lift a pause
/// (<see cref="PauseRules"/>), and it waits until it has been open to objection
/// for <see cref="GoalChangeRules.ObjectionPeriod"/>.
///
/// A field that did not change is null on both sides. The "from" side is kept
/// because the line in the conversation says what changed from what, and the
/// goal itself only remembers the latest answer.
/// </remarks>
public sealed class GoalChange
{
    private readonly List<GoalChangeChecker> _checkers = [];
    private readonly List<GoalChangeVeto> _vetoes = [];

    // EF Core materialisation only.
    private GoalChange()
    {
    }

    private GoalChange(Guid id, Guid goalId, Guid personId, DateTimeOffset createdAt)
    {
        Id = id;
        GoalId = goalId;
        PersonId = personId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid GoalId { get; private set; }

    /// <summary>Who made the edit. Always the goal's owner — only the person who delivers changes the terms.</summary>
    public Guid PersonId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? TitleFrom { get; private set; }

    public string? TitleTo { get; private set; }

    public GoalCategory? CategoryFrom { get; private set; }

    public GoalCategory? CategoryTo { get; private set; }

    /// <summary>
    /// Whether the description changed. A flag of its own, because "no
    /// description any more" is a change whose new value is null.
    /// </summary>
    public bool DescriptionChanged { get; private set; }

    /// <summary>User content: never logged and never sent to Sentry (docs/privacy.md).</summary>
    public string? DescriptionFrom { get; private set; }

    public string? DescriptionTo { get; private set; }

    public GoalSchedule? ScheduleFrom { get; private set; }

    public GoalSchedule? ScheduleTo { get; private set; }

    public int? LimitFrom { get; private set; }

    public int? LimitTo { get; private set; }

    public DontProofMode? ProofModeFrom { get; private set; }

    public DontProofMode? ProofModeTo { get; private set; }

    /// <summary>A one-off's day before the edit: its target, or the day its window ends.</summary>
    public DateOnly? TargetDateFrom { get; private set; }

    public DateOnly? TargetDateTo { get; private set; }

    /// <summary>The checkers added and removed.</summary>
    public IReadOnlyList<GoalChangeChecker> Checkers => _checkers;

    /// <summary>What became of the half that waits. <see cref="GoalChangeStatus.None"/> when nothing did.</summary>
    public GoalChangeStatus Status { get; private set; }

    /// <summary>Whether the waiting half makes the goal easier, and can therefore be objected to.</summary>
    public bool Objectable { get; private set; }

    /// <summary>When it applied, was lifted, withdrawn or replaced. Null while it waits.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>The first local day the waiting half counted for, once it applied.</summary>
    public DateOnly? AppliesOn { get; private set; }

    /// <summary>The objections raised, never shown with a name (<see cref="PauseVeto"/>).</summary>
    public IReadOnlyList<GoalChangeVeto> Vetoes => _vetoes;

    public int VetoCount => _vetoes.Count;

    public IEnumerable<Guid> AddedCheckerIds => _checkers.Where(checker => checker.Added).Select(checker => checker.PersonId);

    public IEnumerable<Guid> RemovedCheckerIds => _checkers.Where(checker => !checker.Added).Select(checker => checker.PersonId);

    /// <summary>True while the waiting half has not applied, been lifted or been taken back.</summary>
    public bool IsPending => Status == GoalChangeStatus.Pending;

    /// <summary>When the objection period ends: the earliest moment an easier change can apply.</summary>
    public DateTimeOffset ObjectionEndsAt => CreatedAt + GoalChangeRules.ObjectionPeriod;

    /// <summary>True when this person has objected.</summary>
    public bool HasVetoFrom(Guid personId) => _vetoes.Any(veto => veto.PersonId == personId);

    /// <summary>
    /// Whether the waiting half may apply at <paramref name="moment"/>: at
    /// once for a stricter change, once the objection period is over for an
    /// easier one.
    /// </summary>
    /// <remarks>
    /// The moment alone decides it for a one-off, whose change goes into the
    /// window it has. A repeating goal's change also waits for its old window
    /// to end, which <see cref="GoalMaintenance"/> decides.
    /// </remarks>
    public bool IsDueAt(DateTimeOffset moment) =>
        IsPending && (!Objectable || moment >= ObjectionEndsAt);

    /// <summary>The waiting half, in the shape that decides whether two edits ask for the same thing.</summary>
    internal DeferredTerms Deferred => new(
        ScheduleTo,
        LimitTo,
        ProofModeTo,
        TargetDateTo is { } to && TargetDateFrom is { } from && to > from ? to : null,
        DescriptionChanged,
        DescriptionTo,
        [.. RemovedCheckerIds.Order()]);

    /// <summary>
    /// Writes down a planned edit. The plan has already applied its immediate
    /// half to the goal; this only records it.
    /// </summary>
    internal static GoalChange Record(Guid id, Goal goal, GoalEditPlan plan, Func<Guid> newId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(newId);

        var change = new GoalChange(id, goal.Id, goal.OwnerPersonId, now)
        {
            TitleFrom = plan.TitleTo is null ? null : plan.TitleFrom,
            TitleTo = plan.TitleTo,
            CategoryFrom = plan.CategoryTo is null ? null : plan.CategoryFrom,
            CategoryTo = plan.CategoryTo,
            TargetDateFrom = plan.TargetDateTo is null ? null : plan.TargetDateFrom,
            TargetDateTo = plan.TargetDateTo,
        };

        foreach (var personId in plan.CheckersAdded)
        {
            change._checkers.Add(new GoalChangeChecker(newId(), id, personId, added: true));
        }

        if (plan.KeepsPending || plan.Deferred is not { } deferred)
        {
            change.Status = GoalChangeStatus.None;
            return change;
        }

        change.ScheduleFrom = deferred.Schedule is null ? null : goal.Schedule.Copy();
        change.ScheduleTo = deferred.Schedule?.Copy();
        change.LimitFrom = deferred.Limit is null ? null : goal.Limit;
        change.LimitTo = deferred.Limit;
        change.ProofModeFrom = deferred.ProofMode is null ? null : goal.ProofMode;
        change.ProofModeTo = deferred.ProofMode;
        change.DescriptionChanged = deferred.DescriptionChanged;
        change.DescriptionFrom = deferred.DescriptionChanged ? goal.Description : null;
        change.DescriptionTo = deferred.Description;

        if (deferred.TargetDate is { } later)
        {
            change.TargetDateFrom = plan.TargetDateFrom;
            change.TargetDateTo = later;
        }

        foreach (var personId in deferred.RemovedCheckers)
        {
            change._checkers.Add(new GoalChangeChecker(newId(), id, personId, added: false));
        }

        change.Status = GoalChangeStatus.Pending;
        change.Objectable = plan.Objectable;
        return change;
    }

    /// <summary>
    /// Adds or takes back one checker's objection. Returns true when the change
    /// has now been lifted.
    /// </summary>
    /// <remarks>
    /// A toggle, like the objection to a pause, and for the same reason: it can
    /// be taken back until it has had a consequence.
    /// </remarks>
    internal bool ToggleVeto(Guid id, Guid personId, int participantCount, DateTimeOffset now)
    {
        if (!IsPending || !Objectable)
        {
            return false;
        }

        var existing = _vetoes.FirstOrDefault(veto => veto.PersonId == personId);

        if (existing is not null)
        {
            _vetoes.Remove(existing);
            return false;
        }

        _vetoes.Add(new GoalChangeVeto(id, Id, personId, now));

        if (!PauseRules.IsOverturned(_vetoes.Count, participantCount))
        {
            return false;
        }

        Status = GoalChangeStatus.Overturned;
        ResolvedAt = now;
        return true;
    }

    internal void MarkApplied(DateOnly on, DateTimeOffset now)
    {
        Status = GoalChangeStatus.Applied;
        AppliesOn = on;
        ResolvedAt = now;
    }

    internal bool Withdraw(DateTimeOffset now) => Resolve(GoalChangeStatus.Withdrawn, now);

    internal bool Supersede(DateTimeOffset now) => Resolve(GoalChangeStatus.Superseded, now);

    private bool Resolve(GoalChangeStatus status, DateTimeOffset now)
    {
        if (!IsPending)
        {
            return false;
        }

        Status = status;
        ResolvedAt = now;
        return true;
    }
}

/// <summary>Somebody added to or removed from a goal's checkers by an edit.</summary>
public sealed class GoalChangeChecker
{
    // EF Core materialisation only.
    private GoalChangeChecker()
    {
    }

    internal GoalChangeChecker(Guid id, Guid goalChangeId, Guid personId, bool added)
    {
        Id = id;
        GoalChangeId = goalChangeId;
        PersonId = personId;
        Added = added;
    }

    public Guid Id { get; private set; }

    public Guid GoalChangeId { get; private set; }

    public Guid PersonId { get; private set; }

    /// <summary>True for somebody added, false for somebody removed.</summary>
    public bool Added { get; private set; }
}

/// <summary>
/// One checker's objection to an easier change.
/// </summary>
/// <remarks>
/// The name is stored so somebody can object only once and take it back — never
/// to be shown; no contract carries it, exactly as with <see cref="PauseVeto"/>.
/// </remarks>
public sealed class GoalChangeVeto
{
    // EF Core materialisation only.
    private GoalChangeVeto()
    {
    }

    internal GoalChangeVeto(Guid id, Guid goalChangeId, Guid personId, DateTimeOffset createdAt)
    {
        Id = id;
        GoalChangeId = goalChangeId;
        PersonId = personId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid GoalChangeId { get; private set; }

    public Guid PersonId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
