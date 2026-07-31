using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway SQLite database for tests that only need persistence, without
/// an HTTP host.
/// </summary>
/// <remarks>
/// Two flavours, matching the two situations the strategy distinguishes:
/// <list type="bullet">
///   <item><see cref="InMemory"/> — fast and fully isolated. The database lives
///   only while a connection is open, so one is held for the lifetime of the
///   instance.</item>
///   <item><see cref="TemporaryFile"/> — a real file under the OS temp
///   directory, used where "starts out genuinely empty, on disk" is the point,
///   as in the migration tests.</item>
/// </list>
/// The real EF Core SQLite provider is used throughout; the in-memory
/// <em>provider</em> (Microsoft.EntityFrameworkCore.InMemory) is deliberately
/// not a dependency of this repository — it is not a relational database and
/// would not catch the SQL these tests exist to check.
/// </remarks>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection? _keepAlive;
    private readonly string? _filePath;

    private SqliteTestDatabase(string connectionString, SqliteConnection? keepAlive, string? filePath)
    {
        ConnectionString = connectionString;
        _keepAlive = keepAlive;
        _filePath = filePath;
    }

    public string ConnectionString { get; }

    public static async Task<SqliteTestDatabase> InMemoryAsync()
    {
        var connectionString = $"Data Source=q2-automated-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync(TestContext.Current.CancellationToken);

        return new SqliteTestDatabase(connectionString, keepAlive, filePath: null);
    }

    public static SqliteTestDatabase TemporaryFile()
    {
        // The name follows the convention DatabaseResetGuard recognises.
        var path = Path.Combine(Path.GetTempPath(), $"q2-automated-test-{Guid.NewGuid():N}.db");

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return new SqliteTestDatabase($"Data Source={path}", keepAlive: null, filePath: path);
    }

    public Q2DbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Q2DbContext>()
            .UseSqlite(ConnectionString, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(Q2DbContext).Assembly.FullName);
                sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;

        return new Q2DbContext(options);
    }

    /// <summary>A seeder wired up the same way the application wires it.</summary>
    public static DatabaseSeeder CreateSeeder(TimeProvider? timeProvider = null) => new(
        [new DevelopmentSeed(), new ManualTestingSeed(), new AutomatedTestSeed(), new E2ESeed()],
        timeProvider ?? new FixedTimeProvider(Q2ApiFactory.Now),
        NullLogger<DatabaseSeeder>.Instance);

    public bool FileExists => _filePath is not null && File.Exists(_filePath);

    public async ValueTask DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();

        if (_filePath is not null && File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
