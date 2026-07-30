using System.ComponentModel.DataAnnotations;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// What the application is allowed to do to its database at startup, bound
/// from the <c>Database</c> configuration section.
/// </summary>
/// <remarks>
/// Every flag defaults to the safe value, so an environment that forgets to
/// configure anything gets a read-only startup rather than a wiped database.
/// <see cref="AllowDestructiveReset"/> alone never authorises anything — it is
/// one of several conditions checked by <see cref="DatabaseResetGuard"/>.
/// </remarks>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations during startup.</summary>
    public bool MigrateOnStartup { get; init; }

    /// <summary>
    /// Insert <see cref="SeedProfile"/> during startup if the database is empty.
    /// Never overwrites existing rows — that is what a reset is for.
    /// </summary>
    public bool SeedOnStartup { get; init; }

    /// <summary>
    /// Delete and recreate the database during startup. DESTRUCTIVE.
    /// Only ManualTesting sets this; it is still subject to the reset guard.
    /// </summary>
    public bool ResetOnStartup { get; init; }

    /// <summary>Which data set <see cref="SeedOnStartup"/> and the CLI insert.</summary>
    [EnumDataType(typeof(SeedProfile))]
    public SeedProfile SeedProfile { get; init; } = SeedProfile.None;

    /// <summary>
    /// Opt-in required before any destructive operation. Necessary, never
    /// sufficient: the environment, provider, file path and seed profile all
    /// have to agree as well.
    /// </summary>
    public bool AllowDestructiveReset { get; init; }
}
