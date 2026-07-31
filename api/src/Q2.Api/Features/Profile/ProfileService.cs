using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Profile;

/// <summary>
/// Who the signed-in person is and how they are doing.
/// </summary>
/// <remarks>
/// One read rather than five. Every screen in the app needs some of this — the
/// home header needs the streak and the day's progress, the profile screen
/// needs all of it — and a phone on a train should not spend five round trips
/// drawing a header.
/// </remarks>
public sealed class ProfileService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    GoalTaskService tasks,
    ActivityService activity,
    TimeProvider timeProvider)
{
    /// <summary>How much history the profile screen shows.</summary>
    public const int RecentActivityLimit = 6;

    public async Task<ProfileResponse> GetAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var earned = await database.PersonBadges
            .AsNoTracking()
            .Where(b => b.PersonId == me.Id)
            .ToDictionaryAsync(b => b.Badge, cancellationToken);

        // The whole catalogue, in its defined order: an unearned badge is shown
        // greyed out, because a badge nobody can see is not something to aim
        // for.
        var badges = BadgeCatalogue.InDisplayOrder
            .Select(key => earned.TryGetValue(key, out var got)
                ? new BadgeResponse(key, true, got.EarnedOn)
                : new BadgeResponse(key, false, null))
            .ToList();

        var conversations = await database.Conversations
            .AsNoTracking()
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .Where(c => c.Participants.Any(p => p.PersonId == me.Id))
            .ToListAsync(cancellationToken);

        var pendingRequests = await database.Friendships
            .AsNoTracking()
            .CountAsync(f => f.Status == FriendshipStatus.Requested, cancellationToken);

        return new ProfileResponse(
            PersonSummary.From(me, now),
            me.StreakOn(today),
            me.KudosReceived,
            me.GoalsCompleted,
            me.WeekActivity(today),
            await tasks.SummariseTodayAsync(cancellationToken),
            conversations.Count(c => c.UnreadCountFor(me.Id) > 0),
            pendingRequests,
            badges,
            await activity.ListOwnAsync(RecentActivityLimit, cancellationToken));
    }
}
