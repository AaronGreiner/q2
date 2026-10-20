using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// This week on the profile (#27): the person's own windows that end between
/// this Monday and this Sunday, by how they ended — and never the windows of
/// goals they only check.
/// </summary>
[Trait("Category", "Integration")]
public class ProfileWeekTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record WeekDocument(DateOnly From, DateOnly To, int Done, int Missed, int Relapsed, int Open);

    private sealed record ProfileDocument(WeekDocument ThisWeek);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    private async Task<WeekDocument> WeekAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/profile", TestContext.Current.CancellationToken)).ReadAsync<ProfileDocument>()).ThisWeek;

    /// <summary>What the database says the week holds, counted the slow way.</summary>
    private async Task<(int Done, int Missed, int Open)> CountedAsync(DateOnly from, DateOnly to)
    {
        var statuses = new List<GoalInstanceStatus>();

        await Factory.WithDatabaseAsync(async database =>
            statuses.AddRange(await database.GoalInstances
                .Where(window => window.DueOn >= from && window.DueOn <= to
                    && database.Goals.Any(goal => goal.Id == window.GoalId && goal.OwnerPersonId == AutomatedTestSeed.CurrentPersonId))
                .Select(window => window.Status)
                .ToListAsync(TestContext.Current.CancellationToken)));

        return (
            statuses.Count(status => status == GoalInstanceStatus.Done),
            statuses.Count(status => status == GoalInstanceStatus.Missed),
            statuses.Count(status => status == GoalInstanceStatus.Open));
    }

    [Fact]
    public async Task TheWeekRunsFromMondayToSundayAndCountsWhatItHolds()
    {
        // The fixture's day is a Monday.
        var week = await WeekAsync(Client);

        Assert.Equal(DayOfWeek.Monday, week.From.DayOfWeek);
        Assert.Equal(week.From.AddDays(6), week.To);
        Assert.Equal(Q2ApiFactory.Today, week.From);

        var counted = await CountedAsync(week.From, week.To);
        Assert.Equal(counted, (week.Done, week.Missed, week.Open));
        Assert.True(week.Open > 0);
    }

    [Fact]
    public async Task ABelievedPhotographMovesAWindowFromOpenToDone()
    {
        var before = await WeekAsync(Client);

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm })).EnsureSuccessStatusCode();

        var after = await WeekAsync(Client);

        Assert.Equal(before.Done + 1, after.Done);
        Assert.Equal(before.Open - 1, after.Open);
    }

    [Fact]
    public async Task OnSundayItIsStillThisWeekAndOnMondayANewOne()
    {
        Clock.Set(Q2ApiFactory.Now.AddDays(6));
        var sunday = await WeekAsync(await ClientForAsync(SeedAccounts.EmailFor("@test.one")));
        Assert.Equal(Q2ApiFactory.Today, sunday.From);

        Clock.Set(Q2ApiFactory.Now.AddDays(7));
        var monday = await WeekAsync(await ClientForAsync(SeedAccounts.EmailFor("@test.one")));
        Assert.Equal(Q2ApiFactory.Today.AddDays(7), monday.From);
    }

    [Fact]
    public async Task AGoalTheyOnlyCheckIsNotTheirs()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var before = await WeekAsync(Client);

        // A Do of the friend's, checked by this person, with a window open today.
        (await friend.PostJsonAsync("/api/goals", new
        {
            title = "Automated test: a Do of the friend's",
            category = "Other",
            schedule = new { kind = "Interval", everyDays = 1 },
            participantIds = new[] { AutomatedTestSeed.CurrentPersonId },
        })).EnsureSuccessStatusCode();

        Assert.Equal(before, await WeekAsync(Client));
    }

    public override async ValueTask DisposeAsync()
    {
        Clock.Reset();
        await base.DisposeAsync();
    }
}
