using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Activity;

/// <summary>
/// Writes to the feed on behalf of the features that cause things to happen.
/// </summary>
/// <remarks>
/// Goals and tasks both publish activity, and both need to be able to take it
/// back. Putting that in one place keeps "ticking a task publishes exactly one
/// entry, and un-ticking removes exactly that one" a single rule rather than
/// two implementations that agree today.
///
/// Nothing here saves: the caller owns the transaction, so the entry and the
/// change that caused it are committed together or not at all.
/// </remarks>
public sealed class ActivityRecorder(Q2DbContext database, IIdGenerator idGenerator)
{
    /// <summary>Adds an entry to the feed.</summary>
    public void Publish(
        Guid actorPersonId,
        ActivityKind kind,
        string? subject,
        int? amount,
        DateTimeOffset occurredAt,
        Guid? sourceId = null)
    {
        var activity = ActivityEvent.Create(
            idGenerator.NewId(),
            actorPersonId,
            kind,
            subject,
            amount,
            kudosCount: 0,
            occurredAt,
            sourceId);

        database.ActivityEvents.Add(activity);
    }

    /// <summary>
    /// Adds a line for every streak milestone <paramref name="person"/>'s
    /// check-ins in this unit of work reached (<see cref="StreakMilestones"/>).
    /// </summary>
    /// <remarks>
    /// Called by whoever saved the check-in — a vote that settled a
    /// photograph, or a goal advanced with its owner in hand — because the
    /// check-in itself is written inside pure code that publishes nothing.
    /// </remarks>
    public void PublishStreakMilestones(Person person, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(person);

        foreach (var growth in person.TakeStreakGrowth())
        {
            if (StreakMilestones.ReachedBy(growth) is { } days)
            {
                Publish(person.Id, ActivityKind.StreakReached, null, days, occurredAt);
            }
        }
    }

    /// <summary>
    /// Removes the entry <paramref name="sourceId"/> published on
    /// <paramref name="day"/>, if it is still there.
    /// </summary>
    /// <remarks>
    /// Scoped to one day so that un-ticking today's run does not erase the same
    /// task's entry from last Tuesday.
    /// </remarks>
    public async Task WithdrawAsync(
        Guid actorPersonId,
        ActivityKind kind,
        Guid sourceId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var published = await database.ActivityEvents
            .Where(e => e.ActorPersonId == actorPersonId
                && e.Kind == kind
                && e.SourceId == sourceId
                && e.OccurredAt >= dayStart
                && e.OccurredAt < dayEnd)
            .ToListAsync(cancellationToken);

        database.ActivityEvents.RemoveRange(published);
    }
}
