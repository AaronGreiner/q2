using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.UnitTests.Persistence;

[Trait("Category", "Unit")]
public class DatabaseLocationTests
{
    private static readonly string DataDirectory =
        Path.Combine(Path.GetTempPath(), "q2-location-tests");

    [Fact]
    public void ARelativePathIsResolvedAgainstTheDataDirectory()
    {
        var (connectionString, filePath) = DatabaseLocation.Resolve("Data Source=q2-development.db", DataDirectory);

        Assert.Equal(Path.Combine(DataDirectory, "q2-development.db"), filePath);
        Assert.Contains(DataDirectory, connectionString);
    }

    [Fact]
    public void AnAbsolutePathIsKept()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "q2-elsewhere.db");

        var (_, filePath) = DatabaseLocation.Resolve($"Data Source={absolute}", DataDirectory);

        Assert.Equal(absolute, filePath);
    }

    [Fact]
    public void AnInMemoryConnectionStringHasNoFile()
    {
        var (_, filePath) = DatabaseLocation.Resolve("Data Source=q2-test;Mode=Memory;Cache=Shared", DataDirectory);

        Assert.Null(filePath);
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=q2-test;Mode=Memory;Cache=Shared")]
    public void InMemoryConnectionStringsAreRecognised(string connectionString)
    {
        Assert.True(DatabaseLocation.IsInMemory(connectionString));
    }

    [Fact]
    public void AFileConnectionStringIsNotInMemory()
    {
        Assert.False(DatabaseLocation.IsInMemory("Data Source=q2-development.db"));
    }

    [Fact]
    public void AConnectionStringWithoutADataSourceIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseLocation.Resolve("Cache=Shared", DataDirectory));
    }

    [Fact]
    public void AnEmptyConnectionStringIsRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => DatabaseLocation.Resolve("   ", DataDirectory));
    }

    [Fact]
    public void TheDataDirectoryFallsBackToTheApiFolder()
    {
        // ContentRoot is api/src/Q2.Api, so the data directory is api/.data.
        var resolved = DatabaseLocation.ResolveDataDirectory(Path.Combine("/repo", "api", "src", "Q2.Api"));

        Assert.Equal(Path.GetFullPath(Path.Combine("/repo", "api", ".data")), resolved);
    }
}
