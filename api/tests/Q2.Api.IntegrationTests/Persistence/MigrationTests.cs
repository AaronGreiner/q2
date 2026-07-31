using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
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
            Assert.Equal(4, result.PeopleInserted);
        }

        // 4. write
        var id = Guid.CreateVersion7();
        var person = Person.Create(
            Guid.CreateVersion7(), "Robin Sample", "@robin.after-migrating", "RS", AvatarColors.Indigo);

        // Somebody to share it with. The owner is not added as a participant —
        // they are already on the goal by owning it.
        var participant = Person.Create(
            Guid.CreateVersion7(), "Sam Sample", "@sam.after-migrating", "SS", AvatarColors.Pink);

        await using (var context = database.CreateContext())
        {
            var goal = Goal.Create(
                id, person.Id, "Written after migrating", null, "medal", GoalRhythm.Weekly, isGroup: false,
                completedSteps: 4, totalSteps: 20, reminderAt: new TimeOnly(18, 0),
                targetDate: new DateOnly(2026, 12, 24), Q2ApiFactory.Now);
            goal.AddParticipant(Guid.CreateVersion7(), participant.Id);
            goal.RecordContribution(Guid.CreateVersion7(), Q2ApiFactory.Today);

            context.People.AddRange(person, participant);
            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // 5. read back through a new connection
        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals
                .Include(g => g.Participants)
                .Include(g => g.Contributions)
                .SingleAsync(g => g.Id == id, TestContext.Current.CancellationToken);

            Assert.Equal("Written after migrating", stored.Title);
            Assert.Equal(new DateOnly(2026, 12, 24), stored.TargetDate);
            Assert.Equal(new TimeOnly(18, 0), stored.ReminderAt);
            Assert.Equal(20, stored.ProgressPercent);
            Assert.Single(stored.Participants);
            Assert.Single(stored.Contributions);
            Assert.Equal(4, await context.Goals.CountAsync(TestContext.Current.CancellationToken));
        }

        Assert.True(database.FileExists);
    }

    /// <summary>
    /// The other case a migration has to survive: an existing database with
    /// rows in it.
    /// </summary>
    /// <remarks>
    /// <c>AccountsAndTwoSidedFriendships</c> is the first migration here that
    /// converts data rather than only reshaping tables. It reads
    /// <c>People.IsCurrentUser</c> — the only thing in the old schema that says
    /// who "you" were — and then drops it, so the conversion has to happen
    /// while the column is still there.
    ///
    /// The migration rebuilds the affected tables explicitly because EF's
    /// generated SQLite rebuild would disable foreign keys outside its
    /// transaction. This test proves that the explicit copy keeps the data and
    /// its ownership intact. Without it, "it did not fail on an empty database"
    /// would still not be evidence about data at all.
    /// </remarks>
    [Fact]
    public async Task AnExistingDatabaseKeepsItsDataThroughTheAccountsMigration()
    {
        const string beforeAccounts = "20260730190319_KudosExperience";

        await using var database = SqliteTestDatabase.TemporaryFile();

        var me = Guid.CreateVersion7();
        var friend = Guid.CreateVersion7();
        var asked = Guid.CreateVersion7();
        var invited = Guid.CreateVersion7();
        var suggested = Guid.CreateVersion7();
        var goal = Guid.CreateVersion7();
        var task = Guid.CreateVersion7();

        await using (var context = database.CreateContext())
        {
            // 1. the schema as it was before accounts existed
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(beforeAccounts, TestContext.Current.CancellationToken);

            // 2. rows in the old shape, written as SQL because the C# model
            //    that produced them no longer exists
            await ExecuteAsync(context, $"""
                INSERT INTO People (Id, DisplayName, Handle, Initials, AvatarColor, IsCurrentUser, KudosReceived, GoalsCompleted)
                VALUES
                  ('{me}',        'Alt Ich',      '@alt.ich',      'AI', '#15803d', 1, 3, 1),
                  ('{friend}',    'Alt Freund',   '@alt.freund',   'AF', '#4f46e5', 0, 0, 0),
                  ('{asked}',     'Alt Anfrage',  '@alt.anfrage',  'AA', '#db2777', 0, 0, 0),
                  ('{invited}',   'Alt Gefragt',  '@alt.gefragt',  'AG', '#b45309', 0, 0, 0),
                  ('{suggested}', 'Alt Vorschlag','@alt.vorschlag','AV', '#0e7490', 0, 0, 0);

                INSERT INTO Friendships (Id, PersonId, Status, MutualFriends) VALUES
                  ('{Guid.CreateVersion7()}', '{friend}',    'Accepted',  2),
                  ('{Guid.CreateVersion7()}', '{asked}',     'Requested', 1),
                  ('{Guid.CreateVersion7()}', '{invited}',   'Invited',   0),
                  ('{Guid.CreateVersion7()}', '{suggested}', 'Suggested', 4);

                INSERT INTO Goals (Id, Title, Icon, Rhythm, Status, IsGroup, CompletedSteps, TotalSteps, CreatedAt)
                VALUES ('{goal}', 'Altes Ziel', 'target', 'Daily', 'Active', 0, 2, 10, '2026-06-01 09:00:00');

                INSERT INTO GoalTasks (Id, GoalId, Title, Rhythm, SortOrder, CreatedAt)
                VALUES ('{task}', '{goal}', 'Alte Aufgabe', 'Daily', 0, '2026-06-01 09:00:00');
                """);

            // 3. the migration under test
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            // Everything that was there is still there, and now belongs to the
            // person who was flagged as "you".
            var storedGoal = await context.Goals.SingleAsync(TestContext.Current.CancellationToken);
            var storedTask = await context.GoalTasks.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Altes Ziel", storedGoal.Title);
            Assert.Equal(me, storedGoal.OwnerPersonId);
            Assert.Equal(me, storedTask.OwnerPersonId);

            var friendships = await context.Friendships.ToListAsync(TestContext.Current.CancellationToken);

            // The suggestion is gone: it was a guess stored as though it were a
            // relationship, and suggestions are derived now.
            Assert.Equal(3, friendships.Count);
            Assert.DoesNotContain(friendships, f => f.Involves(suggested));

            // "They asked you" keeps its direction …
            var incoming = friendships.Single(f => f.Involves(asked));
            Assert.Equal(asked, incoming.RequesterId);
            Assert.Equal(me, incoming.AddresseeId);
            Assert.True(incoming.IsIncomingFor(me));

            // … and so does "you asked them", the other way round.
            var outgoing = friendships.Single(f => f.Involves(invited));
            Assert.Equal(me, outgoing.RequesterId);
            Assert.Equal(invited, outgoing.AddresseeId);
            Assert.True(outgoing.IsOutgoingFrom(me));

            var accepted = friendships.Single(f => f.Involves(friend));
            Assert.Equal(FriendshipStatus.Accepted, accepted.Status);

            // No accounts: a password hash is not something a migration may
            // invent, which is why README section 6 says to start over.
            Assert.Equal(0, await context.Users.CountAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MigrationSqlNeverDisablesForeignKeys()
    {
        await using var database = await SqliteTestDatabase.InMemoryAsync();
        await using var context = database.CreateContext();

        var script = context.GetService<IMigrator>().GenerateScript();

        Assert.DoesNotContain("PRAGMA foreign_keys = 0", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFailedAccountsMigrationRollsBackSchemaAndDataChanges()
    {
        const string beforeAccounts = "20260730190319_KudosExperience";
        const string accounts = "20260731052301_AccountsAndTwoSidedFriendships";

        await using var database = SqliteTestDatabase.TemporaryFile();
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(beforeAccounts, TestContext.Current.CancellationToken);

        // A goal without the old single-user marker makes ownership conversion
        // fail after the migration has already created its backup tables. The
        // whole migration must still roll back to the exact old shape.
        await ExecuteAsync(context, $"""
            INSERT INTO Goals
                (Id, Title, Status, CompletedSteps, TotalSteps, Icon, IsGroup, Rhythm, CreatedAt)
            VALUES
                ('{Guid.CreateVersion7()}', 'Rollback probe', 'Active', 0, 1, 'target', 0, 'Daily',
                 '2026-07-31 09:00:00');
            """);

        var exception = await Assert.ThrowsAsync<SqliteException>(
            () => migrator.MigrateAsync(accounts, TestContext.Current.CancellationToken));

        Assert.Equal(19, exception.SqliteErrorCode);
        Assert.DoesNotContain(
            accounts,
            await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await ScalarAsync(context, "SELECT COUNT(*) FROM Goals WHERE Title = 'Rollback probe';"));
        Assert.Equal(1, await ScalarAsync(
            context,
            "SELECT COUNT(*) FROM pragma_table_info('People') WHERE name = 'IsCurrentUser';"));
        Assert.Equal(0, await ScalarAsync(
            context,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '__q2_%';"));
        Assert.Equal(1, await ScalarAsync(context, "PRAGMA foreign_keys;"));
    }

    [Fact]
    public async Task AccountsMigrationCanBeRevertedAndAppliedAgain()
    {
        const string beforeAccounts = "20260730190319_KudosExperience";
        const string accounts = "20260731052301_AccountsAndTwoSidedFriendships";

        await using var database = SqliteTestDatabase.TemporaryFile();
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await migrator.MigrateAsync(beforeAccounts, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            accounts,
            await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));

        await migrator.MigrateAsync(accounts, TestContext.Current.CancellationToken);

        Assert.Contains(
            accounts,
            await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await ScalarAsync(context, "PRAGMA foreign_keys;"));
        Assert.Equal(0, await ScalarAsync(
            context,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '__q2_%';"));
    }

    /// <summary>Runs several statements as one script, outside EF's model.</summary>
    private static async Task ExecuteAsync(Q2DbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> ScalarAsync(Q2DbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
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
