namespace Q2.Api.Infrastructure.Observability;

/// <summary>Configuration was requested that cannot be satisfied safely.</summary>
public sealed class SentryConfigurationException(string message) : Exception(message);

/// <summary>
/// The resolved Sentry configuration for this process.
/// </summary>
/// <remarks>
/// Sentry is <em>not</em> switched off outside production. It initialises the
/// same way in every environment; what differs is the environment name, the
/// sampling and whether events go to a real endpoint or to the local recording
/// transport. That is the only way local and test runs can prove the
/// integration actually works.
///
/// Sources, in order of precedence: explicit <c>Sentry:*</c> configuration,
/// then the plain <c>SENTRY_*</c> environment variables from
/// <c>.env.example</c>, then per-environment defaults.
/// </remarks>
public sealed record SentrySettings
{
    public const string SectionName = "Sentry";

    public required string? Dsn { get; init; }

    /// <summary>
    /// Whether the SDK should send anywhere. False still leaves the SDK
    /// initialised — capture calls simply become no-ops.
    /// </summary>
    public required bool Enabled { get; init; }

    public required string Environment { get; init; }

    public required string Release { get; init; }

    public required bool Debug { get; init; }

    /// <summary>
    /// Whether <c>ILogger</c> output is additionally sent as Sentry structured
    /// logs. Events answer "what went wrong"; logs answer "what was the system
    /// doing" — see docs/observability.md.
    /// </summary>
    /// <remarks>
    /// On by default, and switchable per environment for the same reason the
    /// sample rates are: log volume is the part that needs a budget, not the
    /// question of whether the integration exists.
    /// </remarks>
    public required bool EnableLogs { get; init; }

    /// <summary>
    /// Whether the counters emitted through <c>SentrySdk.Metrics</c> are sent.
    /// </summary>
    public required bool EnableMetrics { get; init; }

    /// <summary>Share of error events kept. 1.0 everywhere except production.</summary>
    public required float SampleRate { get; init; }

    /// <summary>Share of transactions traced.</summary>
    public required double TracesSampleRate { get; init; }

    /// <summary>Send events to an in-process recorder instead of the network.</summary>
    public required bool UseRecordingTransport { get; init; }

    /// <summary>
    /// Optional JSON Lines file the recorder appends to, so an out-of-process
    /// test (Playwright) can assert on events the server produced.
    /// </summary>
    public required string? RecordingFilePath { get; init; }

    /// <summary>Tags attached to every event.</summary>
    public required IReadOnlyDictionary<string, string> DefaultTags { get; init; }

    public static SentrySettings FromConfiguration(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var section = configuration.GetSection(SectionName);

        var dsn = FirstNonEmpty(section["Dsn"], configuration["SENTRY_DSN"]);
        var testTransport = FirstNonEmpty(section["TestTransport"], configuration["SENTRY_TEST_TRANSPORT"]);
        var useRecording = string.Equals(testTransport, "recording", StringComparison.OrdinalIgnoreCase);

        var enabledSetting = FirstNonEmpty(section["Enabled"], configuration["SENTRY_ENABLED"]);
        var explicitlyEnabled = bool.TryParse(enabledSetting, out var parsed) ? parsed : (bool?)null;

        var isProtectedEnvironment = ApplicationEnvironments.Protected.Contains(hostEnvironment.EnvironmentName);

        if (useRecording && isProtectedEnvironment)
        {
            throw new SentryConfigurationException(
                $"The Sentry recording transport must never be used in '{hostEnvironment.EnvironmentName}'. "
                + "Unset SENTRY_TEST_TRANSPORT (or Sentry:TestTransport).");
        }

        // The recording transport supplies its own placeholder DSN: the SDK
        // needs a syntactically valid one to initialise, but nothing is sent.
        if (useRecording && string.IsNullOrWhiteSpace(dsn))
        {
            dsn = FirstNonEmpty(configuration["SENTRY_TEST_DSN"], "https://0000000000000000000000000000000@localhost/0");
        }

        if (explicitlyEnabled == true && string.IsNullOrWhiteSpace(dsn))
        {
            throw new SentryConfigurationException(
                "Sentry is enabled (Sentry:Enabled / SENTRY_ENABLED = true) but no DSN is configured. "
                + "Set Sentry:Dsn (or SENTRY_DSN), or remove the explicit enable.");
        }

        if (!string.IsNullOrWhiteSpace(dsn) && !LooksLikeDsn(dsn))
        {
            throw new SentryConfigurationException(
                "The configured Sentry DSN is not a valid DSN. Expected the form "
                + "https://<public-key>@<host>/<project-id>. The value itself is not logged.");
        }

        var enabled = explicitlyEnabled ?? !string.IsNullOrWhiteSpace(dsn);

        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["service.name"] = ReleaseIdentity.ServiceName,
        };

        AddIfPresent(tags, "test.run_id", configuration["TEST_RUN_ID"]);
        AddIfPresent(tags, "seed.profile", configuration["TEST_SEED_PROFILE"]);
        AddIfPresent(tags, "git.sha", configuration["GIT_COMMIT_SHA"]);
        AddIfPresent(tags, "ci.run_id", configuration["CI_RUN_ID"]);

        return new SentrySettings
        {
            Dsn = dsn,
            Enabled = enabled,
            Environment = FirstNonEmpty(section["Environment"], SentryEnvironments.For(hostEnvironment.EnvironmentName))!,
            Release = ReleaseIdentity.Resolve(configuration),
            Debug = bool.TryParse(section["Debug"], out var debug) && debug,
            EnableLogs = ParseSwitch(FirstNonEmpty(section["EnableLogs"], configuration["SENTRY_ENABLE_LOGS"]), true),
            EnableMetrics = ParseSwitch(FirstNonEmpty(section["EnableMetrics"], configuration["SENTRY_ENABLE_METRICS"]), true),
            SampleRate = float.TryParse(section["SampleRate"], out var sample) ? sample : DefaultSampleRate,
            TracesSampleRate = double.TryParse(section["TracesSampleRate"], out var traces)
                ? traces
                : DefaultTracesSampleRate(hostEnvironment),
            UseRecordingTransport = useRecording,
            RecordingFilePath = FirstNonEmpty(
                section["TestTransportFile"],
                configuration["SENTRY_TEST_TRANSPORT_FILE"]),
            DefaultTags = tags,
        };
    }

    /// <summary>
    /// Error events are never sampled away. Local runs and tests need every
    /// event — a test waiting for a specific one must not lose it to chance —
    /// and error volume is low enough that production keeps all of them too.
    /// Trace volume is the part that needs a budget; see
    /// <see cref="DefaultTracesSampleRate"/>.
    /// </summary>
    private const float DefaultSampleRate = 1.0f;

    private static double DefaultTracesSampleRate(IHostEnvironment environment) => environment.EnvironmentName switch
    {
        ApplicationEnvironments.Production => 0.1,
        ApplicationEnvironments.Staging => 0.5,
        _ => 1.0,
    };

    private static bool LooksLikeDsn(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && !string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath.Trim('/').Length > 0;

    private static bool ParseSwitch(string? value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    private static void AddIfPresent(IDictionary<string, string> tags, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags[key] = value;
        }
    }
}
