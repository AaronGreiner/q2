using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// What the real SQLite provider does with the model.
/// </summary>
/// <remarks>
/// Against the real provider on purpose:
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> is not a relational database
/// and would happily accept a schema SQLite rejects, a query it cannot
/// translate and a cascade it does not perform.
/// </remarks>
[Trait("Category", "Persistence")]
public class PersistenceTests
{
    private static readonly DateTimeOffset Now = Q2ApiFactory.Now;
    private static readonly DateOnly Today = Q2ApiFactory.Today;

    private static async Task<SqliteTestDatabase> MigratedAsync()
    {
        var database = await SqliteTestDatabase.InMemoryAsync();

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        return database;
    }

    private static Person BuildPerson(string handle = "@robin") =>
        Person.Create(Guid.CreateVersion7(), "Robin Sample", handle, "RS", AvatarColors.Indigo);

    private static Goal BuildGoal(DateOnly? targetDate = null) => Goal.Create(
        Guid.CreateVersion7(), "Walk 8.000 steps a day", "Every day counts.", "target",
        GoalRhythm.Daily, false, 4, 10, new TimeOnly(18, 0), targetDate, Now);

    [Fact]
    public async Task AGoalSurvivesARoundTripWithEverythingHangingOffIt()
    {
        await using var database = await MigratedAsync();

        var person = BuildPerson();
        var goal = BuildGoal(Today.AddDays(30));
        goal.AddParticipant(Guid.CreateVersion7(), person.Id);
        goal.RecordContribution(Guid.CreateVersion7(), Today);

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            context.Goals.Add(goal);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals
                .Include(g => g.Participants)
                .Include(g => g.Contributions)
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(goal.Title, stored.Title);
            Assert.Equal(new TimeOnly(18, 0), stored.ReminderAt);
            Assert.Equal(Today.AddDays(30), stored.TargetDate);
            Assert.Equal(40, stored.ProgressPercent);
            Assert.Single(stored.Participants);
            Assert.Single(stored.Contributions);
        }
    }

    [Fact]
    public async Task AChildAddedThroughItsAggregateIsActuallyInserted()
    {
        // Regression: EF Core defaults a GUID key to ValueGeneratedOnAdd, which
        // makes a new child inside a tracked aggregate look like a row that
        // already exists — the insert was silently downgraded and a streak
        // simply never grew. Q2DbContext turns that default off.
        await using var database = await MigratedAsync();

        var person = BuildPerson();

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var tracked = await context.People
                .Include(p => p.CheckIns)
                .SingleAsync(TestContext.Current.CancellationToken);

            tracked.CheckIn(Guid.CreateVersion7(), Today);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            Assert.Equal(1, await context.DailyCheckIns.CountAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task GoalsCanBeOrderedByWhenTheyWereCreated()
    {
        // SQLite cannot ORDER BY a DateTimeOffset at all, which is why the
        // column is a UTC DateTime behind a value converter. Without it this
        // throws rather than returning the wrong order.
        await using var database = await MigratedAsync();

        await using (var context = database.CreateContext())
        {
            context.Goals.AddRange(BuildGoal(), BuildGoal());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var ordered = await context.Goals
                .OrderByDescending(goal => goal.CreatedAt)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, ordered.Count);
        }
    }

    [Fact]
    public async Task AnInstantComesBackAsUtcRatherThanAsALocalTime()
    {
        await using var database = await MigratedAsync();

        await using (var context = database.CreateContext())
        {
            context.Goals.Add(BuildGoal());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Goals.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
            Assert.Equal(Now, stored.CreatedAt);
        }
    }

    [Fact]
    public async Task DeletingAGoalTakesItsParticipantsAndTasksWithIt()
    {
        await using var database = await MigratedAsync();

        var person = BuildPerson();
        var goal = BuildGoal();
        goal.AddParticipant(Guid.CreateVersion7(), person.Id);

        var task = GoalTask.Create(
            Guid.CreateVersion7(), goal.Id, "Joggen", GoalRhythm.Daily, null, null, null, null, null, null, 0, Now);

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            context.Goals.Add(goal);
            context.GoalTasks.Add(task);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            context.Goals.Remove(await context.Goals.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            Assert.Equal(0, await context.GoalParticipants.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await context.GoalTasks.CountAsync(TestContext.Current.CancellationToken));

            // The person is not a detail of the goal and stays.
            Assert.Equal(1, await context.People.CountAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task DeletingAGoalLeavesTheConversationAboutItStanding()
    {
        await using var database = await MigratedAsync();

        var person = BuildPerson();
        var goal = BuildGoal();
        var conversation = Conversation.CreateDirect(Guid.CreateVersion7(), goal.Id, Now);
        conversation.AddParticipant(Guid.CreateVersion7(), person.Id);

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            context.Goals.Add(goal);
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            context.Goals.Remove(await context.Goals.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Conversations.SingleAsync(TestContext.Current.CancellationToken);

            // It just stops showing the progress bar.
            Assert.Null(stored.GoalId);
        }
    }

    [Fact]
    public async Task OneCheckInPerPersonPerDayIsEnforcedByTheDatabase()
    {
        await using var database = await MigratedAsync();

        var person = BuildPerson();
        person.CheckIn(Guid.CreateVersion7(), Today);

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            // Bypasses the entity's own guard on purpose: the invariant has to
            // hold even if a second writer arrives.
            context.DailyCheckIns.Add(new DailyCheckInProbe(person.Id, Today).Build());

            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task TwoPeopleCannotShareAHandle()
    {
        await using var database = await MigratedAsync();
        await using var context = database.CreateContext();

        context.People.Add(BuildPerson("@taken"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.People.Add(BuildPerson("@taken"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnActivityKeepsItsSourceEvenWhenTheGoalIsGone()
    {
        // The feed is a record of what happened; deleting a goal must not
        // rewrite the past, which is why SourceId is not a foreign key.
        await using var database = await MigratedAsync();

        var person = BuildPerson();
        var goal = BuildGoal();

        await using (var context = database.CreateContext())
        {
            context.People.Add(person);
            context.Goals.Add(goal);
            context.ActivityEvents.Add(ActivityEvent.Create(
                Guid.CreateVersion7(), person.Id, ActivityKind.GoalCreated, goal.Title, null, 0, Now, goal.Id));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            context.Goals.Remove(await context.Goals.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.ActivityEvents.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(goal.Id, stored.SourceId);
        }
    }

    /// <summary>
    /// Builds a raw <see cref="DailyCheckIn"/> without going through
    /// <see cref="Person.CheckIn"/>, so the database constraint can be tested
    /// rather than the entity's guard.
    /// </summary>
    private sealed class DailyCheckInProbe(Guid personId, DateOnly date)
    {
        public DailyCheckIn Build()
        {
            var person = Person.Create(personId, "Probe", "@probe", "PR", AvatarColors.Teal);
            person.CheckIn(Guid.CreateVersion7(), date);
            return person.CheckIns[0];
        }
    }
}
