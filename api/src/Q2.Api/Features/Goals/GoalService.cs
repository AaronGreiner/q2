using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Application logic for goals: reads, writes, and the mapping between the
/// domain model and the API contract.
/// </summary>
/// <remarks>
/// It talks to <see cref="Q2DbContext"/> directly. EF Core's DbContext already
/// is a unit of work plus repository, so wrapping it in another repository
/// layer would add indirection without adding a single testable behaviour
/// (see docs/adr/0003-backend-architecture.md).
/// </remarks>
public sealed class GoalService(
    Q2DbContext database,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<GoalService> logger)
{
    public async Task<IReadOnlyList<GoalResponse>> ListAsync(GoalStatus? status, CancellationToken cancellationToken)
    {
        var query = database.Goals.AsNoTracking().Include(g => g.Participants).AsQueryable();

        if (status is { } wanted)
        {
            query = query.Where(g => g.Status == wanted);
        }

        var goals = await query
            .OrderByDescending(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        var today = Today();
        return [.. goals.Select(goal => GoalResponse.From(goal, today))];
    }

    /// <exception cref="ResourceNotFoundException">No goal with that id exists.</exception>
    public async Task<GoalResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var goal = await database.Goals
            .AsNoTracking()
            .Include(g => g.Participants)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Goal", id);

        return GoalResponse.From(goal, Today());
    }

    /// <exception cref="DomainValidationException">The request violates a domain rule.</exception>
    public async Task<GoalResponse> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = Goal.Create(
            idGenerator.NewId(),
            request.Title ?? string.Empty,
            request.Description,
            request.ProgressPercent ?? 0,
            request.TargetDate,
            timeProvider.GetUtcNow());

        foreach (var participant in request.Participants ?? [])
        {
            goal.AddParticipant(idGenerator.NewId(), participant);
        }

        database.Goals.Add(goal);
        await database.SaveChangesAsync(cancellationToken);

        // The title is user content and may be personal — log the id only.
        logger.LogInformation(
            "Created goal {GoalId} with status {GoalStatus} and {ParticipantCount} participant(s)",
            goal.Id,
            goal.Status,
            goal.Participants.Count);

        return GoalResponse.From(goal, Today());
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
