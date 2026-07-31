using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// The day's tasks: what is on it, and what happens when one is ticked off.
/// </summary>
/// <remarks>
/// Which tasks belong on a day is decided here rather than in the client,
/// because <see cref="GoalTask.IsScheduledFor"/> depends on the day and on the
/// rhythm, and two clients working that out separately is two chances to
/// disagree about whether Saturday counts.
/// </remarks>
public sealed class GoalTaskService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    ActivityRecorder activity,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<GoalTaskService> logger)
{
    /// <summary>
    /// Today's tasks, in the order they are shown.
    /// </summary>
    /// <param name="scheduledOnly">
    /// <c>true</c> — only what is due today. <c>false</c> — everything, which is
    /// what a "all tasks" view would want.
    /// </param>
    public async Task<IReadOnlyList<GoalTaskResponse>> ListAsync(bool scheduledOnly, CancellationToken cancellationToken)
    {
        var today = Today();
        var me = await currentPerson.GetAsync(cancellationToken);

        // The rhythm rules are C#, not SQL: expressing "Monday to Friday" and
        // "the day this weekly task falls on" as a translatable predicate would
        // be far harder to read than filtering a few dozen rows in memory. Who
        // the rows belong to *is* SQL, though — that filter must never be the
        // one that happens after the read.
        var tasks = await database.GoalTasks
            .AsNoTracking()
            .Where(t => t.OwnerPersonId == me.Id)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var relevant = scheduledOnly ? tasks.Where(t => t.IsScheduledFor(today)) : tasks;

        return [.. relevant.Select(task => GoalTaskResponse.From(task, today))];
    }

    /// <summary>How much of today is done. The number in the ring on the home screen.</summary>
    public async Task<DaySummaryResponse> SummariseTodayAsync(CancellationToken cancellationToken)
    {
        var today = Today();
        var me = await currentPerson.GetAsync(cancellationToken);

        var tasks = await database.GoalTasks
            .AsNoTracking()
            .Where(t => t.OwnerPersonId == me.Id)
            .ToListAsync(cancellationToken);
        var scheduled = tasks.Where(t => t.IsScheduledFor(today)).ToList();

        return DaySummaryResponse.From(scheduled.Count(t => t.IsDoneOn(today)), scheduled.Count);
    }

    /// <summary>
    /// Ticks a task off for today, or takes it back.
    /// </summary>
    /// <remarks>
    /// Ticking one off is a day of self-care, so it counts towards the streak
    /// and shows up in friends' feeds. Taking it back withdraws exactly the
    /// entry it published — but leaves the check-in alone: the day still
    /// happened, and one corrected list item does not undo it.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such task of yours.</exception>
    public async Task<GoalTaskResponse> ToggleAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        // Owner in the query, not in a check afterwards: somebody else's task
        // is not a task this person may tick off, and it is not one they get to
        // learn the existence of either.
        var task = await database.GoalTasks
            .SingleOrDefaultAsync(t => t.Id == id && t.OwnerPersonId == me.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Task", id);

        var now = timeProvider.GetUtcNow();
        var today = Today();

        if (task.Toggle(today))
        {
            me.CheckIn(idGenerator.NewId(), today);
            activity.Publish(me.Id, ActivityKind.TaskCompleted, task.Title, null, now, task.Id);
        }
        else
        {
            await activity.WithdrawAsync(me.Id, ActivityKind.TaskCompleted, task.Id, today, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        // The title is user content and may be personal — log the id only.
        logger.LogInformation("Task {TaskId} is now {TaskState}", task.Id, task.IsDoneOn(today) ? "done" : "open");

        return GoalTaskResponse.From(task, today);
    }

    /// <exception cref="DomainValidationException">The request violates a domain rule.</exception>
    /// <exception cref="ResourceNotFoundException">The goal it should belong to does not exist.</exception>
    public async Task<GoalTaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = Today();
        var me = await currentPerson.GetAsync(cancellationToken);
        var rhythm = request.Rhythm ?? GoalRhythm.Daily;

        if (request.GoalId is { } goalId)
        {
            var goal = await database.Goals
                .AsNoTracking()
                .Include(g => g.Participants)
                .SingleOrDefaultAsync(g => g.Id == goalId, cancellationToken);

            if (goal is null || !goal.IsVisibleTo(me.Id))
            {
                throw new ResourceNotFoundException("Goal", goalId);
            }
        }

        // New tasks go to the top of *this person's* list, which is where
        // somebody who has just typed one expects to find it. Scoped to the
        // owner, or one busy person would push everybody else's new tasks down.
        var lowestSortOrder = await database.GoalTasks
            .Where(t => t.OwnerPersonId == me.Id)
            .Select(t => (int?)t.SortOrder)
            .MinAsync(cancellationToken) ?? 0;

        var task = GoalTask.Create(
            idGenerator.NewId(),
            me.Id,
            request.GoalId,
            request.Title ?? string.Empty,
            rhythm,
            request.ReminderAt,

            // A weekly task with no day named means "starting today".
            rhythm == GoalRhythm.Weekly ? request.WeeklyOn ?? today.DayOfWeek : null,
            rhythm == GoalRhythm.Once ? request.DueOn ?? today : null,
            request.TargetValue is null ? null : 0,
            request.TargetValue,
            request.MeasureUnit,
            lowestSortOrder - 1,
            now);

        database.GoalTasks.Add(task);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created task {TaskId} with rhythm {Rhythm}", task.Id, task.Rhythm);

        return GoalTaskResponse.From(task, today);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
