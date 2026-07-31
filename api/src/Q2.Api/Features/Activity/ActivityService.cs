using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Activity;

/// <summary>
/// The friends' feed, the kudos on it, and the weekly leaderboard.
/// </summary>
/// <remarks>
/// Kudos and the leaderboard live together because they are the same number
/// seen twice: giving one moves a row. Splitting them would mean two services
/// writing to <see cref="Person.KudosReceived"/>.
/// </remarks>
public sealed class ActivityService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    FriendsService friends,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<ActivityService> logger)
{
    /// <summary>How many entries the feed shows before it stops being a feed.</summary>
    public const int FeedLimit = 30;

    /// <summary>How many people a leaderboard is worth ranking.</summary>
    public const int LeaderboardLimit = 10;

    public async Task<IReadOnlyList<ActivityResponse>> ListFeedAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        // Your friends, and only them. Your own doing is not news to you — that
        // is on the profile screen — and a stranger's is not yours to see at
        // all: the feed is the one screen that would otherwise show the whole
        // database to whoever signed up most recently.
        var friendIds = await friends.FriendIdsAsync(me.Id, cancellationToken);

        var events = await database.ActivityEvents
            .AsNoTracking()
            .Include(e => e.Kudos)
            .Where(e => friendIds.Contains(e.ActorPersonId))
            .OrderByDescending(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .Take(FeedLimit)
            .ToListAsync(cancellationToken);

        var actors = await LoadActorsAsync(events, cancellationToken);

        return
        [
            .. events
                .Where(e => actors.ContainsKey(e.ActorPersonId))
                .Select(e => ActivityResponse.From(e, actors[e.ActorPersonId], me.Id, now)),
        ];
    }

    /// <summary>Your own recent history, newest first.</summary>
    public async Task<IReadOnlyList<ActivityResponse>> ListOwnAsync(int limit, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var events = await database.ActivityEvents
            .AsNoTracking()
            .Include(e => e.Kudos)
            .Where(e => e.ActorPersonId == me.Id)
            .OrderByDescending(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. events.Select(e => ActivityResponse.From(e, me, me.Id, now))];
    }

    /// <summary>
    /// Gives kudos, or takes them back if they were already given.
    /// </summary>
    /// <remarks>
    /// One button, one meaning: the design has a single tappable count, and a
    /// separate "un-kudos" endpoint would be an affordance nothing in the app
    /// offers.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">
    /// No such activity, or it is not one this person can see. Deliberately the
    /// same answer for both: "that exists but is not yours" would confirm the
    /// existence of an activity belonging to somebody the caller does not know.
    /// </exception>
    public async Task<ActivityResponse> ToggleKudosAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var friendIds = await friends.FriendIdsAsync(me.Id, cancellationToken);

        var activity = await database.ActivityEvents
            .Include(e => e.Kudos)
            .SingleOrDefaultAsync(e => e.Id == id && friendIds.Contains(e.ActorPersonId), cancellationToken)
            ?? throw new ResourceNotFoundException("Activity", id);

        var now = timeProvider.GetUtcNow();

        var actor = await database.People
            .SingleOrDefaultAsync(p => p.Id == activity.ActorPersonId, cancellationToken)
            ?? throw new ResourceNotFoundException("Person", activity.ActorPersonId);

        if (activity.HasKudosFrom(me.Id))
        {
            activity.WithdrawKudos(me.Id);
            actor.WithdrawKudos();
        }
        else
        {
            activity.GiveKudos(idGenerator.NewId(), me.Id);
            actor.ReceiveKudos();
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Activity {ActivityId} now has {KudosCount} kudos",
            activity.Id,
            activity.KudosCount);

        return ActivityResponse.From(activity, actor, me.Id, now);
    }

    /// <summary>
    /// You and your friends, ranked by kudos.
    /// </summary>
    /// <remarks>
    /// Ranks are assigned here rather than in the client so that ties break the
    /// same way on every screen — by name, once the numbers are equal.
    /// </remarks>
    public async Task<IReadOnlyList<LeaderboardEntryResponse>> ListLeaderboardAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var friendIds = await friends.FriendIdsAsync(me.Id, cancellationToken);

        var people = await database.People
            .AsNoTracking()
            .Where(p => p.Id == me.Id || friendIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return
        [
            .. people
                .OrderByDescending(p => p.KudosReceived)
                .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(LeaderboardLimit)
                .Select((person, index) => new LeaderboardEntryResponse(
                    index + 1,
                    PersonSummary.From(person, now),
                    person.KudosReceived,
                    person.Id == me.Id)),
        ];
    }

    private async Task<IReadOnlyDictionary<Guid, Person>> LoadActorsAsync(
        IReadOnlyCollection<ActivityEvent> events,
        CancellationToken cancellationToken)
    {
        var ids = events.Select(e => e.ActorPersonId).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Person>();
        }

        return await database.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
