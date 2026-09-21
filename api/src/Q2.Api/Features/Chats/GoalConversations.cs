using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Chats;

/// <summary>
/// The conversation every shared goal has: opened with the goal, and made up of
/// exactly the people on it.
/// </summary>
/// <remarks>
/// One place that knows who belongs in a goal's conversation, used by the
/// three things that open one — creating a goal, the maintenance pass that
/// catches up older goals, and the seeds — so the rule cannot drift between
/// them ([0027](../../../../docs/adr/0027-goal-conversations.md)).
/// </remarks>
public static class GoalConversations
{
    /// <summary>
    /// Opens the conversation of <paramref name="goal"/>: its owner, who has
    /// read everything so far, and everybody invited to check it.
    /// </summary>
    /// <param name="newParticipantId">
    /// Hands out the membership rows' ids. A function rather than a generator,
    /// so a seed can number them from its own counter.
    /// </param>
    public static Conversation Open(Goal goal, Guid id, Func<Guid> newParticipantId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(newParticipantId);

        var conversation = Conversation.CreateForGoal(id, goal.Id, now);
        conversation.AddParticipant(newParticipantId(), goal.OwnerPersonId, now);

        foreach (var participant in goal.Participants)
        {
            conversation.AddParticipant(newParticipantId(), participant.PersonId);
        }

        return conversation;
    }

    /// <summary>
    /// Opens the missing conversation of every goal that somebody else checks
    /// and that does not have one yet. Returns how many were opened, without
    /// saving.
    /// </summary>
    /// <remarks>
    /// Goals from before a goal came with its conversation. Idempotent, because
    /// it asks the database what is missing rather than remembering what it
    /// did — which is also why it can simply run on every maintenance pass.
    ///
    /// A goal nobody else is on gets none: it was created before a friend was
    /// required, and a conversation with only its owner in it would be a
    /// thread nobody can answer.
    /// </remarks>
    public static async Task<int> OpenMissingAsync(
        Q2DbContext database,
        IIdGenerator ids,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(ids);

        var goals = await database.Goals
            .AsNoTracking()
            .Include(goal => goal.Participants)
            .Where(goal => goal.Participants.Any()
                && !database.Conversations.Any(conversation => conversation.GoalId == goal.Id))
            .ToListAsync(cancellationToken);

        foreach (var goal in goals)
        {
            database.Conversations.Add(Open(goal, ids.NewId(), ids.NewId, now));
        }

        return goals.Count;
    }
}
