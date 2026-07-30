namespace Q2.Api.Features.Goals;

/// <summary>
/// What the API returns for a goal. Entities are never serialised directly:
/// this type is the contract, and it may only change together with the OpenAPI
/// document and the generated frontend types.
/// </summary>
/// <param name="IsOverdue">
/// Derived server-side so every client agrees on it — the browser's clock and
/// time zone are not part of the contract.
/// </param>
public sealed record GoalResponse(
    Guid Id,
    string Title,
    string? Description,
    GoalStatus Status,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateOnly? TargetDate,
    IReadOnlyList<string> Participants,
    bool IsOverdue)
{
    public static GoalResponse From(Goal goal, DateOnly today) => new(
        goal.Id,
        goal.Title,
        goal.Description,
        goal.Status,
        goal.ProgressPercent,
        goal.CreatedAt,
        goal.TargetDate,

        // Sorted here rather than in the query, so every path agrees.
        // Without this, a freshly created goal comes back in the order the
        // caller typed the names while a later read comes back in whatever
        // order the database chose — the same goal, two different orders, and a
        // card summary ("Robin, Kim and 2 others") that changes on reload.
        // Participants have no meaningful intrinsic order, so alphabetical is
        // the predictable choice.
        [.. goal.Participants
            .Select(p => p.DisplayName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)],
        goal.IsOverdue(today));
}

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
    int? ProgressPercent = null,
    DateOnly? TargetDate = null,
    IReadOnlyList<string>? Participants = null);
