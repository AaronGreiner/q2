using Microsoft.EntityFrameworkCore;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>What a seed run did.</summary>
public sealed record SeedResult(SeedProfile Profile, int GoalsInserted, bool WasSkipped, string Reason);

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
    /// database untouched. <c>true</c> deletes all goals first and is only
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
            return new SeedResult(profile, 0, true, "Seed profile is None.");
        }

        var source = sources.SingleOrDefault(s => s.Profile == profile)
            ?? throw new InvalidOperationException(
                $"No seed data source is registered for profile '{profile}'.");

        if (!replaceExisting && await database.Goals.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed profile {SeedProfile} skipped: the database already contains goals.", profile);
            return new SeedResult(profile, 0, true, "Database is not empty and replaceExisting was not requested.");
        }

        if (replaceExisting)
        {
            // Participants first: ExecuteDelete issues plain SQL and does not
            // run EF's cascade, only the database's.
            await database.GoalParticipants.ExecuteDeleteAsync(cancellationToken);
            await database.Goals.ExecuteDeleteAsync(cancellationToken);

            // ExecuteDelete goes straight to SQL and leaves the change tracker
            // holding rows that no longer exist. Since seeds use fixed ids,
            // re-inserting them would then collide with those stale entries.
            database.ChangeTracker.Clear();
        }

        var context = SeedContext.FromTimeProvider(timeProvider);
        var goals = source.CreateGoals(context);

        database.Goals.AddRange(goals);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded profile {SeedProfile} with {GoalCount} goal(s): {SeedDescription}",
            profile,
            goals.Count,
            source.Description);

        return new SeedResult(profile, goals.Count, false, source.Description);
    }
}
