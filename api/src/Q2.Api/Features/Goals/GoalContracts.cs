using Q2.Api.Features.People;

namespace Q2.Api.Features.Goals;

/// <summary>
/// What the API returns for a goal. Entities are never serialised directly:
/// this type is the contract, and it may only change together with the OpenAPI
/// document and the generated frontend types.
/// </summary>
/// <param name="ProgressPercent">
/// Derived from the steps, so the ring and the "14 of 21" under it can never
/// disagree.
/// </param>
/// <param name="Streak">
/// Consecutive days this goal was worked on, derived server-side.
/// </param>
/// <param name="IsOverdue">
/// Derived server-side so every client agrees on it — the browser's clock and
/// time zone are not part of the contract.
/// </param>
public sealed record GoalResponse(
    Guid Id,
    string Title,
    string? Description,
    string Icon,
    GoalRhythm Rhythm,
    GoalStatus Status,
    bool IsGroup,
    int CompletedSteps,
    int TotalSteps,
    int ProgressPercent,
    int Streak,
    TimeOnly? ReminderAt,
    DateOnly? TargetDate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PersonSummary> Participants,
    bool IsOverdue)
{
    public static GoalResponse From(Goal goal, IReadOnlyDictionary<Guid, Person> people, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        return new GoalResponse(
            goal.Id,
            goal.Title,
            goal.Description,
            goal.Icon,
            goal.Rhythm,
            goal.Status,
            goal.IsGroup,
            goal.CompletedSteps,
            goal.TotalSteps,
            goal.ProgressPercent,
            goal.StreakOn(today),
            goal.ReminderAt,
            goal.TargetDate,
            goal.CreatedAt,

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
            goal.IsOverdue(today));
    }
}

/// <summary>One person's part in a shared goal, as shown on the detail screen.</summary>
public sealed record GoalTeamMemberResponse(PersonSummary Person, int Streak);

/// <summary>A goal plus everything only its own screen needs.</summary>
public sealed record GoalDetailResponse(
    GoalResponse Goal,
    IReadOnlyList<GoalTeamMemberResponse> Team,
    IReadOnlyList<GoalTaskResponse> Tasks);

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
    GoalRhythm? Rhythm = null,
    bool? IsGroup = null,
    int? TotalSteps = null,
    TimeOnly? ReminderAt = null,
    DateOnly? TargetDate = null,
    IReadOnlyList<Guid>? ParticipantIds = null);
