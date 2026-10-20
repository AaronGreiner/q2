using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.People;

/// <summary>
/// Gives people the badges their record has earned (#22).
/// </summary>
/// <remarks>
/// Idempotent and catching up, like <see cref="GoalMaintenance"/>: it asks the
/// record what has been earned and awards whatever is missing, so it can run
/// as often as anybody likes, and a badge whose moment nothing noticed is
/// awarded the next time it runs. A badge stays once earned, even if the
/// kudos behind it are taken back: it says what happened.
///
/// Run after the change that might have earned one has been saved, because it
/// reads that change from the database — a believed photograph
/// (<c>ProofService</c>), kudos given (<c>ActivityService</c>), and the goals the
/// maintenance pass moved (<see cref="GoalMaintenanceWorker"/>), which is what
/// notices a week that has ended. Nothing here saves; it returns whether there
/// is anything to save.
///
/// Only what is still missing is asked about, and somebody who has every
/// badge costs one read.
/// </remarks>
public sealed class BadgeAwarder(
    Q2DbContext database,
    TimeZoneResolver timeZones,
    IIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    /// <summary>Awards what each of these people has earned. True when anybody got one.</summary>
    public async Task<bool> AwardEarnedAsync(IEnumerable<Guid> personIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(personIds);

        var awarded = false;

        foreach (var personId in personIds.Distinct())
        {
            awarded |= await AwardEarnedAsync(personId, cancellationToken);
        }

        return awarded;
    }

    /// <summary>Awards what this person has earned. True when they got one.</summary>
    public async Task<bool> AwardEarnedAsync(Guid personId, CancellationToken cancellationToken)
    {
        // Tracked, with both collections: AwardBadge has to see the badges
        // already there, or it would add a second and hit the unique index.
        var person = await database.People
            .Include(candidate => candidate.Badges)
            .Include(candidate => candidate.CheckIns)
            .SingleOrDefaultAsync(candidate => candidate.Id == personId, cancellationToken);

        if (person is null)
        {
            return false;
        }

        var missing = BadgeCatalogue.InDisplayOrder
            .Except(person.Badges.Select(badge => badge.Badge))
            .ToHashSet();

        if (missing.Count == 0)
        {
            return false;
        }

        var calendar = timeZones.For(person.TimeZoneId);
        var today = calendar.Today(timeProvider.GetUtcNow());
        var earned = new List<BadgeKey>();

        if (missing.Contains(BadgeKey.StreakHero) && BadgeRules.IsStreakHero(person.CheckIns.Select(day => day.Date)))
        {
            earned.Add(BadgeKey.StreakHero);
        }

        if (missing.Contains(BadgeKey.EarlyBird))
        {
            var believedAt = await database.ProofPhotos
                .AsNoTracking()
                .Where(proof => proof.UploaderPersonId == personId && proof.Status == ProofStatus.Confirmed)
                .Select(proof => proof.CreatedAt)
                .ToListAsync(cancellationToken);

            if (BadgeRules.IsEarlyBird(believedAt, calendar))
            {
                earned.Add(BadgeKey.EarlyBird);
            }
        }

        if (missing.Contains(BadgeKey.Bookworm) || missing.Contains(BadgeKey.Marathon))
        {
            var keptPerGoal = (await database.Goals
                    .AsNoTracking()
                    .Where(goal => goal.OwnerPersonId == personId
                        && (goal.Category == GoalCategory.Reading || goal.Category == GoalCategory.Sport))
                    .Select(goal => new
                    {
                        goal.Category,
                        Kept = database.GoalInstances.Count(instance =>
                            instance.GoalId == goal.Id && instance.Status == GoalInstanceStatus.Done),
                    })
                    .ToListAsync(cancellationToken))
                .Select(goal => (goal.Category, goal.Kept))
                .ToList();

            if (missing.Contains(BadgeKey.Bookworm)
                && BadgeRules.HasKeptOnOne(keptPerGoal, GoalCategory.Reading, BadgeRules.BookwormWindows))
            {
                earned.Add(BadgeKey.Bookworm);
            }

            if (missing.Contains(BadgeKey.Marathon)
                && BadgeRules.HasKeptOnOne(keptPerGoal, GoalCategory.Sport, BadgeRules.MarathonWindows))
            {
                earned.Add(BadgeKey.Marathon);
            }
        }

        if (missing.Contains(BadgeKey.KudosGiver)
            && BadgeRules.IsKudosGiver(await database.ActivityKudos.CountAsync(kudos => kudos.PersonId == personId, cancellationToken)))
        {
            earned.Add(BadgeKey.KudosGiver);
        }

        if (missing.Contains(BadgeKey.WeeklyWinner))
        {
            var windows = (await database.GoalInstances
                    .AsNoTracking()
                    .Where(instance => database.Goals.Any(goal => goal.Id == instance.GoalId && goal.OwnerPersonId == personId))
                    .Select(instance => new { instance.DueOn, instance.Status })
                    .ToListAsync(cancellationToken))
                .Select(window => (window.DueOn, window.Status))
                .ToList();

            if (BadgeRules.IsWeeklyWinner(windows, today))
            {
                earned.Add(BadgeKey.WeeklyWinner);
            }
        }

        foreach (var badge in earned)
        {
            person.AwardBadge(idGenerator.NewId(), badge, today);
        }

        return earned.Count > 0;
    }
}
