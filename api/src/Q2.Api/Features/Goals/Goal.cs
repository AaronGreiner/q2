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
    public const int MaxParticipants = 20;

    private readonly List<GoalParticipant> _participants = [];
    private readonly List<GoalInstance> _instances = [];
    private readonly List<GoalPause> _pauses = [];

    // EF Core materialisation only.
    private Goal()
    {
        Title = string.Empty;
        Icon = GoalIcons.Default;
        Schedule = GoalSchedule.Once();
    }

    private Goal(
        Guid id,
        Guid ownerPersonId,
        string title,
        string? description,
        string icon,
        GoalSchedule schedule,
        bool isGroup,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerPersonId = ownerPersonId;
        Title = title;
        Description = description;
        Icon = icon;
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

    public string? Description { get; private set; }

    /// <summary>One of <see cref="GoalIcons"/>, never a free-form icon name.</summary>
    public string Icon { get; private set; }

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
        string? icon,
        GoalSchedule schedule,
        bool isGroup,
        TimeOnly? reminderAt,
        DateOnly? targetDate,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var errors = new Dictionary<string, string[]>();

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

        var normalisedIcon = string.IsNullOrWhiteSpace(icon) ? GoalIcons.Default : icon.Trim();
        if (!GoalIcons.IsValid(normalisedIcon))
        {
            errors[nameof(Icon)] = ["That icon is not one of the available goal icons."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        // A target date belongs to a one-off and to nothing else. Keeping it on
        // a repeating goal would leave two answers to "when is this due".
        var normalisedTarget = schedule.Kind == ScheduleKind.Once ? targetDate : null;

        return new Goal(
            id,
            ownerPersonId,
            normalisedTitle,
            normalisedDescription,
            normalisedIcon,
            schedule,
            isGroup,
            reminderAt,
            normalisedTarget,
            createdAt);
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

        var instance = GoalInstance.Create(id, Id, window, startsAt, dueAt);
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
        bool capturedInApp,
        DateTimeOffset now) =>
        CurrentInstance?.SubmitProof(id, OwnerPersonId, imageId, capturedInApp, now);

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
    /// </remarks>
    public int Streak =>
        _instances
            .Where(instance => instance.Status is GoalInstanceStatus.Done or GoalInstanceStatus.Missed)
            .OrderByDescending(instance => instance.DueAt)
            .TakeWhile(instance => instance.Status == GoalInstanceStatus.Done)
            .Count();

    /// <summary>How many windows were delivered, and how many were missed.</summary>
    /// <remarks>
    /// The balance a profile shows. Returned together because the two numbers
    /// only mean anything beside each other: "47" is a boast, "47 · 5" is a
    /// record.
    /// </remarks>
    public (int Done, int Missed) Balance => (
        _instances.Count(instance => instance.Status == GoalInstanceStatus.Done),
        _instances.Count(instance => instance.Status == GoalInstanceStatus.Missed));

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
    /// How many pauses are left in the month <paramref name="today"/> lies in.
    /// </summary>
    /// <remarks>
    /// Overturned pauses count against the allowance on purpose. An objection
    /// should not make the attempt free, or the allowance could be worked
    /// around by simply trying again.
    /// </remarks>
    public int RemainingPauses(DateOnly today) =>
        Math.Max(0, PauseRules.MaxPerMonth - _pauses.Count(pause =>
            pause.StartsOn.Year == today.Year && pause.StartsOn.Month == today.Month));

    /// <summary>
    /// Sets the goal aside for whole local days, suspending the open window.
    /// </summary>
    /// <remarks>
    /// The one door a pause comes through, so the allowance and "never two at
    /// once" are properties of the goal rather than of whoever remembered to
    /// check.
    /// </remarks>
    /// <exception cref="DomainValidationException">
    /// The goal has stopped, a pause is already running, the allowance is used
    /// up, a photograph is being voted on, or the reason or length is not
    /// acceptable.
    /// </exception>
    public GoalPause RequestPause(
        Guid id,
        string? reason,
        int days,
        DateOnly today,
        Func<DateOnly, DateTimeOffset> endOfDay,
        DateTimeOffset now)
    {
        if (Status != GoalStatus.Active)
        {
            throw new DomainValidationException("Pause", "A goal that has stopped cannot be paused.");
        }

        if (ActivePauseAt(now) is not null)
        {
            throw new DomainValidationException("Pause", "This goal is already paused.");
        }

        if (RemainingPauses(today) <= 0)
        {
            throw new DomainValidationException(
                "Pause",
                $"A goal may be paused {PauseRules.MaxPerMonth} times a month.");
        }

        // A vote in flight is the one thing a pause may not interrupt; see
        // GoalInstance.Suspend for why.
        if (CurrentInstance?.PendingProof is not null)
        {
            throw new DomainValidationException(
                "Pause",
                "Wait for the photograph that is being voted on to be decided.");
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

    /// <summary>Ends the running pause early. Returns false when none was running.</summary>
    public bool EndPause(DateOnly today, DateTimeOffset now) =>
        ActivePauseAt(now)?.EndEarly(today, now) ?? false;

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

/// <summary>
/// The icons a goal may use.
/// </summary>
/// <remarks>
/// A closed set on purpose. The frontend bundles its icons at build time rather
/// than fetching them (see app/nuxt.config.ts), so an icon name the server
/// invented at runtime would simply not render. Adding one here means adding it
/// to that bundle in the same change.
/// </remarks>
public static class GoalIcons
{
    public const string Default = "target";

    public static readonly IReadOnlyList<string> All =
    [
        "target",
        "medal",
        "book-open",
        "sunrise",
        "droplet",
        "flame",
        "trophy",
        "sparkles",
        "calendar",
        "alarm-clock",
        "hand-heart",
        "users",
    ];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
