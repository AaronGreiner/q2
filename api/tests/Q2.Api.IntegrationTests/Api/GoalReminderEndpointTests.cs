using System.Net;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Moving a goal's daily reminder — the one thing about a running goal its
/// owner changes on their own.
/// </summary>
[Trait("Category", "Integration")]
public class GoalReminderEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record GoalDocument(Guid Id, GoalStatus Status, TimeOnly? ReminderAt);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private Task<HttpResponseMessage> SetAsync(HttpClient client, Guid goalId, string? reminderAt) =>
        client.PutJsonAsync($"/api/goals/{goalId}/reminder", new { reminderAt });

    [Fact]
    public async Task TheOwnerMovesTheReminderAndItStays()
    {
        var response = await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TimeOnly(7, 30), (await response.ReadAsync<GoalDocument>()).ReminderAt);

        var detail = await (await Client.GetAsync(
                $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
                TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        Assert.Equal(new TimeOnly(7, 30), detail.Goal.ReminderAt);
    }

    [Fact]
    public async Task NullTakesTheReminderAway()
    {
        await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        var response = await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadAsync<GoalDocument>()).ReminderAt);
    }

    [Fact]
    public async Task AFriendOnTheGoalCannotMoveIt()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await SetAsync(friend, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        // Not theirs, and a 404 rather than a 403: the answer is the same as for
        // a goal that does not exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AStoppedGoalKeepsItsReminder()
    {
        var response = await SetAsync(Client, AutomatedTestSeed.CompletedGoalId, "07:30:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
