using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

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
    GoalService goals,
    ActivityService activity,
    ImageService images,
    FriendsService friends,
    BlockList blockList,
    TimeProvider timeProvider,
    TimeZoneResolver timeZones)
{
    /// <summary>How much history the profile screen shows.</summary>
    public const int RecentActivityLimit = 6;

    public async Task<ProfileResponse> GetAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = timeZones.For(me.TimeZoneId).Today(now);

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

        // Waiting for *me*, not waiting in general: the badge on the tab bar
        // counts what this person still has to answer.
        var pendingRequests = await database.Friendships
            .AsNoTracking()
            .CountAsync(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == me.Id, cancellationToken);

        return new ProfileResponse(
            PersonSummary.From(me, now),
            me.StreakOn(today),
            me.KudosReceived,
            me.GoalsCompleted,
            me.WeekActivity(today),
            await goals.SummariseTodayAsync(cancellationToken),
            conversations.Count(c => c.UnreadCountFor(me.Id) > 0),
            pendingRequests,
            badges,
            await activity.ListOwnAsync(RecentActivityLimit, cancellationToken),
            await BalanceOfAsync(me.Id, cancellationToken),
            await goals.ListAtRiskAsync(cancellationToken));
    }

    /// <summary>
    /// Somebody else's profile, counted over what the person asking shares
    /// with them.
    /// </summary>
    /// <remarks>
    /// **The scoping is the point of this method.** A balance is a record of
    /// somebody's failures, and publishing the whole of it to anybody who found
    /// their handle would be the single most damaging thing this product could
    /// do. So the goals it counts are the ones both people are on, and that is
    /// expressed in the query — not filtered out of a wider result afterwards,
    /// where a later refactor could drop the filter and nobody would notice
    /// until somebody's week was on a stranger's screen (section 7e of the
    /// migration plan).
    ///
    /// Looking at your own profile through this route is allowed and answers
    /// the same as <see cref="GetAsync"/> would for the balance: everything you
    /// share with yourself is everything you own.
    /// </remarks>
    public async Task<PersonProfileResponse> GetPersonAsync(Guid personId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var person = await database.People
            .AsNoTracking()
            .Include(candidate => candidate.CheckIns)
            .SingleOrDefaultAsync(candidate => candidate.Id == personId, cancellationToken)
            ?? throw new ResourceNotFoundException("Person", personId);

        /*
         * A blocked person has no profile, in either direction.
         *
         * The same 404 a person who never existed gets — not a 403 and not an
         * empty profile, either of which would say that somebody is there and
         * has done something about you.
         */
        if (await blockList.IsHiddenFromMeAsync(personId, cancellationToken))
        {
            throw new ResourceNotFoundException("Person", personId);
        }

        var shared = await SharedGoalsAsync(me.Id, personId, cancellationToken);

        var (done, missed) = shared.Aggregate(
            (Done: 0, Missed: 0),
            (total, goal) =>
            {
                var balance = goal.Balance;
                return (total.Done + balance.Done, total.Missed + balance.Missed);
            });

        return new PersonProfileResponse(
            PersonSummary.From(person, now),
            await friends.StateBetweenAsync(me.Id, personId, cancellationToken),
            person.StreakOn(timeZones.For(person.TimeZoneId).Today(now)),
            person.KudosReceived,
            person.GoalsCompleted,
            new BalanceResponse(done, missed),
            shared.Count);
    }

    /// <summary>Every window this person has ever had, over their own goals.</summary>
    private async Task<BalanceResponse> BalanceOfAsync(Guid personId, CancellationToken cancellationToken)
    {
        var windows = await database.GoalInstances
            .AsNoTracking()
            .Where(instance => database.Goals.Any(goal =>
                goal.Id == instance.GoalId && goal.OwnerPersonId == personId))
            .Select(instance => instance.Status)
            .ToListAsync(cancellationToken);

        return new BalanceResponse(
            windows.Count(status => status == GoalInstanceStatus.Done),
            windows.Count(status => status == GoalInstanceStatus.Missed));
    }

    /// <summary>
    /// The subject's own goals that the viewer was let in on.
    /// </summary>
    /// <remarks>
    /// **Owned by the subject, deliberately — not "shared either way".** A
    /// balance is a record of what *this person* delivered and missed, and only
    /// a goal they own has windows that are theirs. A goal they were merely
    /// invited to has windows belonging to whoever made that promise, and
    /// counting those here would put somebody else's failures on their profile.
    ///
    /// It is a mistake worth naming because the symmetric version reads as the
    /// fairer rule and passes the obvious test: looking at a friend's profile
    /// from a goal you own would show *your* record under *their* name, and
    /// only from the third possible angle — two people both invited to a third
    /// person's goal — does it become obviously wrong.
    ///
    /// Your own profile through this route is your own goals, which is the same
    /// statement with viewer and subject collapsed.
    /// </remarks>
    private async Task<List<Goal>> SharedGoalsAsync(
        Guid viewerId,
        Guid subjectId,
        CancellationToken cancellationToken) =>
        await database.Goals
            .AsNoTracking()
            .Include(goal => goal.Instances)
            .Where(goal => goal.OwnerPersonId == subjectId
                && (viewerId == subjectId || goal.Participants.Any(p => p.PersonId == viewerId)))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Changes the signed-in person's own name or picture, and answers with the
    /// whole profile again.
    /// </summary>
    /// <remarks>
    /// The full profile rather than the two fields that moved, because every
    /// caller of this is a screen that is already showing the rest of it — and
    /// a client that has to merge a partial response into a cached one is a
    /// client with two ideas of who you are.
    ///
    /// It never touches the handle. That is somebody's address on the friends
    /// screen, and letting a rename move it would break the one thing other
    /// people wrote down.
    /// </remarks>
    public async Task<ProfileResponse> UpdateAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);

        if (request.DisplayName is { } name && !string.IsNullOrWhiteSpace(name))
        {
            me.Rename(name, ProfileDefaults.Initials(name));
        }

        // Remembered before it moves, because a replaced avatar is rubbish the
        // moment the new one is in place: nothing else in q2 ever points at an
        // avatar image, and there is no screen that lists them, so one left
        // behind would sit in this person's storage allowance permanently with
        // no way for them to reach it.
        var replaced = me.AvatarImageId;

        if (request.AvatarImageId is { } imageId)
        {
            // Throws ResourceNotFoundException when the image is not this
            // person's, or was not uploaded as an avatar.
            await images.RequireOwnedAsync(imageId, me.Id, ImagePurpose.Avatar, cancellationToken);
            me.SetAvatarImage(imageId);
        }

        await database.SaveChangesAsync(cancellationToken);

        // After the save, and only when it actually changed. Should this fail,
        // the profile is still correct and the cost is one unreferenced file —
        // the other order would risk a profile pointing at deleted bytes.
        if (replaced is { } previous && previous != me.AvatarImageId)
        {
            await images.DeleteAsync(previous, cancellationToken);
        }

        return await GetAsync(cancellationToken);
    }
}
