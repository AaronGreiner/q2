using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Q2.Api.Features.Notifications;
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
    /// Where uploaded images land during this fixture's lifetime.
    /// </summary>
    /// <remarks>
    /// A private temporary directory, for the same reason the database is
    /// private: the store writes real files, and its default location is beside
    /// the developer's own <c>api/.data</c> database. A test run must not add
    /// anything to that, and two test classes running in parallel must not be
    /// able to see each other's uploads.
    /// </remarks>
    public string ImageRootPath { get; } =
        Path.Combine(Path.GetTempPath(), $"q2-automated-test-images-{Guid.NewGuid():N}");

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

    /// <summary>
    /// Structured logs the SDK delivered during this test, having waited for at
    /// least one matching <paramref name="expected"/>.
    /// </summary>
    /// <remarks>
    /// Logs and metrics do not leave the SDK one at a time: they are collected
    /// in a batch buffer that is handed over when it fills or when its timer
    /// expires, and <see cref="IHub.FlushAsync"/> does not reach into it — it
    /// flushes the worker the batch is eventually queued on. So a test asserting
    /// on a log has to wait for that hand-over rather than read straight after
    /// the request.
    ///
    /// Waiting for a specific entry, rather than for a fixed delay, is also what
    /// makes a negative assertion sound: emit the line that must not be sent,
    /// then a harmless one, and wait for the harmless one. Once it has arrived,
    /// the batch containing both has been processed, so "the secret is not in
    /// this collection" means it was dropped rather than still queued.
    /// </remarks>
    public Task<IReadOnlyList<RecordedSentryLog>> RecordedLogsAsync(Func<RecordedSentryLog, bool> expected) =>
        WaitForAsync(() => SentryEvents.Logs, expected);

    /// <summary>Metrics the SDK delivered during this test, having waited for a matching one.</summary>
    public Task<IReadOnlyList<RecordedSentryMetric>> RecordedMetricsAsync(Func<RecordedSentryMetric, bool> expected) =>
        WaitForAsync(() => SentryEvents.Metrics, expected);

    private async Task<IReadOnlyList<T>> WaitForAsync<T>(
        Func<IReadOnlyList<T>> read,
        Func<T, bool> expected)
    {
        var deadline = DateTimeOffset.UtcNow + FlushTimeout;

        while (true)
        {
            await FlushSentryAsync();

            var recorded = read();
            if (recorded.Any(expected) || DateTimeOffset.UtcNow >= deadline)
            {
                // Returned rather than asserted on: a test that waited in vain
                // should fail on its own assertion, which names what it wanted.
                return recorded;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Everything this host would have pushed, and the dial that makes it fail.</summary>
    public RecordingPushSender PushSender => Services.GetRequiredService<RecordingPushSender>();

    /// <summary>The hub under test, for the tests that emit a metric themselves.</summary>
    public IHub Hub => Services.GetRequiredService<IHub>();

    /// <summary>
    /// A logger from the running application, for the tests that need a log line
    /// with a specific shape. It is the same <c>ILogger</c> pipeline the services
    /// use, so what happens to the line here is what happens to theirs.
    /// </summary>
    public ILogger Logger(string category) =>
        Services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

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

        // Put back by the reset rather than by whoever moved it: a test that
        // fails halfway through must not leave the clock at half past eight for
        // everything that runs after it.
        ((FixedTimeProvider)Services.GetRequiredService<TimeProvider>()).Reset();

        // Same reasoning as the clock: a test that changed how the sender
        // behaves must not leave it that way for whatever runs next.
        PushSender.Reset();

        // The seed inserts no images, so an empty directory is the matching
        // starting point — otherwise the second test in a class would begin
        // with the first one's uploads still on disk.
        if (Directory.Exists(ImageRootPath))
        {
            Directory.Delete(ImageRootPath, recursive: true);
        }
    }

    /// <summary>
    /// Waits for the SDK's background worker to hand everything it has queued
    /// to the transport.
    /// </summary>
    /// <remarks>
    /// Logs and metrics are batched separately from events — they leave in
    /// container envelopes once a buffer fills or a timer expires — so their own
    /// buffers have to be pushed out first. Without that, a test asserting on a
    /// log would be waiting for a timer rather than for the SDK.
    /// </remarks>
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

        builder.UseSetting("Q2:Images:RootPath", ImageRootPath);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddSingleton<IIdGenerator, SequentialTestIdGenerator>();

            /*
             * Notifications are recorded rather than sent.
             *
             * The encryption is checked against RFC 8291's own worked example
             * in the unit tests; what the pipeline has to prove is who gets
             * told, and that needs no network. Registered over the typed
             * HttpClient the application adds, which is what the descriptor
             * removal below is for.
             */
            services.RemoveAll<IPushSender>();
            services.AddSingleton<RecordingPushSender>();
            services.AddSingleton<IPushSender>(provider => provider.GetRequiredService<RecordingPushSender>());
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

        if (Directory.Exists(ImageRootPath))
        {
            Directory.Delete(ImageRootPath, recursive: true);
        }

        await base.DisposeAsync();
    }
}
