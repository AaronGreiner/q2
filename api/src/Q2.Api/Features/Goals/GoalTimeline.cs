using Q2.Api.Features.Proofs;

namespace Q2.Api.Features.Goals;

/// <summary>Something that happened to a goal, as its conversation shows it.</summary>
public enum GoalEventKind
{
    /// <summary>The owner made the promise, in front of these people.</summary>
    Created,

    /// <summary>A photograph arrived. The conversation draws it with its vote.</summary>
    ProofDelivered,

    /// <summary>A window was kept. Carries the streak it brought the goal to.</summary>
    WindowDone,

    /// <summary>A window ran out, or its last photograph was not believed.</summary>
    WindowMissed,

    /// <summary>The owner set the goal aside. Carries the last day it covers — never the reason.</summary>
    PauseStarted,

    /// <summary>A pause ran its course, or its owner ended it early.</summary>
    PauseEnded,

    /// <summary>Enough friends objected to a pause for it to be lifted.</summary>
    PauseOverturned,

    /// <summary>The goal was carried through: a one-off delivered, or its owner saying so.</summary>
    Completed,

    /// <summary>The owner stopped the goal without claiming it was carried through.</summary>
    Stopped,
}

/// <summary>
/// One entry in a goal's timeline.
/// </summary>
/// <param name="SourceId">The row it was read from: the goal, a window, a photograph or a pause.</param>
/// <param name="ActorPersonId">
/// Who did it, when somebody did. Null for what time or a vote decided — and a
/// vote never has an actor, because naming whoever tipped it would name a
/// doubter every time it went the other way (docs/adr/0018-proof-and-vote.md).
/// </param>
/// <param name="Streak">For a kept window: the goal's streak once it was kept.</param>
/// <param name="Until">For a pause: the last local day it covers.</param>
public sealed record GoalEvent(
    GoalEventKind Kind,
    DateTimeOffset At,
    Guid SourceId,
    Guid? ActorPersonId,
    int? Streak = null,
    int? ConfirmedProofs = null,
    int? RequiredProofs = null,
    DateOnly? Until = null,
    ProofPhoto? Proof = null)
{
    /// <summary>
    /// Unique within one goal. A pause starts and ends, and a goal is created and
    /// closed, so the source alone is not.
    /// </summary>
    public string Key => $"{Kind}-{SourceId:N}";
}

/// <summary>
/// What a goal's conversation shows between the messages, read off the goal.
/// </summary>
/// <remarks>
/// **Derived, never written.** Nothing here is stored as a chat message: the
/// windows, the photographs and the pauses already say what happened and when,
/// so a line in the thread cannot disagree with the goal it is about, a goal
/// that existed before it had a conversation shows its whole history there,
/// and <see cref="GoalMaintenance"/> stays a pure function over the goal rather
/// than something that also has to remember to write into a chat
/// ([0027](../../../../docs/adr/0027-goal-conversations.md)).
///
/// The same list for everybody on the goal. What differs per reader — whether
/// they may vote, what they voted — is decided where a photograph is described,
/// not here.
/// </remarks>
public static class GoalTimeline
{
    public static IReadOnlyList<GoalEvent> For(Goal goal, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var events = new List<GoalEvent>
        {
            new(GoalEventKind.Created, goal.CreatedAt, goal.Id, goal.OwnerPersonId),
        };

        // Counted forwards in deadline order, with the same rule as Goal.Streak
        // counts backwards: a kept window adds one, a missed one ends the run,
        // and a window that is open or was set aside is neither.
        var streak = 0;

        foreach (var instance in goal.Instances.OrderBy(instance => instance.DueAt).ThenBy(instance => instance.Id))
        {
            foreach (var proof in instance.Proofs)
            {
                events.Add(new GoalEvent(
                    GoalEventKind.ProofDelivered,
                    proof.CreatedAt,
                    proof.Id,
                    proof.UploaderPersonId,
                    Proof: proof));
            }

            switch (instance.Status)
            {
                case GoalInstanceStatus.Done:
                    streak++;
                    events.Add(new GoalEvent(
                        GoalEventKind.WindowDone,
                        instance.ResolvedAt ?? instance.DueAt,
                        instance.Id,
                        null,
                        Streak: streak,
                        ConfirmedProofs: instance.ConfirmedProofs,
                        RequiredProofs: instance.RequiredProofs));
                    break;

                case GoalInstanceStatus.Missed:
                    streak = 0;
                    events.Add(new GoalEvent(
                        GoalEventKind.WindowMissed,
                        instance.ResolvedAt ?? instance.DueAt,
                        instance.Id,
                        null,
                        ConfirmedProofs: instance.ConfirmedProofs,
                        RequiredProofs: instance.RequiredProofs));
                    break;

                default:
                    break;
            }
        }

        foreach (var pause in goal.Pauses)
        {
            // The last day, and never the reason: the reason and the objection
            // belong on the goal's own screen, and a chat is not where somebody
            // should be asked to judge a friend's illness
            // (docs/adr/0020-pause-and-archive.md).
            events.Add(new GoalEvent(GoalEventKind.PauseStarted, pause.CreatedAt, pause.Id, pause.PersonId, Until: pause.EndsOn));

            if (pause.Status == PauseStatus.Overturned)
            {
                // Nobody's name: objections are anonymous, and so is their result.
                events.Add(new GoalEvent(GoalEventKind.PauseOverturned, pause.EndsAt, pause.Id, null));
            }
            else if (pause.Status == PauseStatus.Ended || now > pause.EndsAt)
            {
                // Over, even before the maintenance pass has written it down.
                events.Add(new GoalEvent(GoalEventKind.PauseEnded, pause.EndsAt, pause.Id, null));
            }
        }

        if (goal.ClosedAt is { } closedAt)
        {
            events.Add(goal.Status == GoalStatus.Completed

                // No actor: a one-off completes itself when its window is kept,
                // and the line should read the same whichever way it got there.
                ? new GoalEvent(GoalEventKind.Completed, closedAt, goal.Id, null)
                : new GoalEvent(GoalEventKind.Stopped, closedAt, goal.Id, goal.OwnerPersonId));
        }

        // The kind breaks a tie at the same instant, in the order it happened:
        // a photograph confirmed on delivery keeps its window in the same
        // moment, and a one-off kept is completed in it too.
        return
        [
            .. events
                .OrderBy(entry => entry.At)
                .ThenBy(entry => entry.Kind)
                .ThenBy(entry => entry.SourceId),
        ];
    }
}
