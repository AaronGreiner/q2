using Microsoft.Extensions.Hosting;

namespace Q2.Api.Infrastructure;

/// <summary>
/// The six environments q2 knows about. The value comes from
/// <c>ASPNETCORE_ENVIRONMENT</c> and drives configuration, the database
/// lifecycle, the Sentry environment and whether destructive operations are
/// permitted at all.
/// </summary>
public static class ApplicationEnvironments
{
    public const string Development = "Development";
    public const string ManualTesting = "ManualTesting";
    public const string AutomatedTest = "AutomatedTest";
    public const string E2E = "E2E";
    public const string Staging = "Staging";
    public const string Production = "Production";

    public static readonly IReadOnlyList<string> All =
    [
        Development, ManualTesting, AutomatedTest, E2E, Staging, Production,
    ];

    /// <summary>
    /// The only environments in which the database may be deleted and rebuilt.
    /// Development is deliberately excluded: a developer's local data is not
    /// something a script gets to throw away as a side effect.
    /// </summary>
    public static readonly IReadOnlySet<string> DestructiveResetAllowed =
        new HashSet<string>(StringComparer.Ordinal) { ManualTesting, AutomatedTest, E2E };

    /// <summary>Environments that must never be touched by test tooling.</summary>
    public static readonly IReadOnlySet<string> Protected =
        new HashSet<string>(StringComparer.Ordinal) { Staging, Production };

    public static bool IsManualTesting(this IHostEnvironment environment) =>
        environment.IsEnvironment(ManualTesting);

    public static bool IsAutomatedTest(this IHostEnvironment environment) =>
        environment.IsEnvironment(AutomatedTest);

    public static bool IsE2E(this IHostEnvironment environment) =>
        environment.IsEnvironment(E2E);

    /// <summary>True for Development, ManualTesting, AutomatedTest and E2E.</summary>
    public static bool IsLocalOrTest(this IHostEnvironment environment) =>
        !Protected.Contains(environment.EnvironmentName);

    public static bool IsKnown(string environmentName) =>
        All.Contains(environmentName, StringComparer.Ordinal);
}
