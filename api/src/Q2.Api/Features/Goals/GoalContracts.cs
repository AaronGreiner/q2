using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>How often a goal is due, as the API states it.</summary>
/// <remarks>
/// A tagged union flattened for JSON: <see cref="Kind"/> says which of the
/// other fields mean anything. The client renders and edits exactly what the
/// server would accept, because both read this one shape.
/// </remarks>
/// <param name="EveryDays">Interval only. 1 is "every day".</param>
/// <param name="Weekdays">Weekdays only, ascending, Monday is 1.</param>
/// <param name="Times">Times only: how many per period.</param>
/// <param name="Period">Times and Limit: the period counted over.</param>
public sealed record GoalScheduleResponse(
    ScheduleKind Kind,
    int? EveryDays,
    IReadOnlyList<Weekday> Weekdays,
    int? Times,
    QuotaPeriod? Period)
{
    public static GoalScheduleResponse From(GoalSchedule schedule) => new(
        schedule.Kind,
        schedule.EveryDays,
        schedule.Weekdays,
        schedule.Times,
        schedule.Period);
}

/// <summary>
/// One window of a goal: when it runs, what it takes, and what became of it.
/// </summary>
/// <param name="StartsOn">First local day of the window.</param>
/// <param name="DueOn">Last local day — the deadline is the end of it.</param>
/// <param name="DueAt">
/// The deadline as an instant, so a client can count down to it without
/// knowing the owner's time zone.
/// </param>
/// <param name="RemainingProofs">
/// Derived, so "noch 2 von 3" cannot disagree with the two numbers it is drawn
/// from.
/// </param>
/// <param name="PendingProofId">
/// The photograph friends are looking at right now, if there is one. What tells
/// a screen to say "wird geprüft" rather than offering the camera again.
/// </param>
/// <param name="AcceptsProof">
/// Whether a photograph may be delivered into this window at this moment.
/// Answered here rather than reconstructed from the other fields, because the
/// rule involves the attempt count and the pending vote as well as the quota —
/// and a client that got it wrong would offer a camera whose upload is refused.
/// </param>
/// <param name="Limit">For a Don't's window: how many uses it allows. Null for a Do.</param>
/// <param name="Uses">For a Don't's window: how many were logged. 0 for a Do.</param>
/// <param name="LatestUseId">
/// For a Don't's window: the use logged last, which its owner may take back for
/// a short while (<see cref="DontUse.UndoPeriod"/>, #110). Null without uses.
/// </param>
public sealed record GoalInstanceResponse(
    Guid Id,
    DateOnly StartsOn,
    DateOnly DueOn,
    DateTimeOffset DueAt,
    int RequiredProofs,
    int ConfirmedProofs,
    int RemainingProofs,
    GoalInstanceStatus Status,
    Guid? PendingProofId,
    bool AcceptsProof,
    int? Limit,
    int Uses,
    Guid? LatestUseId)
{
    public static GoalInstanceResponse From(GoalInstance instance) => new(
        instance.Id,
        instance.StartsOn,
        instance.DueOn,
        instance.DueAt,
        instance.RequiredProofs,
        instance.ConfirmedProofs,
        instance.RemainingProofs,
        instance.Status,
        instance.PendingProof?.Id,
        instance.AcceptsProof,
        instance.Limit,
        instance.Uses,
        instance.LatestUseId);
}

/// <summary>
/// What the API returns for a goal. Entities are never serialised directly:
/// this type is the contract, and it may only change together with the OpenAPI
/// document and the generated frontend types.
/// </summary>
/// <param name="Kind">Something to do, or something to stop doing.</param>
/// <param name="Limit">For a Don't: the most uses per period, 0 for "never".</param>
/// <param name="ProofMode">For a Don't: what its friends get to judge.</param>
/// <param name="Current">
/// The window that is open now, if there is one. Null for a goal that is
/// finished or archived. For a Don't that relapsed this period it is that
/// window, decided, until the period is over — "this week" is still the week
/// of the relapse.
/// </param>
/// <param name="WindowsRelapsed">Don't windows that ended in a relapse; never counted as missed.</param>
/// <param name="RunStartedOn">
/// For a Don't: the first local day of its current clean run, in the owner's
/// zone — what "seit X Tagen" counts from. Null for a Do, and when the owner's
/// calendar was not at hand.
/// </param>
/// <param name="Streak">
/// Consecutive delivered windows, derived server-side from the windows
/// themselves so the number can never disagree with the history behind it.
/// </param>
/// <param name="IsOverdue">
/// Derived server-side so every client agrees on it — the browser's clock and
/// time zone are not part of the contract.
/// </param>
/// <param name="Risk">
/// Set when this goal's open window is close enough to failing to say so, and
/// null the rest of the time.
///
/// Evaluated against the *owner's* evening, not the reader's clock, which is
/// why it is on the response at all rather than worked out by the client: the
/// hour the rule turns on is a fact about the person who made the promise.
/// </param>
/// <param name="Owner">
/// Who made this goal and delivers into it. Carried on every goal rather than
/// only on a friend's, so a list can say whose a row is without a second read;
/// the participants are the people checking, and never include the owner.
/// </param>
/// <param name="IsMine">
/// Whether the reader owns this goal. The one thing a client cannot work out
/// for itself here, and what decides whether it offers the camera, the pause
/// and the two exits at all — the server refuses them either way, but a button
/// that always fails is worse than no button.
/// </param>
/// <param name="ClosedAt">
/// When this goal stopped, either way. Null while it runs.
/// </param>
/// <param name="Pause">
/// The pause holding this goal right now, if there is one. Null the rest of the
/// time — including for a pause that has ended, which is history rather than
/// something a screen has to say anything about.
/// </param>
/// <param name="RemainingPauses">
/// How many pauses the owner has left this month. Only for the owner: nobody
/// else needs their allowance, and it is a fact about a person rather than
/// about the goal.
/// </param>
/// <param name="SpotCheck">
/// The spot check the owner can answer right now, with when its three minutes
/// end. Only for the owner, and never before it is asked: a check drawn for
/// later is not announced (docs/adr/0038-spot-checks.md).
/// </param>
/// <param name="PendingChange">
/// The edit waiting for the next window, if there is one: what it changes,
/// from when, and — for one that makes the goal easier — the objection to it,
/// as everybody on the goal sees it (docs/adr/0044-editing-a-running-goal.md).
/// </param>
public sealed record GoalResponse(
    Guid Id,
    string Title,
    string? Description,
    string Icon,
    GoalScheduleResponse Schedule,
    GoalStatus Status,
    bool IsGroup,
    GoalKind Kind,
    int? Limit,
    DontProofMode? ProofMode,
    GoalInstanceResponse? Current,
    int Streak,
    int WindowsDone,
    int WindowsMissed,
    int WindowsRelapsed,
    DateOnly? RunStartedOn,
    TimeOnly? ReminderAt,
    DateOnly? TargetDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<PersonSummary> Participants,
    PersonSummary Owner,
    bool IsOverdue,
    bool IsMine,
    RiskResponse? Risk,
    GoalPauseResponse? Pause,
    int? RemainingPauses,
    OpenSpotCheckResponse? SpotCheck,
    GoalChangeResponse? PendingChange)
{
    public static GoalResponse From(
        Goal goal,
        IReadOnlyDictionary<Guid, Person> people,
        Guid viewerId,
        DateTimeOffset now,
        LocalCalendar? ownerCalendar = null)
    {
        var (done, missed, relapsed) = goal.Balance;
        var isMine = goal.OwnerPersonId == viewerId;
        var current = goal.CurrentInstance ?? goal.RelapsedInstanceAt(now);

        return new GoalResponse(
            goal.Id,
            goal.Title,
            goal.Description,
            goal.Icon,
            GoalScheduleResponse.From(goal.Schedule),
            goal.Status,
            goal.IsGroup,
            goal.Kind,
            goal.Limit,
            goal.ProofMode,
            current is null ? null : GoalInstanceResponse.From(current),
            goal.Streak,
            done,
            missed,
            relapsed,
            ownerCalendar is null ? null : goal.RunStartedOn(ownerCalendar.DayOf),
            goal.ReminderAt,
            goal.TargetDate,
            goal.CreatedAt,
            goal.ClosedAt,

            // Sorted here rather than in the query, so every path agrees.
            // Without this, a freshly created goal comes back in the order the
            // caller listed the people while a later read comes back in
            // whatever order the database chose — the same goal, two different
            // avatar stacks, changing on reload. Participants have no
            // meaningful intrinsic order, so alphabetical is the predictable
            // choice.
            [.. goal.Participants
                .Where(p => people.ContainsKey(p.PersonId))
                .Select(p => PersonSummary.From(people[p.PersonId], now))
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)],

            // The loader puts every owner into `people`; an owner leaves only
            // together with their goals, so a missing one is a bug, not a case.
            PersonSummary.From(people[goal.OwnerPersonId], now),
            goal.IsOverdueAt(now),
            isMine,

            // Only when the caller supplied the owner's calendar. Without it
            // there is no honest answer — "is it evening" is a question about
            // somebody's zone, and guessing would warn half of Europe in the
            // morning.
            ownerCalendar is null || goal.CurrentInstance is not { } open
                ? null
                : RiskResponse.From(GoalRisk.Assess(open, ownerCalendar, now)),

            GoalPauseResponse.From(goal.ActivePauseAt(now), goal, viewerId),

            // The allowance runs by calendar month in the owner's zone, so
            // without their calendar there is no month to count in.
            isMine && ownerCalendar is not null
                ? goal.RemainingPauses(ownerCalendar.Today(now))
                : null,
            isMine && goal.OpenSpotCheckAt(now) is { ExpiresAt: { } expiresAt } check
                ? new OpenSpotCheckResponse(check.Id, expiresAt)
                : null,
            GoalChangeResponse.From(
                goal.PendingChange,
                goal,
                people,
                viewerId,
                now,
                ownerCalendar is null ? null : GoalMaintenance.WhenPendingChangeApplies(goal, ownerCalendar)));
    }
}

/// <summary>
/// One edit of a running goal, as everybody on it sees it: what changed, from
/// what to what, and what became of the part that waited.
/// </summary>
/// <remarks>
/// A field that did not change is null on both sides. Sentences are the
/// client's: this says which fields moved and how, never "Rhythmus geändert".
/// </remarks>
/// <param name="Status">What became of the waiting half; <c>None</c> when nothing waited.</param>
/// <param name="Objectable">Whether the waiting half makes the goal easier, so the checkers may object.</param>
/// <param name="ObjectionEndsAt">The earliest moment an objectable change can apply. Null otherwise.</param>
/// <param name="AppliesOn">
/// The first local day the waiting half counts for: once applied, the day it
/// did; while it waits, the day it will if nothing intervenes, when the
/// owner's calendar was at hand to work that out.
/// </param>
/// <param name="DescriptionChanged">Whether the description changed; <paramref name="DescriptionTo"/> is null when it was removed.</param>
/// <param name="TargetDateFrom">A one-off's day before the edit, when the edit moved it.</param>
/// <param name="CheckersAdded">Who started checking with this edit.</param>
/// <param name="CheckersRemoved">Who stops, or stopped, checking with it.</param>
/// <param name="VetoCount">Objections raised — a number, never names (<see cref="GoalPauseResponse"/>).</param>
/// <param name="VetoesRequired">How many it takes to lift the change.</param>
/// <param name="CanVeto">Whether the reader may object right now: a checker, and the change still open to it.</param>
public sealed record GoalChangeResponse(
    Guid Id,
    GoalChangeStatus Status,
    DateTimeOffset CreatedAt,
    bool Objectable,
    DateTimeOffset? ObjectionEndsAt,
    DateOnly? AppliesOn,
    DateTimeOffset? ResolvedAt,
    string? TitleFrom,
    string? TitleTo,
    string? IconFrom,
    string? IconTo,
    bool DescriptionChanged,
    string? DescriptionTo,
    GoalScheduleResponse? ScheduleFrom,
    GoalScheduleResponse? ScheduleTo,
    int? LimitFrom,
    int? LimitTo,
    DontProofMode? ProofModeFrom,
    DontProofMode? ProofModeTo,
    DateOnly? TargetDateFrom,
    DateOnly? TargetDateTo,
    IReadOnlyList<PersonSummary> CheckersAdded,
    IReadOnlyList<PersonSummary> CheckersRemoved,
    int VetoCount,
    int VetoesRequired,
    bool VetoedByMe,
    bool CanVeto)
{
    /// <param name="expectedOn">For a change still waiting: the day it is expected to apply, if known.</param>
    public static GoalChangeResponse? From(
        GoalChange? change,
        Goal goal,
        IReadOnlyDictionary<Guid, Person> people,
        Guid viewerId,
        DateTimeOffset now,
        DateOnly? expectedOn = null)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(people);

        if (change is null)
        {
            return null;
        }

        var open = change.IsPending && change.Objectable;

        return new GoalChangeResponse(
            change.Id,
            change.Status,
            change.CreatedAt,
            change.Objectable,
            change.Objectable ? change.ObjectionEndsAt : null,
            change.AppliesOn ?? (change.IsPending ? expectedOn : null),
            change.ResolvedAt,
            change.TitleFrom,
            change.TitleTo,
            change.IconFrom,
            change.IconTo,
            change.DescriptionChanged,
            change.DescriptionTo,
            change.ScheduleFrom is null ? null : GoalScheduleResponse.From(change.ScheduleFrom),
            change.ScheduleTo is null ? null : GoalScheduleResponse.From(change.ScheduleTo),
            change.LimitFrom,
            change.LimitTo,
            change.ProofModeFrom,
            change.ProofModeTo,
            change.TargetDateFrom,
            change.TargetDateTo,
            Summaries(change.AddedCheckerIds, people, now),
            Summaries(change.RemovedCheckerIds, people, now),
            change.VetoCount,
            PauseRules.VetoesRequired(goal.VoterCount),

            // Only the reader's own objection comes back; everybody else's is a count.
            change.HasVetoFrom(viewerId),
            open && goal.CanVote(viewerId));
    }

    /// <summary>
    /// Describes an edit that has not been saved: what the sheet shows before
    /// the owner commits to it. Its id is empty, because nothing was recorded.
    /// </summary>
    public static GoalChangeResponse Preview(
        GoalEditPlan plan,
        Goal goal,
        IReadOnlyDictionary<Guid, Person> people,
        DateTimeOffset now,
        DateOnly? expectedOn)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(people);

        var deferred = plan.HasNewDeferred ? plan.Deferred : null;
        var objectable = deferred is not null && plan.Objectable;

        return new GoalChangeResponse(
            Guid.Empty,
            deferred is null ? GoalChangeStatus.None : GoalChangeStatus.Pending,
            now,
            objectable,
            objectable ? now + GoalChangeRules.ObjectionPeriod : null,
            deferred is null ? null : expectedOn,
            null,
            plan.TitleTo is null ? null : plan.TitleFrom,
            plan.TitleTo,
            plan.IconTo is null ? null : plan.IconFrom,
            plan.IconTo,
            deferred?.DescriptionChanged ?? false,
            deferred?.Description,
            deferred?.Schedule is null ? null : GoalScheduleResponse.From(goal.Schedule),
            deferred?.Schedule is { } schedule ? GoalScheduleResponse.From(schedule) : null,
            deferred?.Limit is null ? null : goal.Limit,
            deferred?.Limit,
            deferred?.ProofMode is null ? null : goal.ProofMode,
            deferred?.ProofMode,
            plan.TargetDateTo is not null || deferred?.TargetDate is not null ? plan.TargetDateFrom : null,
            plan.TargetDateTo ?? deferred?.TargetDate,
            Summaries(plan.CheckersAdded, people, now),
            Summaries(deferred?.RemovedCheckers ?? [], people, now),
            0,
            PauseRules.VetoesRequired(goal.VoterCount + plan.CheckersAdded.Count),
            false,
            false);
    }

    private static IReadOnlyList<PersonSummary> Summaries(
        IEnumerable<Guid> ids,
        IReadOnlyDictionary<Guid, Person> people,
        DateTimeOffset now) =>
        [.. ids
            .Where(people.ContainsKey)
            .Select(id => PersonSummary.From(people[id], now))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)];
}

/// <summary>
/// What saving an edit would do, before it is saved.
/// </summary>
/// <param name="Change">
/// The edit as it would be recorded, or null when it would change nothing.
/// </param>
/// <param name="WithdrawsPending">
/// A change is waiting and this edit would take it back.
/// </param>
public sealed record GoalEditPreviewResponse(GoalChangeResponse? Change, bool WithdrawsPending);

/// <summary>A spot check that is open: answer it with a live photograph before <paramref name="ExpiresAt"/>.</summary>
public sealed record OpenSpotCheckResponse(Guid Id, DateTimeOffset ExpiresAt);

/// <summary>
/// A running pause, as everybody invited to the goal sees it.
/// </summary>
/// <param name="EndsAt">
/// The last instant it covers, so a client can say "noch 2 Tage" without
/// knowing the owner's time zone.
/// </param>
/// <param name="VetoCount">
/// How many objections have been raised — a number and never a list. The names
/// are not withheld from this type, they are never selected into it: doubting
/// somebody's illness by name is a thing nobody would do, and a mechanism
/// nobody uses protects nobody (<see cref="PauseRules"/>).
/// </param>
/// <param name="VetoesRequired">
/// How many it takes to lift the pause. Carried out so a screen can say how far
/// off that is without repeating a rule that lives on the server.
/// </param>
/// <param name="CanVeto">
/// Whether the reader may object at all: invited to the goal, and not the
/// person who asked for the pause.
/// </param>
public sealed record GoalPauseResponse(
    Guid Id,
    string Reason,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateTimeOffset EndsAt,
    int Days,
    int VetoCount,
    int VetoesRequired,
    bool VetoedByMe,
    bool CanVeto)
{
    public static GoalPauseResponse? From(GoalPause? pause, Goal goal, Guid viewerId)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return pause is null
            ? null
            : new GoalPauseResponse(
                pause.Id,
                pause.Reason,
                pause.StartsOn,
                pause.EndsOn,
                pause.EndsAt,
                pause.Days,
                pause.VetoCount,
                PauseRules.VetoesRequired(goal.VoterCount),

                // Only the reader's own objection comes back. Everybody else's
                // is a count, and stays one.
                pause.HasVetoFrom(viewerId),
                goal.CanVote(viewerId));
    }
}

/// <summary>
/// How close a window is to being missed.
/// </summary>
/// <param name="MissingProofs">How many confirmed proofs are still outstanding.</param>
/// <param name="RemainingDays">Days left including today.</param>
/// <remarks>
/// What is missing and how long there is left — and nothing else. No count of
/// past misses and no "again": the numbers are unpleasant enough on their own,
/// and at this point nothing has actually gone wrong yet.
/// </remarks>
public sealed record RiskResponse(RiskReason Reason, int MissingProofs, int RequiredProofs, int RemainingDays)
{
    public static RiskResponse? From(RiskAssessment? assessment) =>
        assessment is { } risk
            ? new RiskResponse(risk.Reason, risk.MissingProofs, risk.RequiredProofs, risk.RemainingDays)
            : null;
}

/// <summary>One person's part in a shared goal, as shown on the detail screen.</summary>
public sealed record GoalTeamMemberResponse(PersonSummary Person, int Streak);

/// <summary>
/// A goal plus everything only its own screen needs.
/// </summary>
/// <param name="History">
/// The windows that have already been resolved, newest first — what the history
/// grid is drawn from.
/// </param>
/// <param name="ConversationId">
/// The goal's own conversation, where its photographs are checked. Null only
/// for a goal nobody else is on, which was created before a friend was required.
/// </param>
public sealed record GoalDetailResponse(
    GoalResponse Goal,
    IReadOnlyList<GoalTeamMemberResponse> Team,
    IReadOnlyList<GoalInstanceResponse> History,
    Guid? ConversationId);

/// <summary>
/// The schedule, as a request states it.
/// </summary>
/// <remarks>
/// Nullable throughout for the same reason as the rest of the request types: a
/// missing field should produce our own field-level message rather than a
/// model-binding failure. <see cref="GoalRequestValidators"/> turns it into a
/// real <see cref="GoalSchedule"/>, which is where the invariants are.
/// </remarks>
/// <param name="Period">
/// For <see cref="ScheduleKind.Times"/>, a week or a month; for
/// <see cref="ScheduleKind.Limit"/>, a day, week or month.
/// </param>
public sealed record GoalScheduleRequest(
    ScheduleKind? Kind = null,
    int? EveryDays = null,
    IReadOnlyList<Weekday>? Weekdays = null,
    int? Times = null,
    QuotaPeriod? Period = null);

/// <summary>
/// Request body for setting a goal aside.
/// </summary>
/// <remarks>
/// Nullable for the same reason as every other request type here: a missing
/// reason should produce our own field-level message rather than a
/// model-binding failure.
/// </remarks>
public sealed record RequestPauseRequest(string? Reason = null, int? Days = null);

/// <summary>
/// Request body for stopping a goal.
/// </summary>
/// <param name="Completed">
/// True for "I carried this through", false for "I am stopping". The person's
/// own claim, and the only difference between the two exits — neither touches
/// the balance.
/// </param>
public sealed record CloseGoalRequest(bool? Completed = null);

/// <summary>
/// Request body for moving a goal's daily reminder.
/// </summary>
/// <param name="ReminderAt">
/// The local time to be nudged at, or null for no reminder. Null is an answer
/// here rather than a missing field, so an empty body takes the reminder away.
/// </param>
public sealed record SetReminderRequest(TimeOnly? ReminderAt = null);

/// <summary>
/// Request body for creating a goal.
/// </summary>
/// <param name="Kind">Do when left out, which is every goal made before Don'ts.</param>
/// <param name="Limit">A Don't only: the most uses per period, 0 for "never".</param>
/// <param name="ProofMode">A Don't only: what its friends get to judge.</param>
/// <remarks>
/// Every property is nullable on purpose: a missing title should produce our
/// own field-level validation message, not a model-binding failure.
/// </remarks>
public sealed record CreateGoalRequest(
    string? Title = null,
    string? Description = null,
    string? Icon = null,
    GoalScheduleRequest? Schedule = null,
    bool? IsGroup = null,
    TimeOnly? ReminderAt = null,
    DateOnly? TargetDate = null,
    IReadOnlyList<Guid>? ParticipantIds = null,
    GoalKind? Kind = null,
    int? Limit = null,
    DontProofMode? ProofMode = null);

/// <summary>
/// Request body for editing a running goal.
/// </summary>
/// <remarks>
/// Every field is optional, and one that is left out stays as it is — or as
/// the change already waiting would leave it (<see cref="GoalEdit"/>). What
/// applies at once and what waits for the next window is the server's
/// decision, and <c>POST /api/goals/{id}/change/preview</c> says which before
/// anything is saved.
/// </remarks>
/// <param name="Description">An empty string removes it.</param>
/// <param name="TargetDate">A one-off only.</param>
/// <param name="Limit">A Don't only.</param>
/// <param name="ProofMode">A Don't only.</param>
/// <param name="ParticipantIds">
/// Everybody who should check the goal. Somebody on it and missing here is
/// removed with the next window; somebody here and not on it is added at once,
/// and has to be a friend.
/// </param>
public sealed record EditGoalRequest(
    string? Title = null,
    string? Description = null,
    string? Icon = null,
    GoalScheduleRequest? Schedule = null,
    DateOnly? TargetDate = null,
    int? Limit = null,
    DontProofMode? ProofMode = null,
    IReadOnlyList<Guid>? ParticipantIds = null);

/// <summary>Request body for logging one use of a Don't.</summary>
/// <param name="ImageId">An optional photograph, uploaded as a proof image first.</param>
public sealed record LogUseRequest(Guid? ImageId = null);
