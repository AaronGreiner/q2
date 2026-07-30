using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// How the seeder behaves against a real database — in particular the rule that
/// keeps a developer's local data safe.
/// </summary>
[Trait("Category", "Seed")]
public class DatabaseSeederTests
{
    private static async Task<SqliteTestDatabase> MigratedDatabaseAsync()
    {
        var database = await SqliteTestDatabase.InMemoryAsync();

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        return database;
    }

    [Fact]
    public async Task SeedingAnEmptyDatabaseInsertsTheProfile()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.Development, replaceExisting: false, TestContext.Current.CancellationToken);

        Assert.False(result.WasSkipped);
        Assert.Equal(3, result.GoalsInserted);
        Assert.Equal(3, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedingANonEmptyDatabaseIsSkippedRatherThanDestructive()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        context.Goals.Add(Goal.Create(Guid.CreateVersion7(), "My own goal", null, 10, null, Q2ApiFactory.Now));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.Development, replaceExisting: false, TestContext.Current.CancellationToken);

        // This is what makes `bun run dev` safe to run every day.
        Assert.True(result.WasSkipped);
        Assert.Equal(0, result.GoalsInserted);
        Assert.Equal("My own goal", (await context.Goals.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    [Fact]
    public async Task ReplacingRemovesEverythingThatWasThereBefore()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var existing = Goal.Create(Guid.CreateVersion7(), "Will be replaced", null, 10, null, Q2ApiFactory.Now);
        existing.AddParticipant(Guid.CreateVersion7(), "Robin Sample");
        context.Goals.Add(existing);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);

        var titles = await context.Goals.Select(g => g.Title).ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Will be replaced", titles);
        Assert.Contains(E2ESeed.SharedGoalTitle, titles);

        // Participants of removed goals must not survive as orphans.
        Assert.Equal(
            await context.Goals.SelectMany(g => g.Participants).CountAsync(TestContext.Current.CancellationToken),
            await context.GoalParticipants.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RebuildingProducesByteIdenticalDataWithAFixedClock()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();
        var seeder = SqliteTestDatabase.CreateSeeder();

        await seeder.SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);
        var first = await SnapshotAsync(context);

        await seeder.SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);
        var second = await SnapshotAsync(context);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task TheE2eSeedKeepsItsPublishedIds()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);

        // Playwright navigates straight to this id.
        var shared = await context.Goals
            .Include(g => g.Participants)
            .SingleAsync(g => g.Id == E2ESeed.SharedGoalId, TestContext.Current.CancellationToken);

        Assert.Equal(E2ESeed.SharedGoalTitle, shared.Title);
        Assert.Equal(2, shared.Participants.Count);
    }

    [Fact]
    public async Task TheNoneProfileWritesNothing()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.None, replaceExisting: true, TestContext.Current.CancellationToken);

        Assert.True(result.WasSkipped);
        Assert.Equal(0, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<List<string>> SnapshotAsync(Q2DbContext context) =>
        await context.Goals
            .AsNoTracking()
            .Include(g => g.Participants)
            .OrderBy(g => g.Id)
            .Select(g => $"{g.Id}|{g.Title}|{g.Status}|{g.ProgressPercent}|{g.CreatedAt:O}|{g.TargetDate}|"
                + string.Join(",", g.Participants.OrderBy(p => p.Id).Select(p => p.Id + ":" + p.DisplayName)))
            .ToListAsync(TestContext.Current.CancellationToken);
}
