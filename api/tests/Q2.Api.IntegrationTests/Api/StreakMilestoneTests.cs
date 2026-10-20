using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A streak milestone reaches friends' feeds (#90): exactly one line, from the
/// believed photograph that carried the run there.
/// </summary>
[Trait("Category", "Integration")]
public class StreakMilestoneTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record PersonDocument(Guid Id);

    private sealed record ActivityDocument(Guid Id, PersonDocument Actor, ActivityKind Kind, int? Amount);

    /// <summary>Gives the signed-in person a run of <paramref name="days"/> days ending yesterday.</summary>
    private async Task GiveRunEndingYesterdayAsync(int days)
    {
        var cancellation = TestContext.Current.CancellationToken;

        await Factory.WithDatabaseAsync(async database =>
            await database.Set<DailyCheckIn>()
                .Where(day => day.PersonId == AutomatedTestSeed.CurrentPersonId)
                .ExecuteDeleteAsync(cancellation));

        await Factory.WithDatabaseAsync(async database =>
        {
            var me = await database.People
                .Include(person => person.CheckIns)
                .SingleAsync(person => person.Id == AutomatedTestSeed.CurrentPersonId, cancellation);

            for (var back = days; back >= 1; back--)
            {
                me.CheckIn(Guid.CreateVersion7(), Q2ApiFactory.Today.AddDays(-back));
            }

            await database.SaveChangesAsync(cancellation);
        });
    }

    private static async Task<IReadOnlyList<ActivityDocument>> MilestonesInFeedAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/feed", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ActivityDocument>>())
        .Where(entry => entry.Kind == ActivityKind.StreakReached && entry.Actor.Id == AutomatedTestSeed.CurrentPersonId)
        .ToList();

    private async Task DeliverAndHaveBelievedAsync(HttpClient friend)
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TheSeventhDayPutsOneLineInFriendsFeeds()
    {
        await GiveRunEndingYesterdayAsync(6);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await DeliverAndHaveBelievedAsync(friend);

        var line = Assert.Single(await MilestonesInFeedAsync(friend));
        Assert.Equal(7, line.Amount);
    }

    [Fact]
    public async Task ADayThatIsNoMilestoneSaysNothing()
    {
        await GiveRunEndingYesterdayAsync(5);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await DeliverAndHaveBelievedAsync(friend);

        Assert.Empty(await MilestonesInFeedAsync(friend));
    }
}
