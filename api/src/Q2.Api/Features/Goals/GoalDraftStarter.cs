using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Turns a waiting Do into a running goal the moment its owner has somebody to
/// make it in front of.
/// </summary>
/// <remarks>
/// Called from every place a friendship becomes accepted — a request answered,
/// two people asking each other, an invite link at registration or for an
/// existing account — before the caller saves, so the friendship and the goal
/// it made possible are committed together. Nobody has to come back and press
/// anything: the promise was made during the introduction, and the friend
/// arriving is what it was waiting for (docs/adr/0039-first-run-onboarding.md).
///
/// Whoever just became the friend is the one who checks it, and hears so
/// through the ordinary goal invitation. A draft only exists while its owner
/// has no friend (<see cref="GoalDraftService.SaveAsync"/>), so the friend that
/// starts it is in practice the first one.
/// </remarks>
public sealed class GoalDraftStarter(
    Q2DbContext database,
    GoalLauncher launcher,
    Notifier notifier,
    IIdGenerator idGenerator,
    TimeZoneResolver timeZones,
    ILogger<GoalDraftStarter> logger)
{
    /// <summary>
    /// Starts the waiting Do of either person, with the other on it. Does not
    /// save. Returns how many were started.
    /// </summary>
    public async Task<int> StartAsync(Guid one, Guid other, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var drafts = await database.GoalDrafts
            .Where(draft => draft.OwnerPersonId == one || draft.OwnerPersonId == other)
            .ToListAsync(cancellationToken);

        if (drafts.Count == 0)
        {
            return 0;
        }

        var owners = drafts.Select(draft => draft.OwnerPersonId).ToList();

        // The zone each owner's days are counted in, which is where the first
        // window's deadline comes from. Read without tracking: the caller may
        // already be holding one of these people.
        var zones = await database.People
            .AsNoTracking()
            .Where(person => owners.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, person => person.TimeZoneId, cancellationToken);

        foreach (var draft in drafts)
        {
            var friend = draft.OwnerPersonId == one ? other : one;

            var goal = draft.Start(idGenerator.NewId(), now);
            goal.AddParticipant(idGenerator.NewId(), friend);

            await launcher.LaunchAsync(goal, timeZones.For(zones.GetValueOrDefault(draft.OwnerPersonId)), now, cancellationToken);

            database.GoalDrafts.Remove(draft);

            // The owner's Dos tab and start screen, on every device, swap the
            // waiting card for the running goal.
            notifier.Touch([draft.OwnerPersonId], LiveArea.Goals);

            // Ids only: the title is what somebody promised, and personal.
            logger.LogInformation(
                "Started the waiting goal of {PersonId} as {GoalId}",
                draft.OwnerPersonId,
                goal.Id);
        }

        return drafts.Count;
    }
}
