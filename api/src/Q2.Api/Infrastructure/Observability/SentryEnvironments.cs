namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// Maps the ASP.NET Core environment onto the Sentry environment name.
/// </summary>
/// <remarks>
/// The mapping lives here and nowhere else. Two rules matter:
/// local runs and production must never share an environment name, and the
/// name must be stable, because dashboards, alerts and issue filters are built
/// on it.
/// <code>
/// Development   -> local-development
/// ManualTesting -> manual-testing
/// AutomatedTest -> automated-test
/// E2E           -> e2e
/// Staging       -> staging
/// Production    -> production
/// </code>
/// </remarks>
public static class SentryEnvironments
{
    public const string LocalDevelopment = "local-development";
    public const string ManualTesting = "manual-testing";
    public const string AutomatedTest = "automated-test";
    public const string E2E = "e2e";
    public const string Staging = "staging";
    public const string Production = "production";

    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ApplicationEnvironments.Development] = LocalDevelopment,
            [ApplicationEnvironments.ManualTesting] = ManualTesting,
            [ApplicationEnvironments.AutomatedTest] = AutomatedTest,
            [ApplicationEnvironments.E2E] = E2E,
            [ApplicationEnvironments.Staging] = Staging,
            [ApplicationEnvironments.Production] = Production,
        };

    /// <summary>
    /// Returns the Sentry environment for an ASP.NET Core environment name.
    /// Unknown names fall back to a clearly marked value rather than to
    /// something that could be mistaken for production.
    /// </summary>
    public static string For(string aspNetCoreEnvironment) =>
        Map.TryGetValue(aspNetCoreEnvironment, out var mapped)
            ? mapped
            : $"unknown-{aspNetCoreEnvironment.ToLowerInvariant()}";
}
