using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Badges are earned in the running app (#22): through each of the paths that
/// can earn one — a believed photograph, kudos given, and a week the
/// maintenance pass sees end. What each badge takes is <c>BadgeRulesTests</c>.
/// </summary>
[Trait("Category", "Integration")]
public class BadgeAwardTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record BadgeDocument(BadgeKey Key, bool IsEarned, DateOnly? EarnedOn);

    private sealed record ProfileDocument(IReadOnlyList<BadgeDocument> Badges);

    private sealed record GoalDocument(Guid Id);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    private static async Task<BadgeDocument> BadgeAsync(HttpClient client, BadgeKey key) =>
        (await (await client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>())
        .Badges.Single(badge => badge.Key == key);

    private async Task TakeAwayAsync(Guid personId, BadgeKey key) =>
        await Factory.WithDatabaseAsync(async database =>
            await database.PersonBadges
                .Where(badge => badge.PersonId == personId && badge.Badge == key)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));

    [Fact]
    public async Task TheBelievedPhotographThatMakesAFortnightEarnsTheStreakHero()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await TakeAwayAsync(AutomatedTestSeed.CurrentPersonId, BadgeKey.StreakHero);

        // Thirteen days ending yesterday, and nothing yet today.
        await Factory.WithDatabaseAsync(async database =>
            await database.DailyCheckIns
                .Where(day => day.PersonId == AutomatedTestSeed.CurrentPersonId)
                .ExecuteDeleteAsync(cancellation));

        await Factory.WithDatabaseAsync(async database =>
        {
            var me = await database.People.Include(person => person.CheckIns)
                .SingleAsync(person => person.Id == AutomatedTestSeed.CurrentPersonId, cancellation);

            for (var back = 13; back >= 1; back--)
            {
                me.CheckIn(Guid.CreateVersion7(), Q2ApiFactory.Today.AddDays(-back));
            }

            await database.SaveChangesAsync(cancellation);
        });

        Assert.False((await BadgeAsync(Client, BadgeKey.StreakHero)).IsEarned);

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm }))
            .EnsureSuccessStatusCode();

        var badge = await BadgeAsync(Client, BadgeKey.StreakHero);
        Assert.True(badge.IsEarned);
        Assert.Equal(Q2ApiFactory.Today, badge.EarnedOn);
    }

    [Fact]
    public async Task TheHundredthKudosEarnsTheKudosGiver()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await TakeAwayAsync(AutomatedTestSeed.CurrentPersonId, BadgeKey.KudosGiver);

        var given = 0;
        var fresh = Guid.Empty;

        await Factory.WithDatabaseAsync(async database =>
        {
            given = await database.ActivityKudos.CountAsync(kudos => kudos.PersonId == AutomatedTestSeed.CurrentPersonId, cancellation);

            // A line of the friend's the seed has no kudos of mine on.
            fresh = await database.ActivityEvents
                .Where(activity => activity.ActorPersonId == AutomatedTestSeed.FriendPersonId
                    && !activity.Kudos.Any(kudos => kudos.PersonId == AutomatedTestSeed.CurrentPersonId))
                .Select(activity => activity.Id)
                .FirstAsync(cancellation);
        });

        // Ninety-nine given, counting the seed's: one line each, a month ago.
        await Factory.WithDatabaseAsync(async database =>
        {
            for (var line = given; line < BadgeRules.KudosGiverCount - 1; line++)
            {
                var activity = ActivityEvent.Create(
                    Guid.CreateVersion7(),
                    AutomatedTestSeed.FriendPersonId,
                    ActivityKind.GoalCreated,
                    $"Line {line}",
                    null,
                    kudosCount: 0,
                    Q2ApiFactory.Now.AddDays(-30).AddMinutes(line));

                activity.GiveKudos(Guid.CreateVersion7(), AutomatedTestSeed.CurrentPersonId);
                database.ActivityEvents.Add(activity);
            }

            await database.SaveChangesAsync(cancellation);
        });

        Assert.False((await BadgeAsync(Client, BadgeKey.KudosGiver)).IsEarned);

        (await Client.PostAsync($"/api/feed/{fresh}/kudos", content: null, cancellation)).EnsureSuccessStatusCode();

        Assert.True((await BadgeAsync(Client, BadgeKey.KudosGiver)).IsEarned);
    }

    [Fact]
    public async Task AWeekWithEveryWindowKeptEarnsTheWeeklyWinnerOnceItIsOver()
    {
        // Somebody with nothing else due: one Do, once a week.
        var four = await ClientForAsync(SeedAccounts.EmailFor("@test.four"));
        var created = await (await four.PostJsonAsync("/api/goals", new
        {
            title = "Einmal die Woche schwimmen",
            category = "Sport",
            schedule = new { kind = "Times", times = 1, period = "Week" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        })).ReadAsync<GoalDocument>();

        var proof = await four.DeliverAcceptedProofAsync(created.Id);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm }))
            .EnsureSuccessStatusCode();

        // Kept on Monday, but the week is not over.
        Assert.False((await BadgeAsync(four, BadgeKey.WeeklyWinner)).IsEarned);

        // The pass on the next Monday opens the next window, and sees the week.
        Clock.Set(Q2ApiFactory.Now.AddDays(7));
        using (var scope = Factory.Services.CreateScope())
        {
            await ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider)
                .RunOnceAsync(TestContext.Current.CancellationToken);
        }

        // Signed in again: a week on, the first session has expired.
        var later = await ClientForAsync(SeedAccounts.EmailFor("@test.four"));
        Assert.True((await BadgeAsync(later, BadgeKey.WeeklyWinner)).IsEarned);
    }

    public override async ValueTask DisposeAsync()
    {
        Clock.Reset();
        await base.DisposeAsync();
    }
}
