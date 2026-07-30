using System.Reflection;

namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// The release identifier shared by the frontend and the backend.
/// </summary>
/// <remarks>
/// Format: <c>q2@&lt;version&gt;</c>. Both services report the same value so a
/// Sentry issue in <c>q2-app</c> can be lined up with one in <c>q2-api</c>, and
/// so a source map upload can be attached to the right release.
///
/// Resolution order — first hit wins:
/// <list type="number">
///   <item><c>Sentry:Release</c> from configuration</item>
///   <item><c>Q2_RELEASE</c></item>
///   <item><c>q2@&lt;GIT_COMMIT_SHA (short)&gt;</c></item>
///   <item><c>q2@&lt;assembly informational version&gt;</c></item>
/// </list>
/// Never random: two starts of the same build always produce the same value.
/// </remarks>
public static class ReleaseIdentity
{
    public const string Prefix = "q2";

    public const string ServiceName = "q2-api";

    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration["Sentry:Release"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var fromEnvironment = configuration["Q2_RELEASE"] ?? Environment.GetEnvironmentVariable("Q2_RELEASE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var commit = configuration["GIT_COMMIT_SHA"] ?? Environment.GetEnvironmentVariable("GIT_COMMIT_SHA");
        if (!string.IsNullOrWhiteSpace(commit))
        {
            return $"{Prefix}@{commit[..Math.Min(commit.Length, 12)]}";
        }

        return $"{Prefix}@{AssemblyVersion()}";
    }

    private static string AssemblyVersion()
    {
        var informational = typeof(ReleaseIdentity).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0-local";
        }

        // Strip the "+<sha>" build metadata the SDK appends, keeping the value
        // identical to what the frontend derives from package.json.
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? informational[..plus] : informational;
    }
}
