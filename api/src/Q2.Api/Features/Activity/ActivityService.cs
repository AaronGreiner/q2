using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Activity;

/// <summary>
/// The friends' feed and the kudos on it.
/// </summary>
/// <remarks>
/// There is deliberately no ranking here. q2 tells your friends when you miss a
/// window, and a table that sorts everybody by how well they are doing turns
/// that into a scoreboard somebody comes last on. The balance on a profile is
/// the counterpart, and it is scoped to the viewer for the same reason: a
/// failure concerns the people it was promised to, and nobody else.
/// </remarks>
public sealed class ActivityService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    FriendsService friends,
    Notifier notifier,
    BadgeAwarder badges,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<ActivityService> logger)
{
    /// <summary>How many entries the feed shows before it stops being a feed.</summary>
    public const int FeedLimit = 30;

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
        var goals = await VisibleGoalIdsAsync(events, me.Id, cancellationToken);

        return
        [
            .. events
                .Where(e => actors.ContainsKey(e.ActorPersonId))
                .Select(e => ActivityResponse.From(e, actors[e.ActorPersonId], me.Id, now, goals)),
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

        var goals = await VisibleGoalIdsAsync(events, me.Id, cancellationToken);

        return [.. events.Select(e => ActivityResponse.From(e, me, me.Id, now, goals))];
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
            await notifier.RetractAsync(NotificationKind.ReactionReceived, actor.Id, me.Id, activity.Id, focusId: null, cancellationToken);
        }
        else
        {
            activity.GiveKudos(idGenerator.NewId(), me.Id);
            actor.ReceiveKudos();

            // The reaction the product is named after, and the one that lands
            // on a line of somebody's history rather than on a photograph.
            await notifier.StageAsync(
                new NotificationEvent(
                    NotificationKind.ReactionReceived,
                    me.Id,
                    NotificationTarget.Activity,
                    activity.Id,
                    activity.Subject,

                    // Where the line itself leads, which is the goal it came
                    // from (#103) — not the whole profile it is a line of (#112).
                    FocusId: activity.SourceId),
                [actor.Id],
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        await notifier.FlushAsync(cancellationToken);

        // Kudos given count towards a badge of the giver's (#22).
        if (activity.HasKudosFrom(me.Id) && await badges.AwardEarnedAsync(me.Id, cancellationToken))
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Activity {ActivityId} now has {KudosCount} kudos",
            activity.Id,
            activity.KudosCount);

        var goals = await VisibleGoalIdsAsync([activity], me.Id, cancellationToken);

        return ActivityResponse.From(activity, actor, me.Id, now, goals);
    }

    /// <summary>
    /// Which of the goals these lines came from the viewer may open — the same
    /// rule as <see cref="Goal.IsVisibleTo"/>, asked of the database in one go.
    /// </summary>
    /// <remarks>
    /// A source is not a foreign key (see <see cref="ActivityEvent.SourceId"/>),
    /// so a deleted goal is simply not found and its line opens the person.
    /// </remarks>
    private async Task<IReadOnlySet<Guid>> VisibleGoalIdsAsync(
        IReadOnlyCollection<ActivityEvent> events,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var sources = events
            .Select(e => e.SourceId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (sources.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var visible = await database.Goals
            .AsNoTracking()
            .Where(goal => sources.Contains(goal.Id)
                && (goal.OwnerPersonId == viewerId || goal.Participants.Any(p => p.PersonId == viewerId)))
            .Select(goal => goal.Id)
            .ToListAsync(cancellationToken);

        return visible.ToHashSet();
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
