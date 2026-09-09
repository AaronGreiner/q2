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
/// <param name="Period">Times only.</param>
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
    bool AcceptsProof)
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
        instance.AcceptsProof);
}

/// <summary>
/// What the API returns for a goal. Entities are never serialised directly:
/// this type is the contract, and it may only change together with the OpenAPI
/// document and the generated frontend types.
/// </summary>
/// <param name="Current">
/// The window that is open now, if there is one. Null for a goal that is
/// finished or archived.
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
public sealed record GoalResponse(
    Guid Id,
    string Title,
    string? Description,
    string Icon,
    GoalScheduleResponse Schedule,
    GoalStatus Status,
    bool IsGroup,
    GoalInstanceResponse? Current,
    int Streak,
    int WindowsDone,
    int WindowsMissed,
    TimeOnly? ReminderAt,
    DateOnly? TargetDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<PersonSummary> Participants,
    bool IsOverdue,
    bool IsMine,
    RiskResponse? Risk,
    GoalPauseResponse? Pause,
    int? RemainingPauses)
{
    public static GoalResponse From(
        Goal goal,
        IReadOnlyDictionary<Guid, Person> people,
        Guid viewerId,
        DateTimeOffset now,
        LocalCalendar? ownerCalendar = null)
    {
        var (done, missed) = goal.Balance;
        var isMine = goal.OwnerPersonId == viewerId;

        return new GoalResponse(
            goal.Id,
            goal.Title,
            goal.Description,
            goal.Icon,
            GoalScheduleResponse.From(goal.Schedule),
            goal.Status,
            goal.IsGroup,
            goal.CurrentInstance is { } current ? GoalInstanceResponse.From(current) : null,
            goal.Streak,
            done,
            missed,
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
                : null);
    }
}

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
public sealed record GoalDetailResponse(
    GoalResponse Goal,
    IReadOnlyList<GoalTeamMemberResponse> Team,
    IReadOnlyList<GoalInstanceResponse> History);

/// <summary>How much of today is done. What the ring on the home screen shows.</summary>
/// <remarks>
/// Counted in windows due today rather than in ticked-off tasks: a window is the
/// only unit in this product that can be finished, and it is the unit somebody's
/// friends can see.
/// </remarks>
public sealed record DaySummaryResponse(int Done, int Total, int Percent)
{
    public static DaySummaryResponse From(int done, int total) =>
        new(done, total, total == 0 ? 0 : (int)Math.Round(done * 100d / total));
}

/// <summary>
/// The schedule, as a request states it.
/// </summary>
/// <remarks>
/// Nullable throughout for the same reason as the rest of the request types: a
/// missing field should produce our own field-level message rather than a
/// model-binding failure. <see cref="GoalRequestValidators"/> turns it into a
/// real <see cref="GoalSchedule"/>, which is where the invariants are.
/// </remarks>
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
/// Request body for creating a goal.
/// </summary>
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
    IReadOnlyList<Guid>? ParticipantIds = null);
