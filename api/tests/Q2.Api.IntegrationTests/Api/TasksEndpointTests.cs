using System.Net;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The day's tasks, and what ticking one off actually does.
/// </summary>
[Trait("Category", "Integration")]
public class TasksEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record TaskDocument(
        Guid Id,
        Guid? GoalId,
        string Title,
        GoalRhythm Rhythm,
        TimeOnly? ReminderAt,
        bool IsDone,
        double? MeasuredValue,
        double? TargetValue,
        string? MeasureUnit,
        int? MeasurePercent);

    private sealed record ProfileDocument(int Streak, DaySummaryDocument Today);

    private sealed record DaySummaryDocument(int Done, int Total, int Percent);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    private Task<IReadOnlyList<TaskDocument>> ListAsync(string query = "") =>
        Client.GetAsync($"/api/tasks{query}", TestContext.Current.CancellationToken)
            .ContinueWith(response => response.Result.ReadAsync<IReadOnlyList<TaskDocument>>()).Unwrap();

    [Fact]
    public async Task TodayListsOnlyWhatIsScheduledForToday()
    {
        var tasks = await ListAsync();

        Assert.Equal(2, tasks.Count);
        Assert.DoesNotContain(tasks, task => task.Id == AutomatedTestSeed.FutureTaskId);
    }

    [Fact]
    public async Task AskingForAllIncludesTheOnesThatAreNotDueToday()
    {
        var tasks = await ListAsync("?all=true");

        Assert.Equal(3, tasks.Count);
        Assert.Contains(tasks, task => task.Id == AutomatedTestSeed.FutureTaskId);
    }

    [Fact]
    public async Task TogglingATaskTicksItOffForToday()
    {
        var response = await Client.PostAsync(
            $"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.True((await response.ReadAsync<TaskDocument>()).IsDone);
    }

    [Fact]
    public async Task TogglingTwiceLeavesItOpenAgain()
    {
        await Client.PostAsync($"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle", null, TestContext.Current.CancellationToken);
        var second = await Client.PostAsync($"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle", null, TestContext.Current.CancellationToken);

        Assert.False((await second.ReadAsync<TaskDocument>()).IsDone);
    }

    [Fact]
    public async Task TickingATaskOffCountsTowardsTheStreak()
    {
        // The seed checks in for three days ending today, so the streak is
        // already three; ticking something off must not double-count it.
        var response = await Client.PostAsync(
            $"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var profile = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        Assert.Equal(3, profile.Streak);
    }

    [Fact]
    public async Task TickingATaskOffMovesTheDaysProgress()
    {
        var before = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        await Client.PostAsync($"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle", null, TestContext.Current.CancellationToken);

        var after = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        Assert.Equal(before.Today.Done + 1, after.Today.Done);
        Assert.Equal(before.Today.Total, after.Today.Total);
        Assert.Equal(100, after.Today.Percent);
    }

    [Fact]
    public async Task TickingOffPublishesToTheFeedAndUnTickingWithdrawsIt()
    {
        await Client.PostAsync($"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle", null, TestContext.Current.CancellationToken);

        await Factory.WithDatabaseAsync(async database =>
        {
            Assert.Contains(database.ActivityEvents, activity => activity.SourceId == AutomatedTestSeed.OpenTaskId);
            await Task.CompletedTask;
        });

        await Client.PostAsync($"/api/tasks/{AutomatedTestSeed.OpenTaskId}/toggle", null, TestContext.Current.CancellationToken);

        await Factory.WithDatabaseAsync(async database =>
        {
            // Exactly the entry that ticking it published, and no other.
            Assert.DoesNotContain(database.ActivityEvents, activity => activity.SourceId == AutomatedTestSeed.OpenTaskId);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TogglingATaskThatDoesNotExistIs404()
    {
        var response = await Client.PostAsync(
            $"/api/tasks/{Guid.CreateVersion7()}/toggle",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatingATaskPutsItAtTheTopOfTodaysList()
    {
        var response = await Client.PostJsonAsync("/api/tasks", new { title = "Frisch angelegt", rhythm = "Daily" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tasks = await ListAsync();
        Assert.Equal("Frisch angelegt", tasks[0].Title);
    }

    [Fact]
    public async Task AMeasurableTaskComesBackWithItsBarFilled()
    {
        var created = await (await Client.PostJsonAsync("/api/tasks", new
        {
            title = "2 L Wasser trinken",
            rhythm = "Daily",
            targetValue = 2,
            measureUnit = "L",
        })).ReadAsync<TaskDocument>();

        Assert.Equal(0, created.MeasurePercent);

        var toggled = await (await Client.PostAsync(
            $"/api/tasks/{created.Id}/toggle", null, TestContext.Current.CancellationToken)).ReadAsync<TaskDocument>();

        // Ticking off a measurable task means it reached its target.
        Assert.Equal(100, toggled.MeasurePercent);
        Assert.Equal(2, toggled.MeasuredValue);
    }

    [Fact]
    public async Task CreatingATaskWithoutATitleIsAValidationProblem()
    {
        var response = await Client.PostJsonAsync("/api/tasks", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Title", (await response.ReadAsync<ValidationDocument>()).Errors.Keys);
    }

    [Fact]
    public async Task CreatingATaskUnderAGoalThatDoesNotExistIs404()
    {
        var response = await Client.PostJsonAsync("/api/tasks", new
        {
            title = "Gehört zu nichts",
            goalId = Guid.CreateVersion7(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
