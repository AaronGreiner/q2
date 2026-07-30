using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.UnitTests.Persistence;

/// <summary>
/// The guard that stands between a test script and someone's data.
/// </summary>
/// <remarks>
/// Each test disables exactly one of the five conditions and asserts a refusal,
/// which is what "do not rely on a single boolean" has to mean in practice.
/// </remarks>
[Trait("Category", "Unit")]
public class DatabaseResetGuardTests
{
    private const string DataDirectory = "/repo/api/.data";

    private static ResetContext Context(
        string environment = ApplicationEnvironments.ManualTesting,
        string provider = DatabaseResetGuard.SqliteProviderName,
        string? filePath = "/repo/api/.data/q2-manual-testing.db",
        string connectionString = "Data Source=/repo/api/.data/q2-manual-testing.db",
        SeedProfile profile = SeedProfile.ManualTesting,
        bool allowSetting = true) =>
        new(environment, provider, connectionString, filePath, DataDirectory, profile, allowSetting);

    [Fact]
    public void AProperlyConfiguredManualTestingResetIsAllowed()
    {
        Assert.True(DatabaseResetGuard.Evaluate(Context()).IsAllowed);
    }

    [Theory]
    [InlineData(ApplicationEnvironments.ManualTesting, SeedProfile.ManualTesting)]
    [InlineData(ApplicationEnvironments.AutomatedTest, SeedProfile.AutomatedTest)]
    [InlineData(ApplicationEnvironments.E2E, SeedProfile.E2E)]
    public void TheThreeTestEnvironmentsAreAllowed(string environment, SeedProfile profile)
    {
        var fileName = $"q2-{environment.ToLowerInvariant()}.db";

        var decision = DatabaseResetGuard.Evaluate(Context(
            environment: environment,
            filePath: $"{DataDirectory}/{fileName}",
            connectionString: $"Data Source={DataDirectory}/{fileName}",
            profile: profile));

        Assert.True(decision.IsAllowed, decision.Reason);
    }

    [Fact]
    public void DevelopmentIsRefusedEvenThoughItIsLocal()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(
            environment: ApplicationEnvironments.Development,
            profile: SeedProfile.Development));

        Assert.False(decision.IsAllowed);
        Assert.Contains("may not be reset", decision.Reason);
    }

    [Theory]
    [InlineData(ApplicationEnvironments.Staging)]
    [InlineData(ApplicationEnvironments.Production)]
    public void ProtectedEnvironmentsAreRefusedEvenWithEverythingElseSetToPass(string environment)
    {
        var decision = DatabaseResetGuard.Evaluate(Context(environment: environment, allowSetting: true));

        Assert.False(decision.IsAllowed);
        Assert.Contains("protected", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownEnvironmentIsRefused()
    {
        Assert.False(DatabaseResetGuard.Evaluate(Context(environment: "QaSandbox")).IsAllowed);
    }

    [Fact]
    public void TheOptInFlagIsNecessary()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(allowSetting: false));

        Assert.False(decision.IsAllowed);
        Assert.Contains("AllowDestructiveReset", decision.Reason);
    }

    [Fact]
    public void ANonSqliteProviderIsRefused()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(provider: "Npgsql.EntityFrameworkCore.PostgreSQL"));

        Assert.False(decision.IsAllowed);
        Assert.Contains("SQLite", decision.Reason);
    }

    [Fact]
    public void AFileOutsideTheKnownDirectoriesIsRefused()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(
            filePath: "/var/lib/q2/q2-manual-testing.db",
            connectionString: "Data Source=/var/lib/q2/q2-manual-testing.db"));

        Assert.False(decision.IsAllowed);
        Assert.Contains("outside", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFileInTheTempDirectoryIsAllowed()
    {
        var path = Path.Combine(Path.GetTempPath(), "q2-e2e-run-123.db");

        var decision = DatabaseResetGuard.Evaluate(Context(
            environment: ApplicationEnvironments.E2E,
            filePath: path,
            connectionString: $"Data Source={path}",
            profile: SeedProfile.E2E));

        Assert.True(decision.IsAllowed, decision.Reason);
    }

    [Theory]
    [InlineData("production-live.db")]
    [InlineData("q2.db")]
    [InlineData("q2-development.db")]
    [InlineData("customers.sqlite")]
    public void AFileNameThatIsNotRecognisablyATestDatabaseIsRefused(string fileName)
    {
        var decision = DatabaseResetGuard.Evaluate(Context(
            filePath: $"{DataDirectory}/{fileName}",
            connectionString: $"Data Source={DataDirectory}/{fileName}"));

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void AMismatchedSeedProfileIsRefused()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(profile: SeedProfile.Development));

        Assert.False(decision.IsAllowed);
        Assert.Contains("seed profile", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnInMemoryDatabaseIsAllowed()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(
            environment: ApplicationEnvironments.AutomatedTest,
            filePath: null,
            connectionString: "Data Source=q2-test;Mode=Memory;Cache=Shared",
            profile: SeedProfile.AutomatedTest));

        Assert.True(decision.IsAllowed, decision.Reason);
    }

    [Fact]
    public void AnUnresolvableTargetIsRefused()
    {
        var decision = DatabaseResetGuard.Evaluate(Context(
            filePath: null,
            connectionString: "Data Source=somewhere.db"));

        Assert.False(decision.IsAllowed);
    }
}
