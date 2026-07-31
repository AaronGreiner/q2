using Microsoft.EntityFrameworkCore;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>What a seed run did.</summary>
public sealed record SeedResult(
    SeedProfile Profile,
    int PeopleInserted,
    int GoalsInserted,
    int TasksInserted,
    int ConversationsInserted,
    bool WasSkipped,
    string Reason);

/// <summary>
/// The single place that writes seed data.
/// </summary>
/// <remarks>
/// Seed rows are never created ad hoc in <c>Program.cs</c>, in a migration or
/// inside a test: they come from an <see cref="ISeedDataSource"/> chosen by
/// profile. That is what makes "which data is in this database?" answerable by
/// reading one class.
/// </remarks>
public sealed class DatabaseSeeder(
    IEnumerable<ISeedDataSource> sources,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger)
{
    /// <summary>
    /// Inserts <paramref name="profile"/> into <paramref name="database"/>.
    /// </summary>
    /// <param name="replaceExisting">
    /// <c>false</c> (the default path for Development) leaves a non-empty
    /// database untouched. <c>true</c> clears everything first and is only
    /// reached from flows that already passed <see cref="DatabaseResetGuard"/>.
    /// </param>
    public async Task<SeedResult> SeedAsync(
        Q2DbContext database,
        SeedProfile profile,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        if (profile == SeedProfile.None)
        {
            return Skipped(profile, "Seed profile is None.");
        }

        var source = sources.SingleOrDefault(s => s.Profile == profile)
            ?? throw new InvalidOperationException(
                $"No seed data source is registered for profile '{profile}'.");

        // People, not goals: a database with people in it has been seeded, even
        // if somebody has since deleted every goal from it.
        if (!replaceExisting && await database.People.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed profile {SeedProfile} skipped: the database already contains people.", profile);
            return Skipped(profile, "Database is not empty and replaceExisting was not requested.");
        }

        if (replaceExisting)
        {
            await ClearAsync(database, cancellationToken);
        }

        var data = source.Create(SeedContext.FromTimeProvider(timeProvider));

        // People first and settings last, in dependency order. EF Core would
        // sort most of this out on its own, but the order is also the answer to
        // "what does this world consist of?" and is worth being able to read.
        database.People.AddRange(data.People);
        database.Friendships.AddRange(data.Friendships);
        database.Goals.AddRange(data.Goals);
        database.GoalTasks.AddRange(data.Tasks);
        database.ActivityEvents.AddRange(data.Activity);
        database.Conversations.AddRange(data.Conversations);
        database.UserSettings.AddRange(data.Settings);

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded profile {SeedProfile}: {PersonCount} people, {GoalCount} goals, {TaskCount} tasks, "
            + "{ConversationCount} conversations — {SeedDescription}",
            profile,
            data.People.Count,
            data.Goals.Count,
            data.Tasks.Count,
            data.Conversations.Count,
            source.Description);

        return new SeedResult(
            profile,
            data.People.Count,
            data.Goals.Count,
            data.Tasks.Count,
            data.Conversations.Count,
            WasSkipped: false,
            source.Description);
    }

    /// <summary>
    /// Empties every table, children before parents.
    /// </summary>
    /// <remarks>
    /// <c>ExecuteDelete</c> issues plain SQL and does not run EF's cascade, only
    /// the database's — so the order matters and cannot be left to the change
    /// tracker. It also leaves the tracker holding rows that no longer exist;
    /// since seeds use fixed ids, re-inserting them would then collide with
    /// those stale entries, which is what the final <c>Clear</c> prevents.
    /// </remarks>
    private static async Task ClearAsync(Q2DbContext database, CancellationToken cancellationToken)
    {
        await database.MessageReactions.ExecuteDeleteAsync(cancellationToken);
        await database.ChatMessages.ExecuteDeleteAsync(cancellationToken);
        await database.ConversationParticipants.ExecuteDeleteAsync(cancellationToken);
        await database.Conversations.ExecuteDeleteAsync(cancellationToken);
        await database.ActivityKudos.ExecuteDeleteAsync(cancellationToken);
        await database.ActivityEvents.ExecuteDeleteAsync(cancellationToken);
        await database.GoalTasks.ExecuteDeleteAsync(cancellationToken);
        await database.GoalContributions.ExecuteDeleteAsync(cancellationToken);
        await database.GoalParticipants.ExecuteDeleteAsync(cancellationToken);
        await database.Goals.ExecuteDeleteAsync(cancellationToken);
        await database.UserSettings.ExecuteDeleteAsync(cancellationToken);
        await database.Friendships.ExecuteDeleteAsync(cancellationToken);
        await database.PersonBadges.ExecuteDeleteAsync(cancellationToken);
        await database.DailyCheckIns.ExecuteDeleteAsync(cancellationToken);
        await database.People.ExecuteDeleteAsync(cancellationToken);

        database.ChangeTracker.Clear();
    }

    private static SeedResult Skipped(SeedProfile profile, string reason) =>
        new(profile, 0, 0, 0, 0, WasSkipped: true, reason);
}
