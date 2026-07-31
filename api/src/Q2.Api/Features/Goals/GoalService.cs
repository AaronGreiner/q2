using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.People;
using Q2.Api.Features.Streaks;
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
    CurrentPerson currentPerson,
    ActivityRecorder activity,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<GoalService> logger)
{
    public async Task<IReadOnlyList<GoalResponse>> ListAsync(GoalStatus? status, CancellationToken cancellationToken)
    {
        var query = database.Goals
            .AsNoTracking()
            .Include(g => g.Participants)
            .Include(g => g.Contributions)
            .AsQueryable();

        if (status is { } wanted)
        {
            query = query.Where(g => g.Status == wanted);
        }

        var goals = await query
            .OrderByDescending(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        var people = await LoadParticipantsAsync(goals, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return [.. goals.Select(goal => GoalResponse.From(goal, people, now))];
    }

    /// <exception cref="ResourceNotFoundException">No goal with that id exists.</exception>
    public async Task<GoalDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var goal = await database.Goals
            .AsNoTracking()
            .Include(g => g.Participants)
            .Include(g => g.Contributions)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Goal", id);

        var now = timeProvider.GetUtcNow();
        var today = Today();
        var people = await LoadParticipantsAsync([goal], cancellationToken);

        // Each team member's own streak, which is the number the detail screen
        // shows next to them — not the goal's.
        var memberIds = goal.Participants.Select(p => p.PersonId).ToList();
        var memberDays = await database.DailyCheckIns
            .AsNoTracking()
            .Where(c => memberIds.Contains(c.PersonId))
            .ToListAsync(cancellationToken);

        var team = goal.Participants
            .Where(p => people.ContainsKey(p.PersonId))
            .Select(p => new GoalTeamMemberResponse(
                PersonSummary.From(people[p.PersonId], now),
                Streak.Count(memberDays.Where(c => c.PersonId == p.PersonId).Select(c => c.Date), today)))
            .OrderBy(m => m.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tasks = await database.GoalTasks
            .AsNoTracking()
            .Where(t => t.GoalId == id)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return new GoalDetailResponse(
            GoalResponse.From(goal, people, now),
            team,
            [.. tasks.Select(task => GoalTaskResponse.From(task, today))]);
    }

    /// <exception cref="DomainValidationException">The request violates a domain rule.</exception>
    public async Task<GoalResponse> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var me = await currentPerson.GetAsync(cancellationToken);

        var goal = Goal.Create(
            idGenerator.NewId(),
            request.Title ?? string.Empty,
            request.Description,
            request.Icon,
            request.Rhythm ?? GoalRhythm.Daily,
            request.IsGroup ?? false,
            completedSteps: 0,
            request.TotalSteps ?? 30,
            request.ReminderAt,
            request.TargetDate,
            now);

        foreach (var personId in request.ParticipantIds ?? [])
        {
            goal.AddParticipant(idGenerator.NewId(), personId);
        }

        database.Goals.Add(goal);
        activity.Publish(me.Id, ActivityKind.GoalCreated, goal.Title, null, now, goal.Id);

        await database.SaveChangesAsync(cancellationToken);

        // The title is user content and may be personal — log the id only.
        logger.LogInformation(
            "Created goal {GoalId} with {StepCount} step(s) and {ParticipantCount} participant(s)",
            goal.Id,
            goal.TotalSteps,
            goal.Participants.Count);

        var people = await LoadParticipantsAsync([goal], cancellationToken);
        return GoalResponse.From(goal, people, now);
    }

    /// <summary>
    /// Records one step of progress towards a goal, which is also a day of
    /// self-care: it counts towards the person's own streak as well as the
    /// goal's.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">No goal with that id exists.</exception>
    public async Task<GoalResponse> ContributeAsync(Guid id, CancellationToken cancellationToken)
    {
        var goal = await database.Goals
            .Include(g => g.Participants)
            .Include(g => g.Contributions)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Goal", id);

        var now = timeProvider.GetUtcNow();
        var today = Today();
        var me = await currentPerson.GetAsync(cancellationToken);

        if (goal.Contribute(idGenerator.NewId(), today))
        {
            me.CheckIn(idGenerator.NewId(), today);
            activity.Publish(me.Id, ActivityKind.GoalProgress, goal.Title, goal.ProgressPercent, now, goal.Id);
            await database.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Recorded progress on goal {GoalId}: {CompletedSteps} of {TotalSteps}",
                goal.Id,
                goal.CompletedSteps,
                goal.TotalSteps);
        }

        var people = await LoadParticipantsAsync([goal], cancellationToken);
        return GoalResponse.From(goal, people, now);
    }

    /// <summary>
    /// Loads every person mentioned by these goals in one query, rather than
    /// letting the mapper pull them in one at a time.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Person>> LoadParticipantsAsync(
        IReadOnlyCollection<Goal> goals,
        CancellationToken cancellationToken)
    {
        var ids = goals.SelectMany(g => g.Participants).Select(p => p.PersonId).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Person>();
        }

        return await database.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
