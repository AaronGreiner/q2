using Q2.Api.Features.Chats;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Something somebody has committed to, alone or in front of other people.
/// </summary>
/// <remarks>
/// A goal is a <see cref="Schedule"/> and a chain of
/// <see cref="GoalInstance">windows</see> — not a counter. That is the whole
/// change from what this used to be: progress was "14 of 21 steps", a number
/// you turned up yourself, which could not be late and could not be failed. A
/// window has a start, a deadline and an outcome, so "3 von 4 diese Woche" is a
/// period with a result rather than a tally somebody increments.
///
/// All invariants live here rather than in the service, and neither the current
/// time nor new ids are read from ambient state: both are parameters. That is
/// what makes seeds and tests reproducible.
/// </remarks>
public sealed class Goal
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 1000;
    /// <summary>
    /// How many people may check a goal, not counting its owner.
    /// </summary>
    /// <remarks>
    /// One fewer than a conversation holds, because the goal's conversation
    /// seats its owner beside every checker. Two separate twenties let a goal
    /// take a checker its conversation then refused, as a 400 the sheets could
    /// not place next to any field (#130).
    /// </remarks>
    public const int MaxParticipants = Conversation.MaxParticipants - 1;

    /// <summary>The most uses a Don't may allow per period. Beyond it, it is not a limit.</summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// How far ahead a one-off's day may lie, in days. A year: further than that
    /// is not a commitment somebody can be checked on, it is a note to self.
    /// </summary>
    public const int MaxTargetDaysAhead = 365;

    /// <summary>How many relapses in a row a streak survives; the next one ends it.</summary>
    public const int RelapsesBeforeStreakEnds = 3;

    private readonly List<GoalParticipant> _participants = [];
    private readonly List<GoalInstance> _instances = [];
    private readonly List<GoalPause> _pauses = [];
    private readonly List<GoalChange> _changes = [];

    // EF Core materialisation only.
    private Goal()
    {
        Title = string.Empty;
        Schedule = GoalSchedule.Once();
    }

    private Goal(
        Guid id,
        Guid ownerPersonId,
        string title,
        string? description,
        GoalCategory category,
        GoalSchedule schedule,
        bool isGroup,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt,
        GoalKind kind,
        int? limit,
        DontProofMode? proofMode)
    {
        Id = id;
        Kind = kind;
        Limit = limit;
        ProofMode = proofMode;
        OwnerPersonId = ownerPersonId;
        Title = title;
        Description = description;
        Category = category;
        Schedule = schedule;
        IsGroup = isGroup;
        ReminderAt = reminderAt;
        TargetDate = targetDate;
        CreatedAt = createdAt;
        Status = GoalStatus.Active;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Whose goal this is: the person who created it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Participants"/>, which is who it is shared
    /// <em>with</em>. Both can read it; only the owner is the one it belongs
    /// to, and only the owner delivers proof for it. Without this column every
    /// goal in the database would be visible to whoever signed in most recently
    /// (docs/adr/0011-authentication-with-identity.md).
    /// </remarks>
    public Guid OwnerPersonId { get; private set; }

    public string Title { get; private set; }

    /// <summary>Something to do, or something to stop doing.</summary>
    public GoalKind Kind { get; private set; }

    /// <summary>
    /// For a Don't: the most uses per period, 0 for "never". Null for a Do.
    /// Changed only by an edit, and then from the next window on, like its
    /// schedule — each window keeps the limit it opened with
    /// (docs/adr/0044-editing-a-running-goal.md).
    /// </summary>
    public int? Limit { get; private set; }

    /// <summary>For a Don't: what its friends get to judge. Null for a Do.</summary>
    public DontProofMode? ProofMode { get; private set; }

    public string? Description { get; private set; }

    /// <summary>What kind of thing this is about (#18).</summary>
    public GoalCategory Category { get; private set; } = GoalCategories.Default;

    /// <summary>
    /// The icon it is drawn with: its category's, never chosen on its own
    /// (<see cref="GoalCategories.IconFor"/>). Not stored.
    /// </summary>
    public string Icon => GoalCategories.IconFor(Category);

    /// <summary>When this is due, and how often it comes back.</summary>
    public GoalSchedule Schedule { get; private set; }

    /// <summary>True for a goal a whole group shares, rather than a pair of friends.</summary>
    public bool IsGroup { get; private set; }

    public GoalStatus Status { get; private set; }

    /// <summary>Local time of the daily reminder, when there is one.</summary>
    /// <remarks>
    /// A reminder, never a deadline. The deadline is the end of the window's
    /// last day; this is only when to be nudged during it.
    /// </remarks>
    public TimeOnly? ReminderAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The day a one-off goal is due by. Null for anything that repeats, which
    /// derives its own days from the schedule.
    /// </summary>
    public DateOnly? TargetDate { get; private set; }

    /// <summary>
    /// The first day a repeating goal counts for, when its owner chose one:
    /// the day it was made, or the day after (#96). Null for a goal made before
    /// there was a choice, and for a one-off — both start the day they were
    /// made.
    /// </summary>
    /// <remarks>
    /// Stored, because nothing else says it: a goal that starts tomorrow has no
    /// window today, and the maintenance pass has to know tomorrow which window
    /// comes first (<see cref="GoalMaintenance"/>).
    /// </remarks>
    public DateOnly? StartsOn { get; private set; }

    /// <summary>
    /// When this goal stopped running, either way. Null while it is still going.
    /// </summary>
    /// <remarks>
    /// One column for two exits, because <see cref="Status"/> already says
    /// which of them it was: <see cref="GoalStatus.Completed"/> for something
    /// carried through, <see cref="GoalStatus.Archived"/> for something put
    /// down. The archive needs to sort by when, and "when" is the same question
    /// in both cases.
    /// </remarks>
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<GoalParticipant> Participants => _participants;

    /// <summary>Every window this goal has had.</summary>
    public IReadOnlyList<GoalInstance> Instances => _instances;

    /// <summary>Every time out taken from this goal, running or over.</summary>
    public IReadOnlyList<GoalPause> Pauses => _pauses;

    /// <summary>Every edit made to this goal while it ran, in the order they were made.</summary>
    public IReadOnlyList<GoalChange> Changes => _changes;

    /// <summary>The edit whose waiting half has not applied yet, if there is one. Never more than one.</summary>
    public GoalChange? PendingChange => _changes.FirstOrDefault(change => change.IsPending);

    /// <summary>The first day the most recently applied edit counted for, if one ever applied.</summary>
    public DateOnly? LastChangeAppliedOn => _changes
        .Where(change => change.Status == GoalChangeStatus.Applied)
        .Max(change => change.AppliesOn);

    /// <summary>
    /// Creates a goal, validating every invariant. <paramref name="id"/> and
    /// <paramref name="createdAt"/> are supplied by the caller so the result is
    /// fully determined by its arguments.
    /// </summary>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Goal Create(
        Guid id,
        Guid ownerPersonId,
        string title,
        string? description,
        GoalCategory? category,
        GoalSchedule schedule,
        bool isGroup,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt,
        GoalKind kind = GoalKind.Do,
        int? limit = null,
        DontProofMode? proofMode = null,
        DateOnly? startsOn = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(kind))
        {
            errors[nameof(Kind)] = ["That is not a kind of goal."];
        }
        else if (kind == GoalKind.Dont)
        {
            // One shape: at most N per period, the period being the window.
            if (schedule.Kind != ScheduleKind.Limit)
            {
                errors[nameof(Schedule)] = ["A Don't is counted per day, week or month."];
            }

            if (limit is not { } n || n is < 0 or > MaxLimit)
            {
                errors[nameof(Limit)] = [$"A limit must be between 0 and {MaxLimit}."];
            }

            if (proofMode is not { } mode || !Enum.IsDefined(mode))
            {
                errors[nameof(ProofMode)] = ["Choose how your friends can check this."];
            }
        }
        else if (schedule.Kind == ScheduleKind.Limit)
        {
            errors[nameof(Schedule)] = ["Only a Don't has a limit."];
        }

        if (ownerPersonId == Guid.Empty)
        {
            errors[nameof(OwnerPersonId)] = ["A goal needs somebody it belongs to."];
        }

        var normalisedTitle = title?.Trim() ?? string.Empty;
        if (normalisedTitle.Length == 0)
        {
            errors[nameof(Title)] = ["A title is required."];
        }
        else if (normalisedTitle.Length > MaxTitleLength)
        {
            errors[nameof(Title)] = [$"A title may be at most {MaxTitleLength} characters long."];
        }

        var normalisedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalisedDescription is { Length: > MaxDescriptionLength })
        {
            errors[nameof(Description)] = [$"A description may be at most {MaxDescriptionLength} characters long."];
        }

        var normalisedCategory = category ?? GoalCategories.Default;
        if (!Enum.IsDefined(normalisedCategory))
        {
            errors[nameof(Category)] = ["That is not one of the goal categories."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        // A target date belongs to a one-off and to nothing else. Keeping it on
        // a repeating goal would leave two answers to "when is this due".
        var normalisedTarget = schedule.Kind == ScheduleKind.Once ? targetDate : null;

        var isDont = kind == GoalKind.Dont;

        var goal = new Goal(
            id,
            ownerPersonId,
            normalisedTitle,
            normalisedDescription,
            normalisedCategory,
            schedule,
            isGroup,
            reminderAt,
            normalisedTarget,
            createdAt,
            kind,
            isDont ? limit : null,
            isDont ? proofMode : null);

        // A one-off has its day; only what repeats starts on one of its own.
        goal.StartsOn = schedule.Repeats ? startsOn : null;
        return goal;
    }

    /// <summary>True for something to stop doing.</summary>
    public bool IsDont => Kind == GoalKind.Dont;

    /// <summary>
    /// A Don't's window that relapsed and whose period has not run out yet —
    /// the window "this week" still is, although it is decided.
    /// </summary>
    public GoalInstance? RelapsedInstanceAt(DateTimeOffset now) =>
        _instances.FirstOrDefault(instance =>
            instance.Status == GoalInstanceStatus.Relapse && instance.StartsAt <= now && now <= instance.DueAt);

    /// <summary>
    /// Logs one use of a Don't into the window running now. Returns null when
    /// there is none to log into — a Do, a stopped or paused goal, or a moment
    /// between windows.
    /// </summary>
    public DontUse? LogUse(Guid id, Guid? imageId, DateTimeOffset now)
    {
        if (!IsDont || Status != GoalStatus.Active || ActivePauseAt(now) is not null)
        {
            return null;
        }

        var window = CurrentInstance ?? RelapsedInstanceAt(now);
        return window?.LogUse(id, imageId, now);
    }

    /// <summary>
    /// Takes back the use just logged into the window running now, or returns
    /// null when it may not be — see <see cref="GoalInstance.UndoUse"/>.
    /// </summary>
    public DontUse? UndoUse(Guid useId, DateTimeOffset now) =>
        IsDont && Status == GoalStatus.Active ? CurrentInstance?.UndoUse(useId, now) : null;

    /// <summary>True for a running Don't that is checked by spot checks.</summary>
    public bool TakesSpotChecks => IsDont && ProofMode == DontProofMode.SpotCheck && Status == GoalStatus.Active;

    /// <summary>
    /// Draws a spot check for the owner's local <paramref name="day"/> at the
    /// moment the caller picked. Returns null when there is nothing to draw:
    /// not a spot-check Don't, paused, no open window, or already one that day.
    /// </summary>
    public SpotCheck? ScheduleSpotCheck(Guid id, DateOnly day, DateTimeOffset at, DateTimeOffset now)
    {
        if (!TakesSpotChecks || ActivePauseAt(now) is not null)
        {
            return null;
        }

        return CurrentInstance?.ScheduleSpotCheck(id, day, at);
    }

    /// <summary>
    /// Draws today's check for right now, or moves today's not-yet-asked one to
    /// now. Only for the diagnostics endpoint, which exists outside Staging and
    /// Production so E2E and manual testing can see a check without waiting
    /// for a random moment.
    /// </summary>
    internal bool ScheduleSpotCheckNow(Guid id, DateOnly day, DateTimeOffset now)
    {
        if (!TakesSpotChecks || ActivePauseAt(now) is not null || CurrentInstance is not { } window)
        {
            return false;
        }

        return window.SpotChecks.FirstOrDefault(check => check.Day == day) is { } existing
            ? existing.Reschedule(now)
            : window.ScheduleSpotCheck(id, day, now) is not null;
    }

    /// <summary>Opens the checks whose moment has come, to be announced to the owner.</summary>
    public IReadOnlyList<SpotCheck> IssueDueSpotChecks(DateTimeOffset now) =>
        TakesSpotChecks && ActivePauseAt(now) is null && CurrentInstance is { } window
            ? window.IssueDueSpotChecks(now)
            : [];

    /// <summary>The check the owner can answer right now, if there is one.</summary>
    public SpotCheck? OpenSpotCheckAt(DateTimeOffset now) => CurrentInstance?.OpenSpotCheckAt(now);

    /// <summary>Answers an open check with a live photograph. Null when there is none to answer.</summary>
    public ProofPhoto? AnswerSpotCheck(Guid checkId, Guid proofId, Guid imageId, DateTimeOffset now) =>
        CurrentInstance?.AnswerSpotCheck(checkId, proofId, OwnerPersonId, imageId, now);

    /// <summary>
    /// The first local day of a Don't's current clean run: the day after its
    /// last relapse or miss, or its first day. What "seit X Tagen" counts from.
    /// </summary>
    /// <param name="dayOf">The owner's local day of an instant.</param>
    public DateOnly? RunStartedOn(Func<DateTimeOffset, DateOnly> dayOf)
    {
        ArgumentNullException.ThrowIfNull(dayOf);

        if (!IsDont || _instances.Count == 0)
        {
            return null;
        }

        // A relapse breaks the run on the day it was logged; a miss is only
        // known at the end of its window.
        var breaks = _instances
            .Where(instance => instance.Status is GoalInstanceStatus.Relapse or GoalInstanceStatus.Missed)
            .Select(instance => instance.Status == GoalInstanceStatus.Relapse && instance.ResolvedAt is { } at
                ? dayOf(at)
                : instance.DueOn)
            .ToList();

        return breaks.Count == 0
            ? _instances.Min(instance => instance.StartsOn)
            : breaks.Max().AddDays(1);
    }

    /// <summary>The window that is currently open, if there is one.</summary>
    public GoalInstance? CurrentInstance =>
        _instances.FirstOrDefault(instance => instance.Status == GoalInstanceStatus.Open);

    /// <summary>The window with the latest deadline, whatever became of it.</summary>
    public GoalInstance? LatestInstance =>
        _instances.OrderByDescending(instance => instance.DueAt).FirstOrDefault();

    /// <summary>
    /// Opens a window, unless one is already open or the goal is not running.
    /// Returns the window, or null when nothing was opened.
    /// </summary>
    /// <remarks>
    /// The one door windows come through, so "never two open at once" is a
    /// property of the model rather than of whoever remembered to check. Goal
    /// creation and the maintenance job both go through it.
    /// </remarks>
    public GoalInstance? OpenWindow(Guid id, GoalWindow window, DateTimeOffset startsAt, DateTimeOffset dueAt)
    {
        if (Status != GoalStatus.Active || CurrentInstance is not null)
        {
            return null;
        }

        // The same window twice is what a maintenance job that ran twice would
        // produce, and it would double every deadline in the history grid.
        if (_instances.Any(instance => instance.StartsOn == window.Start && instance.DueOn == window.End))
        {
            return null;
        }

        // A Don't's window takes a photograph only when its proof mode asks
        // for one at the end; the limit comes along so the window knows it.
        // A window an edit cut short holds its share of the limit, the way a
        // Do's holds its share of the quota (GoalSchedule.NextWindow).
        var instance = IsDont
            ? GoalInstance.Create(
                id,
                Id,
                window with { RequiredProofs = ProofMode == DontProofMode.ProofAtEnd ? 1 : 0 },
                startsAt,
                dueAt,
                GoalSchedule.Prorate(
                    Limit ?? 0,
                    window.End.DayNumber - window.Start.DayNumber + 1,
                    Schedule.PeriodDaysAround(window.End),
                    minimum: 0))
            : GoalInstance.Create(id, Id, window, startsAt, dueAt);
        _instances.Add(instance);
        return instance;
    }

    /// <summary>
    /// Delivers a photograph into the open window for friends to vote on.
    /// Returns null when the window is not taking one.
    /// </summary>
    /// <remarks>
    /// Only the owner delivers. Everybody else on the goal is there to look at
    /// what was delivered, which is the whole shape of the product: the person
    /// who made the promise does not get to mark their own homework.
    /// </remarks>
    public ProofPhoto? SubmitProof(
        Guid id,
        Guid imageId,
        ProofSource source,
        DateTimeOffset now) =>
        CurrentInstance?.SubmitProof(id, OwnerPersonId, imageId, source, now);

    /// <summary>
    /// Applies a decided vote, and completes a one-off goal when its only
    /// window closes. Returns false when nothing moved.
    /// </summary>
    public bool ApplyProofOutcome(ProofPhoto proof, VotingResult result, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proof);

        var instance = _instances.FirstOrDefault(candidate => candidate.Id == proof.GoalInstanceId);

        if (instance is null || !instance.ApplyProofOutcome(proof, result, now))
        {
            return false;
        }

        // A one-off is finished the moment its window is: there is no next one,
        // and leaving it "active" forever would keep it on the list of things
        // still to do.
        if (!Schedule.Repeats && instance.Status == GoalInstanceStatus.Done)
        {
            Status = GoalStatus.Completed;
            ClosedAt = now;

            // A finished one-off has nothing left for a waiting change to change.
            PendingChange?.Supersede(now);
        }

        return true;
    }

    /// <summary>
    /// Counts a believed proof with no photograph behind it. Seeding only.
    /// </summary>
    /// <remarks>
    /// A seeded world has to contain finished windows, and it cannot contain
    /// the pictures that finished them: a seed is a pure function over its
    /// context and may not write files (AGENTS.md section 7). So it states the
    /// outcome and stays silent about the evidence — which is also what a real
    /// window looks like once its proofs have been deleted.
    ///
    /// Internal, and the only door of its kind. Everything a person does goes
    /// through <see cref="SubmitProof"/> and a vote.
    /// </remarks>
    internal bool SeedDeliveredProof(DateTimeOffset now)
    {
        if (CurrentInstance is not { } instance || !instance.RecordProof(now))
        {
            return false;
        }

        if (!Schedule.Repeats && instance.Status == GoalInstanceStatus.Done)
        {
            Status = GoalStatus.Completed;
            ClosedAt = now;
        }

        return true;
    }

    /// <summary>
    /// How many people are entitled to vote on this goal's proofs.
    /// </summary>
    /// <remarks>
    /// The participants, and not the owner — they delivered it. Zero is a real
    /// and common answer: a goal nobody shares confirms its own proofs at once,
    /// because there is nobody to ask (<see cref="ProofVoting.Evaluate"/>).
    /// </remarks>
    public int VoterCount => _participants.Count;

    /// <summary>True when this person may vote on this goal's proofs.</summary>
    public bool CanVote(Guid personId) =>
        personId != OwnerPersonId && _participants.Any(p => p.PersonId == personId);

    /// <summary>
    /// Consecutive delivered windows, counting back from the most recent one
    /// that has been resolved.
    /// </summary>
    /// <remarks>
    /// Derived, never stored — the same rule as every other number q2 shows
    /// (AGENTS.md section 4). It is the chain that breaks: one missed window
    /// ends it, and the window that is currently open does not, because it has
    /// not failed yet and saying so before the deadline would be a claim about
    /// the future.
    ///
    /// A Don't's relapse holds it: it neither adds one nor ends the chain, so
    /// owning up costs less than being caught. The third relapse in a row ends
    /// it like a miss, or honesty would be a way never to lose a streak
    /// (docs/adr/0037-donts.md). <see cref="StreakOf"/> is the one place this
    /// is counted; the conversation counts it forwards with the same rules.
    /// </remarks>
    public int Streak => StreakOf(_instances);

    /// <summary>The streak a sequence of windows adds up to, with the rules above.</summary>
    internal static int StreakOf(IEnumerable<GoalInstance> instances)
    {
        var streak = 0;
        var relapsesInARow = 0;

        foreach (var instance in instances
            .Where(instance => instance.Status is GoalInstanceStatus.Done or GoalInstanceStatus.Missed or GoalInstanceStatus.Relapse)
            .OrderByDescending(instance => instance.DueAt))
        {
            if (instance.Status == GoalInstanceStatus.Missed)
            {
                break;
            }

            if (instance.Status == GoalInstanceStatus.Relapse)
            {
                if (++relapsesInARow >= RelapsesBeforeStreakEnds)
                {
                    break;
                }

                continue;
            }

            relapsesInARow = 0;
            streak++;
        }

        return streak;
    }

    /// <summary>How many windows were kept, missed, and relapsed.</summary>
    /// <remarks>
    /// The balance a profile shows. Returned together because the numbers
    /// only mean anything beside each other: "47" is a boast, "47 · 5" is a
    /// record. A relapse is its own number and never added to the misses.
    /// </remarks>
    public (int Done, int Missed, int Relapsed) Balance => (
        _instances.Count(instance => instance.Status == GoalInstanceStatus.Done),
        _instances.Count(instance => instance.Status == GoalInstanceStatus.Missed),
        _instances.Count(instance => instance.Status == GoalInstanceStatus.Relapse));

    /// <summary>The local days this goal was delivered on. What a streak of days is read from.</summary>
    public IEnumerable<DateOnly> DeliveredDays =>
        _instances
            .Where(instance => instance.Status == GoalInstanceStatus.Done)
            .Select(instance => instance.DueOn);

    /// <summary>
    /// Stops the goal for good, keeping everything it produced. Returns false
    /// when it had already stopped.
    /// </summary>
    /// <remarks>
    /// The exit that was missing. Before it there was one way to stop — delete
    /// it for everybody — so anybody who wanted to stop after half a year had
    /// to destroy their own record to do it. Here the history, the streak it
    /// reached and every photograph stay; only new deadlines cease.
    ///
    /// <paramref name="completed"/> is the difference between the two exits,
    /// and it is the person's own claim about what happened rather than
    /// anything derived: <see cref="GoalStatus.Completed"/> for "I carried this
    /// through", <see cref="GoalStatus.Archived"/> for "I am stopping". The
    /// balance is unaffected either way, which is the point — an exit that made
    /// the record worse is an exit nobody would take.
    /// </remarks>
    public bool Close(bool completed, DateTimeOffset now)
    {
        if (Status != GoalStatus.Active)
        {
            return false;
        }

        Status = completed ? GoalStatus.Completed : GoalStatus.Archived;
        ClosedAt = now;

        // The open window is set aside, never failed. Stopping is not a miss,
        // and a closing that worsened the balance would be one nobody used.
        CurrentInstance?.Suspend();

        // Nor does a change wait for windows that will never open.
        PendingChange?.Supersede(now);

        return true;
    }

    /// <summary>
    /// Moves the daily reminder, or takes it away with null.
    /// </summary>
    /// <remarks>
    /// Not an edit (<see cref="Edit"/>): it nudges only the owner, so nobody who
    /// agreed to check the goal agreed to anything it touches, and it leaves
    /// no line in the conversation. A stopped goal keeps whatever it had,
    /// because there is nothing left to be reminded of. Returns false then.
    /// </remarks>
    public bool SetReminder(TimeOnly? reminderAt)
    {
        if (IsClosed)
        {
            return false;
        }

        ReminderAt = reminderAt;
        return true;
    }

    /// <summary>True once this goal has stopped and belongs in the archive.</summary>
    public bool IsClosed => Status != GoalStatus.Active;

    /// <summary>The pause holding this goal right now, if there is one.</summary>
    public GoalPause? ActivePauseAt(DateTimeOffset now) =>
        _pauses.FirstOrDefault(pause => pause.IsActiveAt(now));

    /// <summary>True when a pause covered that local day.</summary>
    public bool IsPausedOn(DateOnly day) => _pauses.Any(pause => pause.Covers(day));

    /// <summary>
    /// How many pauses are left in the month <paramref name="day"/> lies in.
    /// </summary>
    /// <remarks>
    /// Overturned pauses count against the allowance on purpose. An objection
    /// should not make the attempt free, or the allowance could be worked
    /// around by simply trying again. A pause counts in the month it starts —
    /// a holiday entered in October for November uses November's (#99) — and
    /// one planned and taken back before it began counts nowhere: it never
    /// happened.
    /// </remarks>
    public int RemainingPauses(DateOnly day) =>
        Math.Max(0, PauseRules.MaxPerMonth - _pauses.Count(pause =>
            !pause.WasWithdrawn && pause.StartsOn.Year == day.Year && pause.StartsOn.Month == day.Month));

    /// <summary>The next pause planned ahead and not begun yet, if any (#99).</summary>
    public GoalPause? PlannedPauseAt(DateTimeOffset now) =>
        _pauses.Where(pause => pause.IsPlannedAt(now)).OrderBy(pause => pause.StartsOn).FirstOrDefault();

    /// <summary>
    /// Why this goal cannot be set aside from <paramref name="startsOn"/> for
    /// <paramref name="days"/> days, or null when it can.
    /// </summary>
    /// <remarks>
    /// One answer for a single pause and for "alle aussetzen", which skips the
    /// goals this refuses rather than failing all of them (#99). The reason and
    /// the length are the request's to get right and are checked when the
    /// pause is made.
    /// </remarks>
    public PauseRefusal? PauseRefusalFor(DateOnly startsOn, int days, DateOnly today, DateTimeOffset now)
    {
        if (Status != GoalStatus.Active)
        {
            return PauseRefusal.Stopped;
        }

        if (startsOn < today || startsOn > today.AddDays(PauseRules.MaxDaysAhead))
        {
            return PauseRefusal.OutOfRange;
        }

        var endsOn = startsOn.AddDays(Math.Max(PauseRules.MinDays, days) - 1);

        // Never two at once — running, planned, or one of each.
        if ((startsOn == today && ActivePauseAt(now) is not null)
            || _pauses.Any(pause => pause.Overlaps(startsOn, endsOn)))
        {
            return PauseRefusal.AlreadyPaused;
        }

        if (RemainingPauses(startsOn) <= 0)
        {
            return PauseRefusal.NoneLeft;
        }

        // A pause that begins later sets aside whatever window is open then,
        // not the one running now — so only one that begins today is held up
        // by what is happening in it.
        if (startsOn > today)
        {
            return null;
        }

        // Nor may it be the way out of a spot check that has just been asked:
        // a pause requested in its three minutes would void the very check it
        // was meant to answer.
        if (CurrentInstance?.OpenSpotCheckAt(now) is not null)
        {
            return PauseRefusal.SpotCheckOpen;
        }

        // A vote in flight is the one thing a pause may not interrupt; see
        // GoalInstance.Suspend for why.
        return CurrentInstance?.PendingProof is not null ? PauseRefusal.VoteRunning : null;
    }

    /// <summary>
    /// Sets the goal aside for whole local days — from today, suspending the
    /// open window, or from a later day, planned ahead (#99).
    /// </summary>
    /// <remarks>
    /// The one door a pause comes through, so the allowance and "never two at
    /// once" are properties of the goal rather than of whoever remembered to
    /// check. A planned pause is not announced and holds nothing until its
    /// first day; <see cref="BeginPause"/> and the maintenance pass see to that.
    /// </remarks>
    /// <param name="startsOn">The first day it covers; today when left out.</param>
    /// <param name="startOfDay">The first instant of a local day in the owner's zone; needed for a later start.</param>
    /// <exception cref="DomainValidationException">
    /// The goal has stopped, a pause is already running or planned for those
    /// days, the allowance is used up, a photograph is being voted on, or the
    /// reason, the length or the first day is not acceptable.
    /// </exception>
    public GoalPause RequestPause(
        Guid id,
        string? reason,
        int days,
        DateOnly today,
        Func<DateOnly, DateTimeOffset> endOfDay,
        DateTimeOffset now,
        DateOnly? startsOn = null,
        Func<DateOnly, DateTimeOffset>? startOfDay = null)
    {
        var first = startsOn ?? today;

        if (PauseRefusalFor(first, days, today, now) is { } refusal)
        {
            throw refusal switch
            {
                PauseRefusal.Stopped => new DomainValidationException("Pause", "A goal that has stopped cannot be paused."),
                PauseRefusal.OutOfRange => new DomainValidationException(
                    "StartsOn",
                    $"A pause starts today or within {PauseRules.MaxDaysAhead} days.",
                    first < today ? new FieldErrorCode(FieldErrorCodes.PastDay) : new FieldErrorCode(FieldErrorCodes.TooFarAhead, Max: PauseRules.MaxDaysAhead)),
                PauseRefusal.AlreadyPaused => new DomainValidationException("Pause", "This goal is already paused on those days."),
                PauseRefusal.NoneLeft => new DomainValidationException("Pause", $"A goal may be paused {PauseRules.MaxPerMonth} times a month."),
                PauseRefusal.SpotCheckOpen => new DomainValidationException("Pause", "Answer the spot check first."),
                _ => new DomainValidationException("Pause", "Wait for the photograph that is being voted on to be decided."),
            };
        }

        if (first > today)
        {
            ArgumentNullException.ThrowIfNull(startOfDay);

            var planned = GoalPause.Create(
                id,
                Id,
                OwnerPersonId,
                reason,
                goalInstanceId: null,
                first,
                days,
                startOfDay(first),
                endOfDay,
                now,
                announced: false);

            _pauses.Add(planned);
            return planned;
        }

        var current = CurrentInstance;
        var pause = GoalPause.Create(
            id,
            Id,
            OwnerPersonId,
            reason,
            current?.Id,
            today,
            days,
            now,
            endOfDay,
            now);

        current?.Suspend();
        _pauses.Add(pause);

        return pause;
    }

    /// <summary>
    /// When a planned pause's first day has come, sets aside the window open
    /// then, as asking for the pause that morning would have. Returns true when
    /// it did.
    /// </summary>
    /// <remarks>
    /// Only a window whose deadline falls inside the pause: one that ended
    /// before it began is not excused by it, and stays to be missed once the
    /// pause is over. Idempotent — the pause remembers the window it holds.
    /// </remarks>
    public bool BeginPause(DateTimeOffset now)
    {
        if (ActivePauseAt(now) is not { GoalInstanceId: null } pause
            || CurrentInstance is not { } open
            || open.DueAt < pause.StartsAt
            || !open.Suspend())
        {
            return false;
        }

        pause.Hold(open.Id);
        return true;
    }

    /// <summary>The running pauses the goal's friends have not been told about yet.</summary>
    public IEnumerable<GoalPause> PausesToAnnounce(DateTimeOffset now) =>
        _pauses.Where(pause => pause.AnnouncedAt is null && pause.IsActiveAt(now));

    /// <summary>
    /// Records one person's objection to the running pause, and lifts it when
    /// enough of them have. Returns the pause, or null when there is none to
    /// object to.
    /// </summary>
    public GoalPause? VetoPause(Guid vetoId, Guid personId, DateTimeOffset now)
    {
        if (ActivePauseAt(now) is not { } pause)
        {
            return null;
        }

        if (pause.ToggleVeto(vetoId, personId, _participants.Count, now))
        {
            // The window the pause set aside goes back exactly as it was, with
            // its original deadline — including one that has since passed.
            _instances.FirstOrDefault(instance => instance.Id == pause.GoalInstanceId)?.Resume();
        }

        return pause;
    }

    /// <summary>
    /// Ends the running pause early, or takes back one planned ahead. Returns
    /// false when there was neither.
    /// </summary>
    public bool EndPause(DateOnly today, DateTimeOffset now) =>
        (ActivePauseAt(now) ?? PlannedPauseAt(now))?.EndEarly(today, now) ?? false;

    /// <summary>
    /// Closes any pause whose last day has passed. Returns true when one did,
    /// so the caller knows it has to save.
    /// </summary>
    public bool ExpirePauses(DateTimeOffset now)
    {
        var changed = false;

        foreach (var pause in _pauses)
        {
            changed |= pause.Expire(now);
        }

        return changed;
    }

    /// <summary>
    /// Gives a one-off back the window a pause set aside, once that pause is
    /// over, with its deadline moved back by the days the pause covered (#129).
    /// Returns false when there was nothing to give back.
    /// </summary>
    /// <remarks>
    /// A repeating goal picks up with its next window after a pause; a one-off
    /// has no next window, so without this it stayed active with nothing to
    /// deliver into, nothing to miss and nothing to complete.
    ///
    /// Later by the paused days rather than with its old deadline: the pause
    /// says those days do not count, so the time that was left when it began is
    /// what is left when it ends. A deadline that has meanwhile passed anyway —
    /// a host that was down, say — is missed by the next step of maintenance,
    /// as any other would be. An objection that carried never gets here: it put
    /// the window back with its old deadline the moment it carried, because it
    /// says the pause should not have happened (docs/adr/0020-pause-and-archive.md).
    /// </remarks>
    public bool ReopenOneOffAfterPause(Func<DateOnly, DateTimeOffset> endOfDay, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endOfDay);

        if (Schedule.Repeats || IsClosed || ActivePauseAt(now) is not null
            || _instances.FirstOrDefault(instance => instance.Status == GoalInstanceStatus.Paused) is not { } window)
        {
            return false;
        }

        // The pause that set this window aside: the latest one that ended and
        // names it. Each ended pause hands the window back, so there is never
        // an older one still owed.
        var pause = _pauses
            .Where(candidate => candidate.Status == PauseStatus.Ended && candidate.GoalInstanceId == window.Id)
            .OrderByDescending(candidate => candidate.StartsAt)
            .FirstOrDefault();

        if (pause is null)
        {
            return false;
        }

        var from = pause.StartsOn > window.StartsOn ? pause.StartsOn : window.StartsOn;
        var paused = Math.Max(0, pause.EndsOn.DayNumber - from.DayNumber + 1);
        var dueOn = window.DueOn.AddDays(paused);

        return window.MoveDeadline(dueOn, endOfDay(dueOn)) && window.Resume();
    }

    /// <summary>A one-off's window while it can still be moved: running, or set aside by a pause.</summary>
    private GoalInstance? OneOffWindow => Schedule.Repeats
        ? null
        : _instances.FirstOrDefault(instance => instance.Status is GoalInstanceStatus.Open or GoalInstanceStatus.Paused);

    /// <summary>
    /// Works out what an edit would do, without doing it: what applies now,
    /// what waits for the next window, and whether the checkers may object.
    /// </summary>
    /// <remarks>
    /// The kind, the owner and everything that already happened — windows,
    /// photographs, votes, uses, checks — are not part of an edit at all. Nor
    /// is the shape of a schedule's promise: a one-off stays a one-off, and a
    /// repeating goal keeps repeating (docs/adr/0044-editing-a-running-goal.md).
    /// </remarks>
    /// <param name="today">The owner's local day, for a one-off's new day.</param>
    /// <exception cref="DomainValidationException">
    /// The goal has stopped, or something asked for is not allowed.
    /// </exception>
    public GoalEditPlan PlanEdit(GoalEdit edit, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edit);

        if (IsClosed)
        {
            throw new DomainValidationException("Goal", "A goal that has stopped cannot be edited.");
        }

        var errors = new FieldErrors();
        var pending = PendingChange;

        string? titleTo = null;
        if (edit.Title?.Trim() is { } title)
        {
            if (title.Length == 0)
            {
                errors.Add(nameof(Title), new(FieldErrorCodes.Required), "A title is required.");
            }
            else if (title.Length > MaxTitleLength)
            {
                errors.Add(
                    nameof(Title),
                    new(FieldErrorCodes.TooLong, Max: MaxTitleLength),
                    $"A title may be at most {MaxTitleLength} characters long.");
            }
            else if (title != Title)
            {
                titleTo = title;
            }
        }

        GoalCategory? categoryTo = null;
        if (edit.Category is { } category)
        {
            if (!Enum.IsDefined(category))
            {
                errors.Add(nameof(Category), new(FieldErrorCodes.UnknownOption), "That is not one of the goal categories.");
            }
            else if (category != Category)
            {
                categoryTo = category;
            }
        }

        // What is not asked for stays as the waiting change would leave it.
        var description = edit.Description is { } said
            ? (string.IsNullOrWhiteSpace(said) ? null : said.Trim())
            : pending is { DescriptionChanged: true } ? pending.DescriptionTo : Description;

        if (description is { Length: > MaxDescriptionLength })
        {
            errors.Add(
                nameof(Description),
                new(FieldErrorCodes.TooLong, Max: MaxDescriptionLength),
                $"A description may be at most {MaxDescriptionLength} characters long.");
        }

        var schedule = edit.Schedule ?? pending?.ScheduleTo ?? Schedule;
        if (IsDont != (schedule.Kind == ScheduleKind.Limit))
        {
            errors.Add(
                nameof(Schedule),
                new(FieldErrorCodes.UnknownOption),
                IsDont ? "A Don't is counted per day, week or month." : "Only a Don't has a limit.");
        }
        else if (schedule.Repeats != Schedule.Repeats)
        {
            errors.Add(
                nameof(Schedule),
                new(FieldErrorCodes.KindFixed),
                "A one-off stays a one-off, and a repeating goal keeps repeating.");
        }

        int? limitTo = null;
        DontProofMode? proofModeTo = null;

        if (!IsDont)
        {
            if (edit.Limit is not null)
            {
                errors.Add(nameof(Limit), new(FieldErrorCodes.UnknownOption), "Only a Don't has a limit.");
            }

            if (edit.ProofMode is not null)
            {
                errors.Add(nameof(ProofMode), new(FieldErrorCodes.UnknownOption), "Only a Don't has a proof mode.");
            }
        }
        else
        {
            var limit = edit.Limit ?? pending?.LimitTo ?? Limit ?? 0;
            if (limit is < 0 or > MaxLimit)
            {
                errors.Add(
                    nameof(Limit),
                    new(FieldErrorCodes.OutOfRange, Min: 0, Max: MaxLimit),
                    $"A limit must be between 0 and {MaxLimit}.");
            }
            else if (limit != Limit)
            {
                limitTo = limit;
            }

            var mode = edit.ProofMode ?? pending?.ProofModeTo ?? ProofMode ?? DontProofMode.HonestOnly;
            if (!Enum.IsDefined(mode))
            {
                errors.Add(nameof(ProofMode), new(FieldErrorCodes.UnknownOption), "Choose how your friends can check this.");
            }
            else if (mode != ProofMode)
            {
                proofModeTo = mode;
            }
        }

        var window = OneOffWindow;
        var dueFrom = Schedule.Repeats ? (DateOnly?)null : window?.DueOn ?? TargetDate;
        var waitingLater = pending?.Deferred.TargetDate;
        DateOnly? earlier = null;
        DateOnly? later = null;

        if ((edit.TargetDate ?? waitingLater) is { } target)
        {
            if (Schedule.Repeats)
            {
                errors.Add(nameof(TargetDate), new(FieldErrorCodes.KindFixed), "Only a one-off has a day.");
            }
            else if (window is null || dueFrom is not { } due)
            {
                errors.Add(nameof(TargetDate), new(FieldErrorCodes.Invalid), "This one-off has no running window to move.");
            }
            else if (target < today && target != due)
            {
                errors.Add(nameof(TargetDate), new(FieldErrorCodes.PastDay), "A one-off cannot be due on a day that has already passed.");
            }
            else if (target > today.AddDays(MaxTargetDaysAhead))
            {
                errors.Add(
                    nameof(TargetDate),
                    new(FieldErrorCodes.TooFarAhead, Max: MaxTargetDaysAhead),
                    $"A one-off may be due at most {MaxTargetDaysAhead} days ahead.");
            }
            else if (target < due)
            {
                earlier = target;
            }

            // A later day waits a day for objections, so it can only be asked
            // for while the deadline is still that far off — or it would be a
            // way to slip past the one that is about to pass.
            else if (target > due)
            {
                if (target != waitingLater && window.DueAt - now < GoalChangeRules.ObjectionPeriod)
                {
                    errors.Add(
                        nameof(TargetDate),
                        new(FieldErrorCodes.TooLateToMove),
                        "A one-off can be moved to a later day only while its deadline is at least a day away.");
                }
                else
                {
                    later = target;
                }
            }
        }

        var checkers = _participants.Select(participant => participant.PersonId).ToHashSet();
        var wanted = edit.CheckerIds is { } ids
            ? ids.Where(personId => personId != OwnerPersonId).ToHashSet()
            : checkers.Except(pending?.RemovedCheckerIds ?? []).ToHashSet();

        // A goal is made in front of somebody, and stays in front of somebody
        // (docs/adr/0027-goal-conversations.md). One from before a friend was
        // required has nobody to lose, and can still be edited.
        if (wanted.Count == 0 && checkers.Count > 0)
        {
            errors.Add("ParticipantIds", new(FieldErrorCodes.NoChecker), "A goal needs at least one friend to check it.");
        }
        else if (wanted.Count > MaxParticipants)
        {
            errors.Add(
                "ParticipantIds",
                new(FieldErrorCodes.TooMany, Max: MaxParticipants),
                $"A goal may have at most {MaxParticipants} participants.");
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var descriptionChanged = description != Description;
        var terms = new DeferredTerms(
            schedule.SameAs(Schedule) ? null : schedule,
            limitTo,
            proofModeTo,
            later,
            descriptionChanged,
            descriptionChanged ? description : null,
            [.. checkers.Except(wanted).Order()]);

        var deferred = terms.IsEmpty ? null : terms;

        return new GoalEditPlan(
            Title,
            titleTo,
            Category,
            categoryTo,
            dueFrom,
            earlier,
            [.. wanted.Except(checkers).Order()],
            deferred,
            deferred is not null && GoalChangeRules.IsEasier(this, deferred),
            KeepsPending: deferred is not null && pending is not null && deferred.SameAs(pending.Deferred),
            WithdrawsPending: deferred is null && pending is not null);
    }

    /// <summary>
    /// Carries out a planned edit: what applies now, applies; what waits is
    /// recorded, replacing whatever was waiting. Returns the edit's row, or
    /// null when there is nothing new to record.
    /// </summary>
    /// <remarks>
    /// The caller adds an added checker to the goal's conversation and tells
    /// them; the goal only knows its own participants.
    /// </remarks>
    /// <param name="newId">Hands out the ids of the participant and checker rows.</param>
    /// <param name="endOfDay">The last instant of a local day in the owner's zone.</param>
    /// <exception cref="DomainValidationException">The goal has stopped since the plan was made.</exception>
    public GoalChange? Edit(
        Guid changeId,
        GoalEditPlan plan,
        Func<Guid> newId,
        Func<DateOnly, DateTimeOffset> endOfDay,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(newId);
        ArgumentNullException.ThrowIfNull(endOfDay);

        if (IsClosed)
        {
            throw new DomainValidationException("Goal", "A goal that has stopped cannot be edited.");
        }

        if (!plan.HasChanges)
        {
            return null;
        }

        if (plan.TitleTo is { } title)
        {
            Title = title;
        }

        if (plan.CategoryTo is { } category)
        {
            Category = category;
        }

        foreach (var personId in plan.CheckersAdded)
        {
            AddParticipant(newId(), personId);
        }

        if (plan.TargetDateTo is { } earlier)
        {
            MoveOneOffDeadline(earlier, endOfDay);
        }

        // One change waits at a time: a new one takes the old one's place,
        // and an edit that wants nothing to wait takes it back.
        if (plan.WithdrawsPending)
        {
            PendingChange?.Withdraw(now);
        }
        else if (plan.HasNewDeferred)
        {
            PendingChange?.Supersede(now);
        }

        if (!plan.HasImmediate && !plan.HasNewDeferred)
        {
            return null;
        }

        var change = GoalChange.Record(changeId, this, plan, newId, now);
        _changes.Add(change);
        return change;
    }

    /// <summary>
    /// Puts the waiting half of an edit into force. <see cref="GoalMaintenance"/>
    /// calls it as the window it applies with opens.
    /// </summary>
    /// <param name="on">The first local day it counts for.</param>
    internal void ApplyChange(GoalChange change, DateOnly on, Func<DateOnly, DateTimeOffset> endOfDay, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(endOfDay);

        if (change.ScheduleTo is { } schedule)
        {
            // A copy, because EF Core owns a schedule through exactly one row.
            Schedule = schedule.Copy();
        }

        if (change.LimitTo is { } limit)
        {
            Limit = limit;
        }

        if (change.ProofModeTo is { } mode)
        {
            ProofMode = mode;
        }

        if (change.DescriptionChanged)
        {
            Description = change.DescriptionTo;
        }

        if (change.Deferred.TargetDate is { } later)
        {
            MoveOneOffDeadline(later, endOfDay);
        }

        // Never the last one. The edit made sure somebody stays; this keeps it
        // true whatever happened in between.
        var removed = change.RemovedCheckerIds.ToHashSet();
        if (_participants.Any(participant => !removed.Contains(participant.PersonId)))
        {
            _participants.RemoveAll(participant => removed.Contains(participant.PersonId));
        }

        change.MarkApplied(on, now);
    }

    /// <summary>
    /// Applies a one-off's waiting change once its objection period is over.
    /// Returns false when there is nothing to apply yet.
    /// </summary>
    /// <remarks>
    /// A one-off has no next window for a change to wait for, so it applies
    /// into the window it has — but never while a photograph in it is being
    /// voted on: friends halfway through deciding must not have the people
    /// deciding changed under them.
    /// </remarks>
    internal bool ApplyDueOneOffChange(DateOnly today, Func<DateOnly, DateTimeOffset> endOfDay, DateTimeOffset now)
    {
        if (Schedule.Repeats
            || PendingChange is not { } change
            || !change.IsDueAt(now)
            || CurrentInstance?.PendingProof is not null)
        {
            return false;
        }

        ApplyChange(change, today, endOfDay, now);
        return true;
    }

    /// <summary>
    /// Records one checker's objection to the waiting change, or takes it back,
    /// and lifts the change when enough of them have objected. Returns the
    /// change, or null when nothing is open to objection.
    /// </summary>
    public GoalChange? VetoChange(Guid vetoId, Guid personId, DateTimeOffset now)
    {
        if (PendingChange is not { Objectable: true } change)
        {
            return null;
        }

        change.ToggleVeto(vetoId, personId, _participants.Count, now);
        return change;
    }

    /// <summary>Takes back the waiting change. Returns false when nothing was waiting.</summary>
    public bool WithdrawChange(DateTimeOffset now) => PendingChange?.Withdraw(now) ?? false;

    private void MoveOneOffDeadline(DateOnly day, Func<DateOnly, DateTimeOffset> endOfDay)
    {
        TargetDate = day;
        OneOffWindow?.MoveDeadline(day, endOfDay(day));
    }

    /// <summary>True when this person may read the goal: its owner, or somebody it is shared with.</summary>
    public bool IsVisibleTo(Guid personId) =>
        OwnerPersonId == personId || _participants.Any(p => p.PersonId == personId);

    /// <summary>
    /// Adds a participant. Adding the same person twice does nothing, and the
    /// owner is not added at all — they are already on it, by owning it.
    /// </summary>
    public void AddParticipant(Guid id, Guid personId)
    {
        if (personId == OwnerPersonId || _participants.Any(p => p.PersonId == personId))
        {
            return;
        }

        if (_participants.Count >= MaxParticipants)
        {
            throw new DomainValidationException(
                nameof(Participants),
                $"A goal may have at most {MaxParticipants} participants.");
        }

        _participants.Add(new GoalParticipant(id, Id, personId));
    }

    /// <summary>
    /// True when the open window's deadline has passed and nobody has resolved
    /// it yet — the state the maintenance job is about to turn into "missed".
    /// </summary>
    public bool IsOverdueAt(DateTimeOffset now) =>
        Status == GoalStatus.Active && CurrentInstance is { } instance && now > instance.DueAt;
}
