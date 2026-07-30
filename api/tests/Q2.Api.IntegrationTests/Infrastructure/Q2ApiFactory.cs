using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Observability.Testing;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.Infrastructure.Time;
using Sentry;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real application against a private SQLite in-memory database.
/// </summary>
/// <remarks>
/// Everything that matters is the production code path: the real middleware
/// pipeline, the real DI graph, the real EF Core provider, the real Sentry SDK.
/// Only three things are substituted, and each for a stated reason:
/// <list type="bullet">
///   <item>the database is a per-instance in-memory one, so tests are isolated
///   and can run in parallel;</item>
///   <item><see cref="TimeProvider"/> is fixed, so seeds and "is this overdue?"
///   agree on one instant;</item>
///   <item><see cref="IIdGenerator"/> is sequential, so a created goal has a
///   predictable id.</item>
/// </list>
/// The Sentry transport is <em>not</em> substituted here: the AutomatedTest
/// environment configures the recording transport through normal configuration,
/// so tests exercise the same wiring a developer would.
///
/// A SQLite in-memory database exists only while at least one connection to it
/// is open, which is why <see cref="_keepAlive"/> is held open for the lifetime
/// of the fixture.
/// </remarks>
public class Q2ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The instant every test runs at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    public static DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    /// <summary>
    /// How long a Sentry flush may take. Generous on purpose: the recording
    /// transport is in-process, so a flush takes microseconds and this is only
    /// ever reached when something is genuinely wrong — where a failed
    /// assertion is a better diagnosis than a truncated wait.
    /// </summary>
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

    private readonly string _databaseName = $"q2-automated-test-{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    public string ConnectionString => $"Data Source={_databaseName};Mode=Memory;Cache=Shared";

    /// <summary>
    /// The recorder itself. Use <see cref="RecordedEventsAsync"/> to read
    /// events; this is for the recorder's own switches, such as
    /// <see cref="RecordingTransport.FailOnSend"/>.
    /// </summary>
    public RecordingTransport SentryEvents => Services.GetRequiredService<RecordingTransport>();

    /// <summary>
    /// Events the Sentry SDK delivered during this test, after waiting for
    /// delivery to actually happen.
    /// </summary>
    /// <remarks>
    /// <see cref="IHub.CaptureException"/> does not send anything; it queues the
    /// envelope on the SDK's background worker, which serialises it and calls
    /// the transport on another thread. Reading the recorder straight after the
    /// HTTP response therefore races that worker: usually the event has arrived,
    /// but on a loaded machine it has not, and the test sees an empty
    /// collection.
    ///
    /// Flushing first removes the race in both directions — "exactly one event"
    /// no longer depends on timing, and "no event" no longer passes merely
    /// because delivery had not caught up yet.
    /// </remarks>
    public async Task<IReadOnlyList<RecordedSentryEvent>> RecordedEventsAsync()
    {
        await FlushSentryAsync();
        return SentryEvents.Events;
    }

    public async ValueTask InitializeAsync()
    {
        // Open before anything else touches the database, otherwise the
        // in-memory database is discarded when the first connection closes.
        _keepAlive = new SqliteConnection(ConnectionString);
        await _keepAlive.OpenAsync(TestContext.Current.CancellationToken);

        await ResetAsync();
    }

    /// <summary>
    /// Applies every migration to the empty database and inserts the
    /// AutomatedTest seed. Tests that arrange their own state call this again.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<DatabaseMaintenance>();

        // Real migrations, not EnsureCreated: the schema under test is the
        // schema the migrations produce.
        await maintenance.MigrateAsync(CancellationToken.None);
        await maintenance.SeedAsync(SeedProfile.AutomatedTest, replaceExisting: true, CancellationToken.None);

        // Drain before clearing, not after: an event queued by the previous
        // test but not yet delivered would otherwise arrive once this test is
        // already running and be counted as its own.
        await FlushSentryAsync();
        SentryEvents.Clear();

        ((SequentialTestIdGenerator)Services.GetRequiredService<IIdGenerator>()).Reset();
    }

    /// <summary>
    /// Waits for the SDK's background worker to hand everything it has queued
    /// to the transport.
    /// </summary>
    private Task FlushSentryAsync() =>
        Services.GetRequiredService<IHub>().FlushAsync(FlushTimeout);

    /// <summary>Runs <paramref name="action"/> against a fresh DbContext.</summary>
    public async Task WithDatabaseAsync(Func<Q2DbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<Q2DbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ApplicationEnvironments.AutomatedTest);
        builder.UseSetting($"ConnectionStrings:{PersistenceRegistration.ConnectionStringName}", ConnectionString);

        // Proves the release actually reaches the events rather than being
        // whatever the test assembly version happens to be.
        builder.UseSetting("Sentry:Release", "q2@integration-tests");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddSingleton<IIdGenerator, SequentialTestIdGenerator>();
        });
    }

    /// <summary>
    /// Also satisfies xunit's <see cref="IAsyncLifetime"/>. Closing the
    /// keep-alive connection is what actually destroys the in-memory database.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync();
            _keepAlive = null;
        }

        await base.DisposeAsync();
    }
}
