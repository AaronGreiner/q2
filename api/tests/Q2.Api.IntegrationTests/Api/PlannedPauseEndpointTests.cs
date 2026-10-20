using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Setting every goal aside at once, and a pause planned ahead (#99).
/// </summary>
/// <remarks>
/// The pause rules themselves are <c>GoalPauseTests</c>. What is worth the
/// pipeline here: one request pauses many goals and each goal's friends are
/// told as before; a planned pause tells nobody until it begins, and then the
/// maintenance pass does.
/// </remarks>
[Trait("Category", "Integration")]
public class PlannedPauseEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private const string Reason = "Urlaub an der Ostsee, ohne Netz.";

    private sealed record PauseDocument(Guid Id, DateOnly StartsOn, DateOnly EndsOn, int Days);

    private sealed record GoalDocument(Guid Id, PauseDocument? Pause, PauseDocument? PlannedPause);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private sealed record SkipDocument(Guid GoalId, string Title, PauseRefusal Reason);

    private sealed record PauseAllDocument(IReadOnlyList<Guid> Paused, IReadOnlyList<SkipDocument> Skipped);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    private static async Task<GoalDocument> GoalAsync(HttpClient client, Guid id) =>
        (await (await client.GetAsync($"/api/goals/{id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal;

    private async Task<int> PausedLinesAsync(Guid goalId)
    {
        var count = 0;

        await Factory.WithDatabaseAsync(async database =>
            count = await database.Notifications.CountAsync(
                line => line.Kind == NotificationKind.GoalPaused && line.TargetId == goalId,
                TestContext.Current.CancellationToken));

        return count;
    }

    [Fact]
    public async Task OneRequestSetsEveryRunningGoalAsideAndEachGoalsFriendsAreTold()
    {
        var response = await Client.PostJsonAsync("/api/goals/pauses", new { reason = Reason, days = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<PauseAllDocument>();

        Assert.Contains(AutomatedTestSeed.ActiveGoalId, result.Paused);
        Assert.NotNull((await GoalAsync(Client, AutomatedTestSeed.ActiveGoalId)).Pause);

        // The shared goal's friend hears of it as they would of one pause.
        Assert.Equal(1, await PausedLinesAsync(AutomatedTestSeed.ActiveGoalId));

        // Nothing stopped was touched, and nothing anybody else owns.
        Assert.DoesNotContain(AutomatedTestSeed.CompletedGoalId, result.Paused);
    }

    [Fact]
    public async Task AGoalThatCannotBePausedIsLeftOutWithItsReasonAndTheRestArePaused()
    {
        // A photograph waiting for its verdict on the shared goal.
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var result = await (await Client.PostJsonAsync("/api/goals/pauses", new { reason = Reason, days = 2 }))
            .ReadAsync<PauseAllDocument>();

        var skipped = Assert.Single(result.Skipped, skip => skip.GoalId == AutomatedTestSeed.ActiveGoalId);
        Assert.Equal(PauseRefusal.VoteRunning, skipped.Reason);
        Assert.NotEmpty(result.Paused);
    }

    [Fact]
    public async Task APausePlannedAheadIsToldWhenItBeginsAndNotBefore()
    {
        var startsOn = Q2ApiFactory.Today.AddDays(2);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause",
            new { reason = Reason, days = 3, startsOn });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The owner sees it planned; the friend sees nothing yet.
        var mine = await GoalAsync(Client, AutomatedTestSeed.ActiveGoalId);
        Assert.Null(mine.Pause);
        Assert.Equal(startsOn, mine.PlannedPause!.StartsOn);
        Assert.Null((await GoalAsync(friend, AutomatedTestSeed.ActiveGoalId)).PlannedPause);
        Assert.Equal(0, await PausedLinesAsync(AutomatedTestSeed.ActiveGoalId));

        // Its first morning: the pass announces it, once.
        Clock.Set(Q2ApiFactory.Now.AddDays(2));
        for (var pass = 0; pass < 2; pass++)
        {
            using var scope = Factory.Services.CreateScope();
            await ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider)
                .RunOnceAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await PausedLinesAsync(AutomatedTestSeed.ActiveGoalId));

        var later = await ClientForAsync(AutomatedTestSeed.CurrentPersonEmail);
        Assert.Equal(startsOn, (await GoalAsync(later, AutomatedTestSeed.ActiveGoalId)).Pause!.StartsOn);
    }

    [Fact]
    public async Task APauseStartsWithinAMonth()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause",
            new { reason = Reason, days = 3, startsOn = Q2ApiFactory.Today.AddDays(PauseRules.MaxDaysAhead + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("StartsOn", (await response.ReadAsync<ValidationDocument>()).Errors.Keys);
    }

    [Fact]
    public async Task APlannedPauseCanBeTakenBackBeforeItBegins()
    {
        await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause",
            new { reason = Reason, days = 3, startsOn = Q2ApiFactory.Today.AddDays(5) });

        var response = await Client.DeleteAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await GoalAsync(Client, AutomatedTestSeed.ActiveGoalId)).PlannedPause);
    }

    public override async ValueTask DisposeAsync()
    {
        Clock.Reset();
        await base.DisposeAsync();
    }
}
