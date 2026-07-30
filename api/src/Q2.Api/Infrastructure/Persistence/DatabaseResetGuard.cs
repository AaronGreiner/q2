using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>Why a destructive database operation was allowed or refused.</summary>
public sealed record ResetDecision(bool IsAllowed, string Reason)
{
    public static ResetDecision Allow(string reason) => new(true, reason);

    public static ResetDecision Refuse(string reason) => new(false, reason);
}

/// <summary>Everything the guard needs to judge a reset request.</summary>
public sealed record ResetContext(
    string EnvironmentName,
    string ProviderName,
    string ConnectionString,
    string? DatabaseFilePath,
    string DataDirectory,
    SeedProfile SeedProfile,
    bool AllowDestructiveResetSetting);

/// <summary>
/// Refuses to delete a database unless several independent signals agree that
/// the target is a throwaway local test database.
/// </summary>
/// <remarks>
/// A single configuration boolean is not enough protection: it can be copied
/// into the wrong environment file, exported in the wrong shell, or inherited
/// by a deployment. So the guard checks five things that would all have to be
/// wrong at once for real data to be at risk:
/// <list type="number">
///   <item>the environment is ManualTesting, AutomatedTest or E2E;</item>
///   <item><c>Database:AllowDestructiveReset</c> is explicitly on, and does not
///   contradict the environment;</item>
///   <item>the provider is SQLite;</item>
///   <item>the target is in-memory, or a file under the repository data
///   directory or the OS temp directory whose name follows the
///   <c>q2-&lt;purpose&gt;.db</c> test naming convention;</item>
///   <item>the seed profile matches the environment.</item>
/// </list>
/// </remarks>
public static class DatabaseResetGuard
{
    public const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    private static readonly IReadOnlyDictionary<string, SeedProfile> ExpectedProfiles =
        new Dictionary<string, SeedProfile>(StringComparer.Ordinal)
        {
            [ApplicationEnvironments.ManualTesting] = SeedProfile.ManualTesting,
            [ApplicationEnvironments.AutomatedTest] = SeedProfile.AutomatedTest,
            [ApplicationEnvironments.E2E] = SeedProfile.E2E,
        };

    /// <summary>Markers a database file name must contain to be resettable.</summary>
    private static readonly string[] TestDatabaseMarkers =
        ["manual-testing", "automated-test", "e2e", "test"];

    public static ResetDecision Evaluate(ResetContext context)
    {
        // 1. Environment. Staging and Production are called out separately
        //    because that is the mistake with the worst consequences.
        if (ApplicationEnvironments.Protected.Contains(context.EnvironmentName))
        {
            return ResetDecision.Refuse(
                $"Environment '{context.EnvironmentName}' is protected. Destructive database operations are never permitted here.");
        }

        if (!ApplicationEnvironments.DestructiveResetAllowed.Contains(context.EnvironmentName))
        {
            return ResetDecision.Refuse(
                $"Environment '{context.EnvironmentName}' may not be reset. Allowed: "
                + $"{string.Join(", ", ApplicationEnvironments.DestructiveResetAllowed)}. "
                + "For a fresh local database use `bun run test:manual:start`.");
        }

        // 2. Explicit opt-in, and no contradiction between the two settings.
        if (!context.AllowDestructiveResetSetting)
        {
            return ResetDecision.Refuse(
                $"Database:AllowDestructiveReset is false for environment '{context.EnvironmentName}'. "
                + "The configuration contradicts the request; refusing rather than guessing.");
        }

        // 3. Provider.
        if (!string.Equals(context.ProviderName, SqliteProviderName, StringComparison.Ordinal))
        {
            return ResetDecision.Refuse(
                $"Only the SQLite provider may be reset, but the context uses '{context.ProviderName}'.");
        }

        // 4. Target database.
        var targetDecision = EvaluateTarget(context);
        if (!targetDecision.IsAllowed)
        {
            return targetDecision;
        }

        // 5. Seed profile has to belong to this environment.
        if (!ExpectedProfiles.TryGetValue(context.EnvironmentName, out var expectedProfile))
        {
            return ResetDecision.Refuse($"No seed profile is defined for environment '{context.EnvironmentName}'.");
        }

        if (context.SeedProfile != expectedProfile)
        {
            return ResetDecision.Refuse(
                $"Environment '{context.EnvironmentName}' expects seed profile '{expectedProfile}', "
                + $"but '{context.SeedProfile}' is configured. Refusing a reset with a mismatched profile.");
        }

        return ResetDecision.Allow(
            $"Environment '{context.EnvironmentName}', SQLite target '{Describe(context)}', seed profile '{context.SeedProfile}'.");
    }

    private static ResetDecision EvaluateTarget(ResetContext context)
    {
        if (context.DatabaseFilePath is null)
        {
            return DatabaseLocation.IsInMemory(context.ConnectionString)
                ? ResetDecision.Allow("In-memory database.")
                : ResetDecision.Refuse("The database target could not be resolved to a file or an in-memory database.");
        }

        var fileName = Path.GetFileName(context.DatabaseFilePath);

        if (!fileName.StartsWith("q2-", StringComparison.Ordinal)
            || !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return ResetDecision.Refuse(
                $"'{fileName}' does not follow the local test database naming convention 'q2-<purpose>.db'.");
        }

        if (!TestDatabaseMarkers.Any(marker => fileName.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return ResetDecision.Refuse(
                $"'{fileName}' is not recognisable as a test database. Its name must contain one of: "
                + string.Join(", ", TestDatabaseMarkers) + ".");
        }

        if (!IsInside(context.DatabaseFilePath, context.DataDirectory)
            && !IsInside(context.DatabaseFilePath, Path.GetTempPath()))
        {
            return ResetDecision.Refuse(
                $"'{context.DatabaseFilePath}' is outside the repository data directory and the temp directory. "
                + "Refusing to delete a database in an unexpected location.");
        }

        return ResetDecision.Allow($"Local test database '{fileName}'.");
    }

    private static bool IsInside(string path, string directory)
    {
        var normalisedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
            + Path.DirectorySeparatorChar;
        var normalisedPath = Path.GetFullPath(path);

        return normalisedPath.StartsWith(normalisedDirectory, StringComparison.Ordinal);
    }

    private static string Describe(ResetContext context) =>
        context.DatabaseFilePath is null ? ":memory:" : Path.GetFileName(context.DatabaseFilePath);
}

/// <summary>Thrown when <see cref="DatabaseResetGuard"/> refuses an operation.</summary>
public sealed class DatabaseResetNotAllowedException(string reason)
    : InvalidOperationException($"Refusing to reset the database. {reason}");
