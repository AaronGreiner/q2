using Q2.Api.Infrastructure.Observability.Testing;
using Sentry.Extensibility;

namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// Wires up structured logging and Sentry.
/// </summary>
public static class ObservabilityRegistration
{
    public static WebApplicationBuilder AddQ2Observability(this WebApplicationBuilder builder)
    {
        AddLogging(builder);
        AddSentry(builder);
        return builder;
    }

    /// <summary>
    /// Human-readable single-line logs locally, JSON everywhere else so a log
    /// shipper can parse them. Request logging with headers or bodies is
    /// deliberately not enabled — see docs/privacy.md.
    /// </summary>
    private static void AddLogging(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        if (builder.Environment.IsDevelopment() || builder.Environment.IsManualTesting())
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        }
        else
        {
            builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        }
    }

    private static void AddSentry(WebApplicationBuilder builder)
    {
        var settings = SentrySettings.FromConfiguration(builder.Configuration, builder.Environment);
        var scrubber = new SentryEventScrubber();

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(scrubber);

        RecordingTransport? recordingTransport = settings.UseRecordingTransport
            ? new RecordingTransport(settings.RecordingFilePath)
            : null;

        if (recordingTransport is not null)
        {
            // Registered so tests and the diagnostics endpoint can inspect it.
            builder.Services.AddSingleton(recordingTransport);
        }

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = settings.Enabled ? settings.Dsn ?? string.Empty : string.Empty;
            options.Environment = settings.Environment;
            options.Release = settings.Release;
            options.Debug = settings.Debug;
            options.SampleRate = settings.SampleRate;
            options.TracesSampleRate = settings.TracesSampleRate;

            // Never attach IP addresses, cookies or user identity automatically.
            options.SendDefaultPii = false;
            options.MaxRequestBodySize = RequestSize.None;
            options.AutoSessionTracking = false;

            // We already map environment names ourselves; leave them alone.
            options.AdjustStandardEnvironmentNameCasing = false;

            // Errors become events; lower levels only ever become breadcrumbs.
            options.MinimumEventLevel = LogLevel.Error;
            options.MinimumBreadcrumbLevel = LogLevel.Information;

            options.AttachStacktrace = true;

            foreach (var (key, value) in settings.DefaultTags)
            {
                options.DefaultTags[key] = value;
            }

            options.SetBeforeSend((@event, _) => scrubber.Scrub(@event));
            options.SetBeforeBreadcrumb((breadcrumb, _) => scrubber.ScrubBreadcrumb(breadcrumb));
            options.TracesSampler = context => SampleTrace(context, settings.TracesSampleRate);

            if (recordingTransport is not null)
            {
                options.Transport = recordingTransport;
            }
        });
    }

    /// <summary>
    /// Keeps health checks and static noise out of the trace budget while
    /// leaving the configured rate in place for everything else.
    /// </summary>
    private static double SampleTrace(TransactionSamplingContext context, double configuredRate)
    {
        if (context.TryGetHttpPath() is { } path
            && (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase)))
        {
            return 0.0;
        }

        return configuredRate;
    }

    private static string? TryGetHttpPath(this TransactionSamplingContext context) =>
        context.CustomSamplingContext.TryGetValue("__HttpPath", out var path) ? path as string : null;

    /// <summary>
    /// Logs, once at startup, whether monitoring is actually on. Without this
    /// a missing DSN is invisible until the day an incident is not reported.
    /// </summary>
    public static void LogObservabilityStatus(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<SentrySettings>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Q2.Observability");

        var transport = settings.UseRecordingTransport ? "local recording transport" : "Sentry HTTP transport";

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Dsn))
        {
            logger.LogWarning(
                "Sentry is NOT sending events (no DSN configured). Environment={SentryEnvironment}, Release={Release}. "
                + "The application runs normally; set Sentry:Dsn to enable reporting.",
                settings.Environment,
                settings.Release);
            return;
        }

        logger.LogInformation(
            "Sentry active via {Transport}. Environment={SentryEnvironment}, Release={Release}, "
            + "SampleRate={SampleRate}, TracesSampleRate={TracesSampleRate}",
            transport,
            settings.Environment,
            settings.Release,
            settings.SampleRate,
            settings.TracesSampleRate);
    }
}
