using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// What a decided photograph means for the people who were not holding it.
/// </summary>
/// <remarks>
/// A verdict is reached in one of two ways — by the vote that settles it, or by
/// the maintenance pass once twelve hours have run out with nobody looking —
/// and its owner hears it the same way from either
/// (<see cref="ProofService.VoteAsync"/>, <see cref="GoalMaintenanceWorker"/>).
///
/// **A verdict never has an actor.** Naming whoever cast the deciding vote would
/// name a doubter every time it went the other way, and doubt is anonymous
/// ([0018](../../../../docs/adr/0018-proof-and-vote.md)).
///
/// A verdict reached at the moment of delivery — a goal with nobody to ask — is
/// not announced: the person who would be told is the one holding the phone.
/// </remarks>
public static class ProofVerdicts
{
    /// <summary>
    /// Advances a goal and stages any verdict its deadline reached. Reads and
    /// writes must announce it just as the background pass does; otherwise
    /// opening the goal first would silently consume that pass's notification.
    /// </summary>
    public static async Task<bool> AdvanceAsync(
        Notifier notifier,
        Goal goal,
        LocalCalendar calendar,
        DateTimeOffset now,
        IIdGenerator ids,
        Person? owner,
        CancellationToken cancellationToken)
    {
        var voting = goal.CurrentInstance?.PendingProof;
        var changed = GoalMaintenance.Advance(goal, calendar, now, ids, owner);

        if (voting is not null)
        {
            await AnnounceAsync(notifier, goal, voting, cancellationToken);
        }

        return changed;
    }

    /// <summary>
    /// Stages the owner's notification and clears the photograph from every
    /// voter's queue. Does nothing while the vote is still open.
    /// </summary>
    public static async Task AnnounceAsync(
        Notifier notifier,
        Goal goal,
        ProofPhoto proof,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(proof);

        if (proof.Status == ProofStatus.Voting)
        {
            return;
        }

        var verdict = proof.Status == ProofStatus.Confirmed
            ? new NotificationEvent(
                NotificationKind.ProofConfirmed,
                ActorPersonId: null,
                NotificationTarget.Goal,
                goal.Id,
                goal.Title)
            : new NotificationEvent(
                NotificationKind.ProofRefused,
                ActorPersonId: null,
                NotificationTarget.Goal,
                goal.Id,
                goal.Title,

                // Whether there is a second try is the one thing worth knowing
                // straight away: it decides whether there is anything to do.
                Amount: ProofVoting.HasAttemptLeft(proof.Attempt) ? 1 : 0);

        await notifier.StageAsync(verdict, [goal.OwnerPersonId], cancellationToken);

        // It has left every voter's queue, whoever settled it.
        notifier.Touch(goal.Participants.Select(participant => participant.PersonId), LiveArea.Proofs);
    }
}
