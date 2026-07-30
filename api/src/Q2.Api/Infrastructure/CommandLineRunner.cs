using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.Infrastructure;

/// <summary>
/// Maintenance commands that run against the fully configured application and
/// then exit, instead of serving HTTP.
/// </summary>
/// <remarks>
/// <code>
/// dotnet run --project api/src/Q2.Api -- db migrate
/// dotnet run --project api/src/Q2.Api -- db seed [--profile &lt;Profile&gt;] [--force]
/// dotnet run --project api/src/Q2.Api -- db reset
/// dotnet run --project api/src/Q2.Api -- db reset-and-seed
/// dotnet run --project api/src/Q2.Api -- openapi --output &lt;path&gt;
/// dotnet run --project api/src/Q2.Api -- sentry canary
/// </code>
/// Reusing the real host means these commands see exactly the configuration
/// the server would see — same connection string, same guard, same seeds.
/// The root <c>bun run db:*</c> scripts are thin wrappers around them.
/// </remarks>
public static class CommandLineRunner
{
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && args[0] is "db" or "openapi" or "sentry";

    public static async Task<int> RunAsync(WebApplication app, string[] args, CancellationToken cancellationToken)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Q2.Cli");

        try
        {
            return args[0] switch
            {
                "db" => await RunDatabaseCommandAsync(app, args, logger, cancellationToken),
                "openapi" => await RunOpenApiCommandAsync(app, args, logger, cancellationToken),
                "sentry" => await RunSentryCommandAsync(app, args, logger),
                _ => Fail(logger, $"Unknown command '{args[0]}'."),
            };
        }
        catch (DatabaseResetNotAllowedException exception)
        {
            // Expected outcome of a guard rail, not a crash: report it plainly.
            logger.LogError("{Message}", exception.Message);
            return 3;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Command failed.");
            return 1;
        }
    }

    private static async Task<int> RunDatabaseCommandAsync(
        WebApplication app,
        string[] args,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            return Fail(logger, "Usage: db <migrate|seed|reset|reset-and-seed> [--profile <Profile>] [--force]");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<DatabaseMaintenance>();

        var profile = ParseProfile(args);
        var force = args.Contains("--force", StringComparer.Ordinal);

        switch (args[1])
        {
            case "migrate":
                await maintenance.MigrateAsync(cancellationToken);
                return 0;

            case "seed":
                var seedResult = await maintenance.SeedAsync(profile, force, cancellationToken);
                logger.LogInformation(
                    "Seed '{SeedProfile}': {Outcome} ({Reason})",
                    seedResult.Profile,
                    seedResult.WasSkipped ? "skipped" : $"{seedResult.GoalsInserted} goal(s) inserted",
                    seedResult.Reason);
                return 0;

            case "reset":
                await maintenance.ResetAsync(cancellationToken);
                return 0;

            case "reset-and-seed":
                var result = await maintenance.ResetAndSeedAsync(cancellationToken);
                logger.LogInformation(
                    "Database rebuilt and seeded with profile '{SeedProfile}' ({GoalCount} goals).",
                    result.Profile,
                    result.GoalsInserted);
                return 0;

            default:
                return Fail(logger, $"Unknown db command '{args[1]}'.");
        }
    }

    private static async Task<int> RunOpenApiCommandAsync(
        WebApplication app,
        string[] args,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var outputIndex = Array.IndexOf(args, "--output");

        if (outputIndex < 0 || outputIndex + 1 >= args.Length)
        {
            return Fail(logger, "Usage: openapi --output <path>");
        }

        // `dotnet run` sets the working directory to the project folder, so a
        // relative path would land somewhere the caller did not expect.
        // Resolve it against api/ instead, which is stable and guessable.
        var requestedPath = args[outputIndex + 1];
        var apiRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
        var outputPath = Path.IsPathRooted(requestedPath)
            ? Path.GetFullPath(requestedPath)
            : Path.GetFullPath(Path.Combine(apiRoot, requestedPath));

        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Routes only reach the endpoint data sources once the host starts, so
        // the document is generated from a briefly started app. Port 0 lets the
        // OS pick a free port; nothing ever connects to it.
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(cancellationToken);

        try
        {
            // Document providers are registered per document name.
            var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(OpenApiConfiguration.DocumentName);
            var document = await provider.GetOpenApiDocumentAsync(cancellationToken);

            await using var stream = File.Create(outputPath);
            await document.SerializeAsJsonAsync(stream, OpenApiConfiguration.SpecVersion, cancellationToken);
        }
        finally
        {
            await app.StopAsync(cancellationToken);
        }

        logger.LogInformation("OpenAPI document written to {OutputPath}", outputPath);
        return 0;
    }

    /// <summary>
    /// <c>sentry canary</c> — sends one clearly synthetic event through the
    /// fully configured SDK.
    /// </summary>
    /// <remarks>
    /// Proves the real ingestion path works end to end, which the recording
    /// transport by design cannot. Meant for a trusted CI run against a
    /// dedicated non-production project.
    ///
    /// It refuses to run in Staging or Production, does nothing when the
    /// recording transport is active, and exits successfully when no DSN is
    /// configured — so a repository without the optional secret is not a
    /// failed build.
    /// </remarks>
    private static async Task<int> RunSentryCommandAsync(WebApplication app, string[] args, ILogger logger)
    {
        if (args.Length < 2 || args[1] != "canary")
        {
            return Fail(logger, "Usage: sentry canary");
        }

        if (ApplicationEnvironments.Protected.Contains(app.Environment.EnvironmentName))
        {
            return Fail(
                logger,
                $"Refusing to send a canary event from '{app.Environment.EnvironmentName}'. "
                + "Synthetic events must never reach a production project.");
        }

        var settings = app.Services.GetRequiredService<SentrySettings>();

        if (settings.UseRecordingTransport)
        {
            logger.LogWarning(
                "The recording transport is active, so nothing would be sent. "
                + "Unset SENTRY_TEST_TRANSPORT to exercise the real ingestion path.");
            return 0;
        }

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Dsn))
        {
            logger.LogInformation("No Sentry DSN configured; skipping the canary.");
            return 0;
        }

        var hub = app.Services.GetRequiredService<IHub>();

        var eventId = hub.CaptureException(new DiagnosticsTestException(
            "Synthetic q2 canary event from CI. This is not a real incident."));

        // Fire-and-forget capture happens on a background worker, and this
        // process is about to exit.
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(15));

        logger.LogInformation(
            "Canary event {EventId} sent. Environment={SentryEnvironment}, Release={Release}",
            eventId,
            settings.Environment,
            settings.Release);

        return 0;
    }

    private static SeedProfile? ParseProfile(string[] args)
    {
        var index = Array.IndexOf(args, "--profile");

        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        return Enum.TryParse<SeedProfile>(args[index + 1], ignoreCase: true, out var profile)
            ? profile
            : throw new ArgumentException(
                $"Unknown seed profile '{args[index + 1]}'. Expected one of: {string.Join(", ", Enum.GetNames<SeedProfile>())}.");
    }

    private static int Fail(ILogger logger, string message)
    {
        logger.LogError("{Message}", message);
        return 2;
    }
}
