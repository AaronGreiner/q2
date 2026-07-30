using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// Migrate, seed and reset — the operations behind the <c>db</c> CLI commands
/// and the startup policy.
/// </summary>
/// <remarks>
/// Every destructive path goes through <see cref="DatabaseResetGuard"/>, and
/// there is no way to bypass it: <see cref="ResetAsync"/> is the only method
/// that deletes anything.
/// </remarks>
public sealed class DatabaseMaintenance(
    Q2DbContext database,
    DatabaseSeeder seeder,
    IOptions<DatabaseOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseMaintenance> logger)
{
    private readonly DatabaseOptions _options = options.Value;

    /// <summary>Applies all pending EF Core migrations. Never destructive.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        DatabaseLocation.EnsureDirectoryExists(ResolveFilePath());

        var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date; no migrations to apply.");
            return;
        }

        logger.LogInformation("Applying {MigrationCount} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await database.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migrations applied.");
    }

    /// <summary>
    /// Inserts the configured seed profile. Without
    /// <paramref name="replaceExisting"/> an already populated database is left
    /// alone.
    /// </summary>
    public Task<SeedResult> SeedAsync(SeedProfile? profile, bool replaceExisting, CancellationToken cancellationToken)
    {
        var effective = profile ?? _options.SeedProfile;

        if (replaceExisting)
        {
            EnsureDestructiveOperationAllowed(effective);
        }

        return seeder.SeedAsync(database, effective, replaceExisting, cancellationToken);
    }

    /// <summary>
    /// DESTRUCTIVE. Deletes the database and recreates the schema from the
    /// migrations. Refuses unless <see cref="DatabaseResetGuard"/> agrees.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        var decision = EnsureDestructiveOperationAllowed(_options.SeedProfile);

        logger.LogWarning(
            "Resetting the {EnvironmentName} database. {Reason}",
            environment.EnvironmentName,
            decision.Reason);

        await database.Database.EnsureDeletedAsync(cancellationToken);
        DatabaseLocation.EnsureDirectoryExists(ResolveFilePath());
        await database.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database reset complete; schema rebuilt from migrations.");
    }

    /// <summary>DESTRUCTIVE. <see cref="ResetAsync"/> followed by a full seed.</summary>
    public async Task<SeedResult> ResetAndSeedAsync(CancellationToken cancellationToken)
    {
        await ResetAsync(cancellationToken);
        return await seeder.SeedAsync(database, _options.SeedProfile, replaceExisting: true, cancellationToken);
    }

    /// <summary>
    /// Runs whatever this environment's configuration permits at startup.
    /// Staging and Production do nothing: their schema is rolled out by a
    /// deliberate deployment step, not by an application start.
    /// </summary>
    public async Task ApplyStartupPolicyAsync(CancellationToken cancellationToken)
    {
        if (ApplicationEnvironments.Protected.Contains(environment.EnvironmentName))
        {
            logger.LogInformation(
                "Environment {EnvironmentName}: skipping automatic database work. Migrations are applied by the deployment process.",
                environment.EnvironmentName);
            return;
        }

        if (_options.ResetOnStartup)
        {
            await ResetAndSeedAsync(cancellationToken);
            return;
        }

        if (_options.MigrateOnStartup)
        {
            await MigrateAsync(cancellationToken);
        }

        if (_options.SeedOnStartup)
        {
            await seeder.SeedAsync(database, _options.SeedProfile, replaceExisting: false, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the guard context from live state — the actual provider, the
    /// actual resolved file — rather than from what configuration claims.
    /// </summary>
    public ResetContext BuildResetContext(SeedProfile profile) => new(
        environment.EnvironmentName,
        database.Database.ProviderName ?? "unknown",
        database.Database.GetConnectionString() ?? string.Empty,
        ResolveFilePath(),
        DatabaseLocation.ResolveDataDirectory(environment.ContentRootPath),
        profile,
        _options.AllowDestructiveReset);

    private ResetDecision EnsureDestructiveOperationAllowed(SeedProfile profile)
    {
        var decision = DatabaseResetGuard.Evaluate(BuildResetContext(profile));

        if (!decision.IsAllowed)
        {
            throw new DatabaseResetNotAllowedException(decision.Reason);
        }

        return decision;
    }

    private string? ResolveFilePath()
    {
        var connectionString = database.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString) || DatabaseLocation.IsInMemory(connectionString))
        {
            return null;
        }

        var (_, filePath) = DatabaseLocation.Resolve(
            connectionString,
            DatabaseLocation.ResolveDataDirectory(environment.ContentRootPath));

        return filePath;
    }
}
