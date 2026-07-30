using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// Migrations are source code, and this is what keeps them honest.
/// </summary>
/// <remarks>
/// The central case is the one a new environment goes through: an empty file on
/// disk, every migration applied in order, a seed inserted, a row written and
/// read back. If that passes, a fresh deployment works.
/// </remarks>
[Trait("Category", "Migrations")]
public class MigrationTests
{
    [Fact]
    public async Task AnEmptyDatabaseCanBeMigratedSeededWrittenAndReadBack()
    {
        await using var database = SqliteTestDatabase.TemporaryFile();

        // 1. genuinely empty — the file does not exist yet
        Assert.False(database.FileExists);

        await using (var context = database.CreateContext())
        {
            // 2. apply every migration
            var pending = (await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.NotEmpty(pending);

            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var applied = (await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.Equal(pending, applied);

            // 3. seed
            var result = await SqliteTestDatabase.CreateSeeder()
                .SeedAsync(context, SeedProfile.AutomatedTest, replaceExisting: true, TestContext.Current.CancellationToken);
            Assert.Equal(3, result.GoalsInserted);
        }

        // 4. write
        var id = Guid.CreateVersion7();
        await using (var context = database.CreateContext())
        {
            var goal = Goal.Create(id, "Written after migrating", null, 20, new DateOnly(2026, 12, 24), Q2ApiFactory.Now);
            goal.AddParticipant(Guid.CreateVersion7(), "Robin Sample");
            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // 5. read back through a new connection
        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals
                .Include(g => g.Participants)
                .SingleAsync(g => g.Id == id, TestContext.Current.CancellationToken);

            Assert.Equal("Written after migrating", stored.Title);
            Assert.Equal(new DateOnly(2026, 12, 24), stored.TargetDate);
            Assert.Single(stored.Participants);
            Assert.Equal(4, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
        }

        Assert.True(database.FileExists);
    }

    [Fact]
    public async Task TheModelHasNoChangesThatAreMissingAMigration()
    {
        await using var database = await SqliteTestDatabase.InMemoryAsync();
        await using var context = database.CreateContext();

        var pending = context.Database.HasPendingModelChanges();

        Assert.False(
            pending,
            "The EF Core model has changed without a matching migration. "
            + "Run `bun run db:add-migration <Name>` and commit the result.");
    }

    [Fact]
    public async Task MigratingTwiceIsHarmless()
    {
        await using var database = SqliteTestDatabase.TemporaryFile();

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EveryMigrationIsNamedWithASortableTimestamp()
    {
        await using var database = await SqliteTestDatabase.InMemoryAsync();
        await using var context = database.CreateContext();

        var migrations = context.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.Equal(migrations, migrations.OrderBy(m => m, StringComparer.Ordinal));
        Assert.All(migrations, migration => Assert.Matches(@"^\d{14}_[A-Za-z0-9]+$", migration));
    }
}
