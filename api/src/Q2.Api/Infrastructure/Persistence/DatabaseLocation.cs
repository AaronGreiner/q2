using Microsoft.Data.Sqlite;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// Turns the configured connection string into an absolute, unambiguous one.
/// </summary>
/// <remarks>
/// SQLite resolves a relative <c>Data Source</c> against the process working
/// directory, which differs between <c>dotnet run</c>, <c>dotnet test</c> and a
/// published app — the same configuration would then point at different files.
/// So relative paths are resolved here, once, against an explicit data
/// directory:
/// <list type="bullet">
///   <item><c>Q2_DATA_DIR</c> when set, otherwise</item>
///   <item><c>&lt;ContentRoot&gt;/../../.data</c>, which is <c>api/.data</c> for
///   the normal repository layout.</item>
/// </list>
/// Absolute paths and in-memory connection strings are passed through unchanged.
/// </remarks>
public static class DatabaseLocation
{
    public const string DataDirectoryVariable = "Q2_DATA_DIR";

    public static string ResolveDataDirectory(string contentRootPath)
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryVariable);

        return string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", ".data"))
            : Path.GetFullPath(configured);
    }

    /// <summary>
    /// Returns the connection string with an absolute <c>Data Source</c>, and
    /// the resolved file path (<c>null</c> for in-memory databases).
    /// </summary>
    public static (string ConnectionString, string? FilePath) Resolve(string connectionString, string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (IsInMemory(builder))
        {
            return (builder.ToString(), null);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "The database connection string has no 'Data Source'. Set ConnectionStrings__Database.");
        }

        var absolute = Path.IsPathRooted(builder.DataSource)
            ? Path.GetFullPath(builder.DataSource)
            : Path.GetFullPath(Path.Combine(dataDirectory, builder.DataSource));

        builder.DataSource = absolute;
        return (builder.ToString(), absolute);
    }

    public static bool IsInMemory(SqliteConnectionStringBuilder builder) =>
        builder.Mode == SqliteOpenMode.Memory
        || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase);

    public static bool IsInMemory(string connectionString) =>
        IsInMemory(new SqliteConnectionStringBuilder(connectionString));

    /// <summary>Creates the directory a file-based database will live in.</summary>
    public static void EnsureDirectoryExists(string? filePath)
    {
        if (filePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
