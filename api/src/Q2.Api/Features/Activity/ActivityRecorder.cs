using Microsoft.EntityFrameworkCore;
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
