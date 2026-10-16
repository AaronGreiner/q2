using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// The moment a goal starts running: its first window, its conversation, its
/// line in the feed, and the people asked to check it hearing so.
/// </summary>
/// <remarks>
/// Two doors lead here — somebody creating a goal in front of friends
/// (<see cref="GoalService.CreateAsync"/>), and a waiting Do starting because
/// its owner's first friend arrived (<see cref="GoalDraftStarter"/>). One class
/// rather than two copies, so a goal made either way cannot differ in what came
/// with it.
///
/// Nothing here saves. The caller owns the transaction — registration through
/// an invite link, for one, saves inside its own — and flushes the notifier
/// once it has committed.
/// </remarks>
public sealed class GoalLauncher(
    Q2DbContext database,
    ActivityRecorder activity,
    Notifier notifier,
    IIdGenerator idGenerator,
    Q2Metrics metrics)
{
    /// <summary>
    /// Adds <paramref name="goal"/>, already carrying its participants, with
    /// everything that comes with it.
    /// </summary>
    public async Task LaunchAsync(
        Goal goal,
        LocalCalendar ownerCalendar,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // The first window opens now, through the same code that opens every
        // later one — so "created today" and "rolled over overnight" cannot
        // disagree about where a window starts.
        GoalMaintenance.Advance(goal, ownerCalendar, now, idGenerator);

        database.Goals.Add(goal);

        // Its conversation opens with it, in the same save: there is no moment
        // at which the goal exists and the place its friends check it does not
        // (docs/adr/0027-goal-conversations.md).
        var conversation = GoalConversations.Open(goal, idGenerator.NewId(), idGenerator.NewId, now);
        database.Conversations.Add(conversation);

        activity.Publish(goal.OwnerPersonId, ActivityKind.GoalCreated, goal.Title, null, now, goal.Id);

        // Being put on a goal is being made one of the people who decide it,
        // which is worth knowing before the first photograph arrives.
        await notifier.StageAsync(
            new NotificationEvent(NotificationKind.GoalInvitation, goal.OwnerPersonId, NotificationTarget.Goal, goal.Id, goal.Title),
            goal.Participants.Select(participant => participant.PersonId),
            cancellationToken);

        // A new row in everybody's chat list, the owner's other devices too.
        notifier.Touch(conversation.Participants.Select(participant => participant.PersonId), LiveArea.Chats);

        // How often, and of which kind. Never which goal, and never whose.
        metrics.CountGoalCreated(goal.Schedule.Kind, goal.IsGroup);
    }
}
