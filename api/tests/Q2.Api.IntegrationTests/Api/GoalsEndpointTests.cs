using System.Net;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The goal endpoints, over real HTTP against a real database.
/// </summary>
[Trait("Category", "Integration")]
public class GoalsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record GoalDocument(
        Guid Id,
        string Title,
        string? Description,
        string Icon,
        GoalRhythm Rhythm,
        GoalStatus Status,
        bool IsGroup,
        int CompletedSteps,
        int TotalSteps,
        int ProgressPercent,
        int Streak,
        TimeOnly? ReminderAt,
        DateOnly? TargetDate,
        DateTimeOffset CreatedAt,
        IReadOnlyList<PersonDocument> Participants,
        bool IsOverdue);

    private sealed record PersonDocument(
        Guid Id,
        string DisplayName,
        string Handle,
        string Initials,
        string AvatarColor,
        bool IsOnline);

    private sealed record TeamMemberDocument(PersonDocument Person, int Streak);

    private sealed record GoalDetailDocument(
        GoalDocument Goal,
        IReadOnlyList<TeamMemberDocument> Team,
        IReadOnlyList<TaskDocument> Tasks);

    private sealed record TaskDocument(Guid Id, Guid? GoalId, string Title, bool IsDone);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    [Fact]
    public async Task ListingReturnsTheSeededGoalsNewestFirst()
    {
        var response = await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var goals = await response.ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.Equal(3, goals.Count);
        Assert.Equal(goals.OrderByDescending(goal => goal.CreatedAt).Select(goal => goal.Id), goals.Select(goal => goal.Id));
    }

    [Fact]
    public async Task ProgressIsDerivedFromTheStepsRatherThanStored()
    {
        var goals = await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.All(
            goals,
            goal => Assert.Equal(
                (int)Math.Round(goal.CompletedSteps * 100d / goal.TotalSteps),
                goal.ProgressPercent));
    }

    [Fact]
    public async Task FilteringByStatusReturnsOnlyThatStatus()
    {
        var response = await Client.GetAsync("/api/goals?status=Completed", TestContext.Current.CancellationToken);

        var goals = await response.ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.All(goals, goal => Assert.Equal(GoalStatus.Completed, goal.Status));
        Assert.Single(goals);
    }

    [Fact]
    public async Task FilteringToAStatusNothingMatchesReturnsAnEmptyList()
    {
        var response = await Client.GetAsync("/api/goals?status=Archived", TestContext.Current.CancellationToken);

        Assert.Empty(await response.ReadAsync<IReadOnlyList<GoalDocument>>());
    }

    [Fact]
    public async Task AnUnknownStatusIsRejectedRatherThanIgnored()
    {
        var response = await Client.GetAsync("/api/goals?status=Nonsense", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GettingOneGoalReturnsItsTeamAndItsTasks()
    {
        var response = await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var detail = await response.ReadAsync<GoalDetailDocument>();

        Assert.Equal(AutomatedTestSeed.ActiveGoalId, detail.Goal.Id);
        Assert.Single(detail.Team);
        Assert.Equal("Test Person Two", detail.Team[0].Person.DisplayName);
        Assert.Contains(detail.Tasks, task => task.GoalId == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task ASharedGoalOnlyReturnsTheSignedInPersonsTasks()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var createdResponse = await friend.PostJsonAsync("/api/tasks", new
        {
            title = "Automated test: participant-only task",
            goalId = AutomatedTestSeed.ActiveGoalId,
        });
        createdResponse.EnsureSuccessStatusCode();
        var participantsTask = await createdResponse.ReadAsync<TaskDocument>();

        var ownersDetail = await (await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken)).ReadAsync<GoalDetailDocument>();

        Assert.Contains(ownersDetail.Tasks, task => task.Id == AutomatedTestSeed.OpenTaskId);
        Assert.DoesNotContain(ownersDetail.Tasks, task => task.Id == participantsTask.Id);

        var participantsDetail = await (await friend.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken)).ReadAsync<GoalDetailDocument>();

        Assert.Contains(participantsDetail.Tasks, task => task.Id == participantsTask.Id);
        Assert.DoesNotContain(participantsDetail.Tasks, task => task.Id == AutomatedTestSeed.OpenTaskId);
    }

    [Fact]
    public async Task ParticipantsComeBackInAStableOrder()
    {
        // The same goal must not produce two different avatar stacks depending
        // on which query loaded it.
        var first = await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();
        var second = await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        Assert.Equal(
            first.Goal.Participants.Select(person => person.Id),
            second.Goal.Participants.Select(person => person.Id));
    }

    [Fact]
    public async Task AGoalThatDoesNotExistIs404()
    {
        var response = await Client.GetAsync(
            $"/api/goals/{Guid.CreateVersion7()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreatingAGoalReturns201WithItsLocation()
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Neues Ziel",
            icon = "flame",
            rhythm = "Weekly",
            totalSteps = 12,
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadAsync<GoalDocument>();

        Assert.Equal($"/api/goals/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal("Neues Ziel", created.Title);
        Assert.Equal("flame", created.Icon);
        Assert.Equal(GoalRhythm.Weekly, created.Rhythm);
        Assert.Equal(0, created.ProgressPercent);
        Assert.Single(created.Participants);
    }

    [Fact]
    public async Task ACreatedGoalIsInTheListAfterwards()
    {
        await Client.PostJsonAsync("/api/goals", new { title = "Taucht in der Liste auf" });

        var goals = await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.Contains(goals, goal => goal.Title == "Taucht in der Liste auf");
    }

    [Fact]
    public async Task CreatingAGoalWithoutATitleIsAValidationProblem()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ValidationDocument>();
        Assert.Contains("Title", problem.Errors.Keys);
    }

    [Fact]
    public async Task CreatingAGoalWithAnUnknownIconIsAValidationProblem()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "Anything", icon = "rocket" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ValidationDocument>();
        Assert.Contains("Icon", problem.Errors.Keys);
    }

    [Fact]
    public async Task ContributingAddsAStep()
    {
        var before = await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        var response = await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/contribute",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var after = await response.ReadAsync<GoalDocument>();

        Assert.Equal(before.Goal.CompletedSteps + 1, after.CompletedSteps);
    }

    [Fact]
    public async Task ContributingToAGoalNobodyHasTouchedStartsAStreak()
    {
        var response = await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.GoalWithoutTargetDateId}/contribute",
            content: null,
            TestContext.Current.CancellationToken);

        var goal = await response.ReadAsync<GoalDocument>();

        Assert.Equal(1, goal.CompletedSteps);
        Assert.Equal(1, goal.Streak);
    }

    [Fact]
    public async Task ContributingTwiceInOneDayAddsTwoStepsButNotTwoDays()
    {
        var first = await (await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.GoalWithoutTargetDateId}/contribute", null, TestContext.Current.CancellationToken))
            .ReadAsync<GoalDocument>();

        var second = await (await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.GoalWithoutTargetDateId}/contribute", null, TestContext.Current.CancellationToken))
            .ReadAsync<GoalDocument>();

        Assert.Equal(2, second.CompletedSteps);

        // The streak counts days worked on, not taps.
        Assert.Equal(first.Streak, second.Streak);
    }

    [Fact]
    public async Task ContributingOnADayThatAlreadyCountsLeavesTheStreakWhereItIs()
    {
        // The seeded goal was already worked on today, so today is spoken for.
        var before = await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        var after = await (await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/contribute", null, TestContext.Current.CancellationToken))
            .ReadAsync<GoalDocument>();

        Assert.Equal(before.Goal.Streak, after.Streak);
    }

    [Fact]
    public async Task ContributingToACompletedGoalLeavesItAlone()
    {
        var response = await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.CompletedGoalId}/contribute",
            content: null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var goal = await response.ReadAsync<GoalDocument>();

        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.Equal(goal.TotalSteps, goal.CompletedSteps);
    }

    [Fact]
    public async Task ContributingToAGoalThatDoesNotExistIs404()
    {
        var response = await Client.PostAsync(
            $"/api/goals/{Guid.CreateVersion7()}/contribute",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContributingPublishesSomethingForFriendsToSee()
    {
        await Client.PostAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/contribute", null, TestContext.Current.CancellationToken);

        await Factory.WithDatabaseAsync(async database =>
        {
            var published = database.ActivityEvents
                .Where(activity => activity.SourceId == AutomatedTestSeed.ActiveGoalId)
                .ToList();

            Assert.Contains(published, activity => activity.Kind == Features.Activity.ActivityKind.GoalProgress);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task NothingAboutAPersonIsInventedByTheContract()
    {
        // Every avatar colour a client receives has to be one it can render
        // white text on.
        var goals = await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.All(
            goals.SelectMany(goal => goal.Participants),
            person => Assert.Contains(person.AvatarColor, AvatarColors.All));
    }
}
