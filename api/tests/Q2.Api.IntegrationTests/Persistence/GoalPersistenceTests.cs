using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// What actually survives a round trip through SQLite.
/// </summary>
/// <remarks>
/// The schema always comes from the real migrations — never
/// <c>EnsureCreated</c> — so these tests fail if a migration and the model ever
/// drift apart.
/// </remarks>
[Trait("Category", "Persistence")]
public class GoalPersistenceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);

    private static async Task<SqliteTestDatabase> MigratedDatabaseAsync()
    {
        var database = await SqliteTestDatabase.InMemoryAsync();

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        return database;
    }

    [Fact]
    public async Task AGoalSurvivesARoundTripWithEveryField()
    {
        await using var database = await MigratedDatabaseAsync();
        var id = Guid.CreateVersion7();

        await using (var context = database.CreateContext())
        {
            var goal = Goal.Create(id, "Run a 10k", "Together, twice a week.", 62, new DateOnly(2026, 9, 1), CreatedAt);
            goal.AddParticipant(Guid.CreateVersion7(), "Robin Sample");
            goal.AddParticipant(Guid.CreateVersion7(), "Kim Example");

            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals
                .Include(g => g.Participants)
                .SingleAsync(g => g.Id == id, TestContext.Current.CancellationToken);

            Assert.Equal("Run a 10k", stored.Title);
            Assert.Equal("Together, twice a week.", stored.Description);
            Assert.Equal(62, stored.ProgressPercent);
            Assert.Equal(GoalStatus.Active, stored.Status);
            Assert.Equal(new DateOnly(2026, 9, 1), stored.TargetDate);
            Assert.Equal(2, stored.Participants.Count);
        }
    }

    [Fact]
    public async Task InstantsComeBackAsTheSameUtcInstant()
    {
        await using var database = await MigratedDatabaseAsync();
        var id = Guid.CreateVersion7();

        // Written with a non-UTC offset on purpose: the storage layer
        // normalises to UTC, and the value must survive that unchanged.
        var berlinNoon = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2));

        await using (var context = database.CreateContext())
        {
            context.Goals.Add(Goal.Create(id, "Timezone check", null, 0, null, berlinNoon));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals.SingleAsync(g => g.Id == id, TestContext.Current.CancellationToken);

            Assert.Equal(berlinNoon.ToUniversalTime(), stored.CreatedAt);
            Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
        }
    }

    [Fact]
    public async Task GoalsCanBeOrderedByCreationDateInTheDatabase()
    {
        await using var database = await MigratedDatabaseAsync();

        await using (var context = database.CreateContext())
        {
            context.Goals.AddRange(
                Goal.Create(Guid.CreateVersion7(), "Oldest", null, 0, null, CreatedAt.AddDays(-10)),
                Goal.Create(Guid.CreateVersion7(), "Newest", null, 0, null, CreatedAt),
                Goal.Create(Guid.CreateVersion7(), "Middle", null, 0, null, CreatedAt.AddDays(-5)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            // Regression guard: with a raw DateTimeOffset column SQLite refuses
            // to translate this ORDER BY at all.
            var titles = await context.Goals
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => g.Title)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["Newest", "Middle", "Oldest"], titles);
        }
    }

    [Fact]
    public async Task TheStatusIsStoredAsReadableText()
    {
        await using var database = await MigratedDatabaseAsync();

        await using (var context = database.CreateContext())
        {
            context.Goals.Add(Goal.Create(Guid.CreateVersion7(), "Done", null, 100, null, CreatedAt));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM Goals LIMIT 1";

        // Not an ordinal: reordering the enum must not silently rewrite history.
        Assert.Equal("Completed", (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingAGoalRemovesItsParticipants()
    {
        await using var database = await MigratedDatabaseAsync();
        var id = Guid.CreateVersion7();

        await using (var context = database.CreateContext())
        {
            var goal = Goal.Create(id, "Shared", null, 0, null, CreatedAt);
            goal.AddParticipant(Guid.CreateVersion7(), "Robin Sample");
            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            context.Goals.Remove(await context.Goals.SingleAsync(g => g.Id == id, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            Assert.Empty(await context.GoalParticipants.ToListAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task TheSameParticipantCannotBeStoredTwiceOnOneGoal()
    {
        await using var database = await MigratedDatabaseAsync();
        var goalId = Guid.CreateVersion7();

        await using (var context = database.CreateContext())
        {
            var goal = Goal.Create(goalId, "Shared", null, 0, null, CreatedAt);
            goal.AddParticipant(Guid.CreateVersion7(), "Robin Sample");
            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            // Bypasses the domain guard on purpose: the database is the last
            // line of defence and has to hold the rule by itself.
            var duplicateId = Guid.CreateVersion7();

            // Values are passed as parameters so EF applies the same Guid type
            // mapping the provider uses; a hand-formatted string would only
            // ever fail the foreign key.
            var exception = await Assert.ThrowsAsync<SqliteException>(async () =>
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO GoalParticipants (Id, GoalId, DisplayName) VALUES ({duplicateId}, {goalId}, {"Robin Sample"})",
                    TestContext.Current.CancellationToken));

            Assert.Contains("UNIQUE", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
