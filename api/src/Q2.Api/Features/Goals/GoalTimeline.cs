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

    /// <summary>
    /// The owner logged a use of a Don't. Carries how many this period, and the
    /// limit. A neutral line: no vote, no notification.
    /// </summary>
    UseLogged,

    /// <summary>A Don't's period ended in a relapse the owner logged themselves.</summary>
    WindowRelapse,

    /// <summary>
    /// The owner edited the goal. Carries the edit: what applied at once, and
    /// what waits for the next window (docs/adr/0044-editing-a-running-goal.md).
    /// </summary>
    Edited,

    /// <summary>What an edit left waiting is in force now. Carries the edit and the day it counts from.</summary>
    ChangeApplied,

    /// <summary>Enough checkers objected to an edit for it to be lifted; the goal stayed as it was.</summary>
    ChangeOverturned,

    /// <summary>The owner took back what an edit had left waiting.</summary>
    ChangeWithdrawn,
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
/// <param name="Uses">For a logged use: which one of the period it was.</param>
/// <param name="Limit">For a Don't's window or use: the limit of its period.</param>
/// <param name="Use">For a logged use: the use itself, with its photograph or clip if it had one.</param>
/// <param name="Change">For an edit and what became of it: the edit itself.</param>
public sealed record GoalEvent(
    GoalEventKind Kind,
    DateTimeOffset At,
    Guid SourceId,
    Guid? ActorPersonId,
    int? Streak = null,
    int? ConfirmedProofs = null,
    int? RequiredProofs = null,
    DateOnly? Until = null,
    ProofPhoto? Proof = null,
    DateOnly? Day = null,
    int? Uses = null,
    int? Limit = null,
    DontUse? Use = null,
    GoalChange? Change = null)
{
    /// <summary>
    /// Unique within one goal. A pause starts and ends, a goal is created and
    /// closed, and an edit is made and later applies, so the source alone is not.
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

        // Counted forwards in deadline order, with the same rules Goal.Streak
        // counts backwards by: a kept window adds one, a missed one ends the
        // run, a relapse holds it until the third in a row, and a window that
        // is open or was set aside is neither.
        var resolved = new List<GoalInstance>();

        foreach (var instance in goal.Instances.OrderBy(instance => instance.DueAt).ThenBy(instance => instance.Id))
        {
            var count = 0;

            foreach (var use in instance.UseLog.OrderBy(use => use.LoggedAt))
            {
                count++;
                events.Add(new GoalEvent(
                    GoalEventKind.UseLogged,
                    use.LoggedAt,
                    use.Id,
                    goal.OwnerPersonId,
                    Uses: count,
                    Limit: instance.Limit,
                    Use: use));
            }

            if (instance.Status is GoalInstanceStatus.Done or GoalInstanceStatus.Missed or GoalInstanceStatus.Relapse)
            {
                resolved.Add(instance);
            }

            var streak = Goal.StreakOf(resolved);

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
                    events.Add(new GoalEvent(
                        GoalEventKind.WindowDone,
                        instance.ResolvedAt ?? instance.DueAt,
                        instance.Id,
                        null,
                        Streak: streak,
                        ConfirmedProofs: instance.ConfirmedProofs,
                        RequiredProofs: instance.RequiredProofs,
                        Day: instance.DueOn,
                        Limit: instance.Limit));
                    break;

                case GoalInstanceStatus.Relapse:
                    events.Add(new GoalEvent(
                        GoalEventKind.WindowRelapse,
                        instance.ResolvedAt ?? instance.DueAt,
                        instance.Id,
                        null,
                        Streak: streak,
                        Day: instance.DueOn,
                        Uses: instance.Uses,
                        Limit: instance.Limit));
                    break;

                case GoalInstanceStatus.Missed:
                    events.Add(new GoalEvent(
                        GoalEventKind.WindowMissed,
                        instance.ResolvedAt ?? instance.DueAt,
                        instance.Id,
                        null,
                        ConfirmedProofs: instance.ConfirmedProofs,
                        RequiredProofs: instance.RequiredProofs,
                        Day: instance.DueOn,
                        Limit: instance.Limit));
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

        foreach (var change in goal.Changes)
        {
            // What changed, from what to what, is the owner's doing; what
            // became of the waiting half is time's or the checkers', and
            // names nobody — objections are anonymous, as for a pause.
            events.Add(new GoalEvent(GoalEventKind.Edited, change.CreatedAt, change.Id, change.PersonId, Change: change));

            var resolvedAt = change.ResolvedAt ?? change.CreatedAt;

            switch (change.Status)
            {
                case GoalChangeStatus.Applied:
                    events.Add(new GoalEvent(GoalEventKind.ChangeApplied, resolvedAt, change.Id, null, Day: change.AppliesOn, Change: change));
                    break;

                case GoalChangeStatus.Overturned:
                    events.Add(new GoalEvent(GoalEventKind.ChangeOverturned, resolvedAt, change.Id, null, Change: change));
                    break;

                case GoalChangeStatus.Withdrawn:
                    events.Add(new GoalEvent(GoalEventKind.ChangeWithdrawn, resolvedAt, change.Id, change.PersonId, Change: change));
                    break;

                // Replaced by the next edit, whose own line says what it is
                // now, or dropped with a goal that stopped, which says so too.
                default:
                    break;
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
