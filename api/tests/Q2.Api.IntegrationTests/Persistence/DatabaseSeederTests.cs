using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
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
    public async Task SeedingAnEmptyDatabaseInsertsTheWholeProfile()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.Development, replaceExisting: false, TestContext.Current.CancellationToken);

        Assert.False(result.WasSkipped);
        Assert.Equal(10, result.PeopleInserted);
        Assert.Equal(6, result.GoalsInserted);

        // Every goal brings its own history, so this is the count that would
        // change if a seed quietly stopped laying one down.
        Assert.True(result.WindowsInserted > result.GoalsInserted);
        Assert.Equal(5, result.ConversationsInserted);

        Assert.Equal(10, await context.People.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(0, await context.ChatMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedingANonEmptyDatabaseIsSkippedRatherThanDestructive()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        context.People.Add(Person.Create(
            Guid.CreateVersion7(), "My own account", "@mine", "MO", AvatarColors.Teal));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.Development, replaceExisting: false, TestContext.Current.CancellationToken);

        // This is what makes `bun run dev` safe to run every day.
        Assert.True(result.WasSkipped);
        Assert.Equal(0, result.PeopleInserted);
        Assert.Equal("My own account", (await context.People.SingleAsync(TestContext.Current.CancellationToken)).DisplayName);
    }

    [Fact]
    public async Task ADatabaseWithPeopleButNoGoalsStillCountsAsSeeded()
    {
        // Somebody who deleted every goal has not asked for a fresh world.
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        context.People.Add(Person.Create(
            Guid.CreateVersion7(), "My own account", "@mine", "MO", AvatarColors.Teal));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.Development, replaceExisting: false, TestContext.Current.CancellationToken);

        Assert.True(result.WasSkipped);
    }

    [Fact]
    public async Task ReplacingRemovesEverythingThatWasThereBefore()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.ManualTesting, replaceExisting: true, TestContext.Current.CancellationToken);

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.AutomatedTest, replaceExisting: true, TestContext.Current.CancellationToken);

        Assert.False(result.WasSkipped);
        Assert.Equal(result.PeopleInserted, await context.People.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(result.GoalsInserted, await context.Goals.CountAsync(TestContext.Current.CancellationToken));

        // Nothing from the previous profile may be left dangling anywhere.
        Assert.All(
            await context.Goals.ToListAsync(TestContext.Current.CancellationToken),
            goal => Assert.StartsWith("Automated test", goal.Title));
    }

    [Fact]
    public async Task ReplacingTwiceWithTheSameProfileWorks()
    {
        // Fixed ids mean a stale change-tracker entry would collide on the
        // second run; the seeder clears it.
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var seeder = SqliteTestDatabase.CreateSeeder();

        await seeder.SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);
        var second = await seeder.SeedAsync(context, SeedProfile.E2E, replaceExisting: true, TestContext.Current.CancellationToken);

        Assert.False(second.WasSkipped);
        Assert.Equal(second.GoalsInserted, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheNoneProfileWritesNothing()
    {
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        var result = await SqliteTestDatabase.CreateSeeder()
            .SeedAsync(context, SeedProfile.None, replaceExisting: true, TestContext.Current.CancellationToken);

        Assert.True(result.WasSkipped);
        Assert.Equal(0, await context.People.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASeededWorldSatisfiesEveryConstraintTheSchemaHas()
    {
        // The seed builder is the only thing in the repository that writes a
        // whole graph at once, so this is where a missing unique index or a
        // wrong foreign key would first show up.
        await using var database = await MigratedDatabaseAsync();
        await using var context = database.CreateContext();

        foreach (var profile in new[]
                 {
                     SeedProfile.Development, SeedProfile.ManualTesting,
                     SeedProfile.AutomatedTest, SeedProfile.E2E,
                 })
        {
            await SqliteTestDatabase.CreateSeeder()
                .SeedAsync(context, profile, replaceExisting: true, TestContext.Current.CancellationToken);
        }

        // Re-seeding replaces rather than accumulates, and every person that
        // survives still has exactly one account to sign in with.
        var people = await context.People.CountAsync(TestContext.Current.CancellationToken);
        var accounts = await context.Users.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(people, accounts);
        Assert.Equal(5, people);
    }

    [Fact]
    public async Task EveryGoalKeepsItsParticipantsAndContributionsThroughARoundTrip()
    {
        await using var database = await MigratedDatabaseAsync();

        await using (var context = database.CreateContext())
        {
            await SqliteTestDatabase.CreateSeeder()
                .SeedAsync(context, SeedProfile.Development, replaceExisting: true, TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var goals = await context.Goals
                .Include(goal => goal.Participants)
                .Include(goal => goal.Instances)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains(goals, goal => goal.Participants.Count > 0);
            Assert.Contains(goals, goal => goal.Instances.Count > 0);
            Assert.All(goals, goal => Assert.Contains(goal.Icon, GoalIcons.All));
        }
    }
}
