using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Reading, writing and discarding the Do somebody without friends has waiting.
/// </summary>
/// <remarks>
/// Starting it is not here: that happens when a friendship does, in
/// <see cref="GoalDraftStarter"/>, whichever of the two people asked for it.
/// </remarks>
public sealed class GoalDraftService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    FriendsService friends,
    Notifier notifier,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<GoalDraftService> logger)
{
    /// <summary>The caller's waiting Do, if there is one, and whether they have a friend.</summary>
    public async Task<GoalDraftStateResponse> GetAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var draft = await database.GoalDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OwnerPersonId == me.Id, cancellationToken);

        var hasFriends = await database.Friendships
            .AsNoTracking()
            .AnyAsync(
                friendship => friendship.Status == FriendshipStatus.Accepted
                    && (friendship.RequesterId == me.Id || friendship.AddresseeId == me.Id),
                cancellationToken);

        return new GoalDraftStateResponse(draft is null ? null : GoalDraftResponse.From(draft), hasFriends);
    }

    /// <summary>
    /// Writes the caller's waiting Do, replacing what it said before.
    /// </summary>
    /// <remarks>
    /// Refused once there is a friend. A waiting Do exists because there was
    /// nobody to make it in front of; with somebody there, the goal is made
    /// properly, with them choosing to check it — and a draft written then
    /// would wait for a friendship that already happened.
    /// </remarks>
    /// <exception cref="DomainValidationException">The request is not usable, or there is a friend already.</exception>
    public async Task<GoalDraftResponse> SaveAsync(SaveGoalDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);

        if ((await friends.FriendIdsAsync(me.Id, cancellationToken)).Count > 0)
        {
            throw new DomainValidationException(
                "Friends",
                "You have a friend to make this in front of; create the goal instead.");
        }

        var schedule = CreateGoalRequestValidator.ToSchedule(request.Schedule);

        var draft = await database.GoalDrafts
            .SingleOrDefaultAsync(candidate => candidate.OwnerPersonId == me.Id, cancellationToken);

        if (draft is null)
        {
            draft = GoalDraft.Create(
                idGenerator.NewId(),
                me.Id,
                request.Title ?? string.Empty,
                request.Category,
                schedule,
                request.ReminderAt,
                timeProvider.GetUtcNow());

            database.GoalDrafts.Add(draft);
        }
        else
        {
            draft.Describe(request.Title ?? string.Empty, request.Category, schedule, request.ReminderAt);
        }

        await database.SaveChangesAsync(cancellationToken);

        // The owner's other devices show the waiting card too.
        notifier.Touch([me.Id], LiveArea.Goals);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("Saved the waiting goal of {PersonId}", me.Id);

        return GoalDraftResponse.From(draft);
    }

    /// <summary>Throws the waiting Do away. Nothing to do when there is none.</summary>
    public async Task DiscardAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var removed = await database.GoalDrafts
            .Where(candidate => candidate.OwnerPersonId == me.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            notifier.Touch([me.Id], LiveArea.Goals);
            await notifier.FlushAsync(cancellationToken);

            logger.LogInformation("Discarded the waiting goal of {PersonId}", me.Id);
        }
    }
}
