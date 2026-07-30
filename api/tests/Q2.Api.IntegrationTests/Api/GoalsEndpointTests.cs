using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The goal endpoints, exercised through the real HTTP pipeline against a
/// migrated and seeded SQLite in-memory database.
/// </summary>
[Trait("Category", "Integration")]
public class GoalsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ListReturnsTheSeededGoalsNewestFirst()
    {
        var response = await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var goals = await response.ReadAsync<List<GoalResponse>>();

        Assert.Equal(3, goals.Count);
        Assert.Equal(
            goals.Select(g => g.CreatedAt).OrderByDescending(c => c),
            goals.Select(g => g.CreatedAt));
    }

    [Fact]
    public async Task ListIncludesParticipantsAndDerivedFields()
    {
        var goals = await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<List<GoalResponse>>();

        var shared = goals.Single(g => g.Id == AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(["Test Participant One", "Test Participant Two"], shared.Participants);
        Assert.Equal(GoalStatus.Active, shared.Status);
        Assert.Equal(40, shared.ProgressPercent);
        Assert.False(shared.IsOverdue);
    }

    [Fact]
    public async Task ListCanBeFilteredByStatus()
    {
        var completed = await (await Client.GetAsync("/api/goals?status=Completed", TestContext.Current.CancellationToken))
            .ReadAsync<List<GoalResponse>>();

        Assert.Single(completed);
        Assert.Equal(AutomatedTestSeed.CompletedGoalId, completed[0].Id);
    }

    [Fact]
    public async Task FilteringByAStatusWithNoMatchesReturnsAnEmptyList()
    {
        // Backs the frontend's empty state with a real, reachable response.
        var archived = await (await Client.GetAsync("/api/goals?status=Archived", TestContext.Current.CancellationToken))
            .ReadAsync<List<GoalResponse>>();

        Assert.Empty(archived);
    }

    [Fact]
    public async Task GetByIdReturnsTheGoal()
    {
        var response = await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.GoalWithoutTargetDateId}",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var goal = await response.ReadAsync<GoalResponse>();

        Assert.Equal("Automated test: goal without target date", goal.Title);
        Assert.Null(goal.TargetDate);
        Assert.Equal(0, goal.ProgressPercent);
    }

    [Fact]
    public async Task GetByIdReturnsAProblemDetails404ForAnUnknownGoal()
    {
        var response = await Client.GetAsync(
            "/api/goals/11111111-1111-4111-8111-111111111111",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadAsync<ProblemDocument>();
        Assert.Equal(404, problem.Status);
        Assert.Equal("Not found", problem.Title);
    }

    [Fact]
    public async Task GetByIdRejectsAMalformedIdWithoutReachingTheHandler()
    {
        var response = await Client.GetAsync("/api/goals/not-a-guid", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateStoresTheGoalAndReturnsItWithALocation()
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "  Swim once a week  ",
            description = "  Starting slowly.  ",
            progressPercent = 15,
            targetDate = "2026-09-30",
            participants = new[] { "Robin Sample", "Kim Example" },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/goals/{SequentialTestIdGenerator.IdAt(1)}", response.Headers.Location?.OriginalString);

        var created = await response.ReadAsync<GoalResponse>();
        Assert.Equal("Swim once a week", created.Title);
        Assert.Equal("Starting slowly.", created.Description);
        Assert.Equal(15, created.ProgressPercent);
        Assert.Equal(new DateOnly(2026, 9, 30), created.TargetDate);
        Assert.Equal(Q2ApiFactory.Now, created.CreatedAt);
        Assert.Equal(GoalStatus.Active, created.Status);

        // Persisted, not just echoed back.
        await Factory.WithDatabaseAsync(async database =>
        {
            var stored = await database.Goals
                .Include(g => g.Participants)
                .SingleAsync(g => g.Id == created.Id, TestContext.Current.CancellationToken);

            Assert.Equal("Swim once a week", stored.Title);
            Assert.Equal(2, stored.Participants.Count);
        });
    }

    [Fact]
    public async Task ParticipantOrderIsTheSameWhicheverWayTheGoalIsRead()
    {
        // Regression guard, found by hand: the create response used to return
        // the order the caller typed while later reads returned the database's
        // order, so the same goal had two different participant orders and the
        // card summary changed on reload.
        var created = await (await Client.PostJsonAsync("/api/goals", new
        {
            title = "Order check",
            participants = new[] { "Zoe Zulu", "Anna Alpha", "Mike Mid" },
        })).ReadAsync<GoalResponse>();

        var fetched = await (await Client.GetAsync($"/api/goals/{created.Id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalResponse>();

        var listed = (await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<List<GoalResponse>>()).Single(g => g.Id == created.Id);

        Assert.Equal(["Anna Alpha", "Mike Mid", "Zoe Zulu"], created.Participants);
        Assert.Equal(created.Participants, fetched.Participants);
        Assert.Equal(created.Participants, listed.Participants);
    }

    [Fact]
    public async Task CreateWithFullProgressCompletesTheGoalImmediately()
    {
        var created = await (await Client.PostJsonAsync("/api/goals", new
        {
            title = "Finish the reading list",
            progressPercent = 100,
        })).ReadAsync<GoalResponse>();

        Assert.Equal(GoalStatus.Completed, created.Status);
    }

    [Fact]
    public async Task CreateRejectsAMissingTitleWithFieldLevelErrors()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "   ", progressPercent = 150 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadAsync<ValidationProblemDocument>();

        Assert.Contains("Title", problem.Errors.Keys);
        Assert.Contains("ProgressPercent", problem.Errors.Keys);
        Assert.NotNull(problem.TraceId);
    }

    [Fact]
    public async Task CreateRejectsMalformedJsonWithA400RatherThanA500()
    {
        var response = await Client.PostAsync(
            "/api/goals",
            new StringContent("{ this is not json", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheHealthEndpointReportsDatabaseConnectivity()
    {
        var response = await Client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<HealthDocument>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        Assert.Equal("healthy", body?.Status);
    }

    private sealed record ProblemDocument(string? Title, int? Status, string? Detail, string? TraceId);

    private sealed record ValidationProblemDocument(
        string? Title,
        int? Status,
        string? TraceId,
        Dictionary<string, string[]> Errors);

    private sealed record HealthDocument(string Status, string Service);
}
