using System.Net;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The goal endpoints, over real HTTP against a real database.
/// </summary>
[Trait("Category", "Integration")]
public class GoalsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ScheduleDocument(
        ScheduleKind Kind,
        int? EveryDays,
        IReadOnlyList<Weekday> Weekdays,
        int? Times,
        QuotaPeriod? Period);

    private sealed record WindowDocument(
        Guid Id,
        DateOnly StartsOn,
        DateOnly DueOn,
        DateTimeOffset DueAt,
        int RequiredProofs,
        int ConfirmedProofs,
        int RemainingProofs,
        GoalInstanceStatus Status,
        Guid? PendingProofId,
        bool AcceptsProof);

    private sealed record GoalDocument(
        Guid Id,
        string Title,
        string? Description,
        string Icon,
        ScheduleDocument Schedule,
        GoalStatus Status,
        bool IsGroup,
        WindowDocument? Current,
        int Streak,
        int WindowsDone,
        int WindowsMissed,
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
        IReadOnlyList<WindowDocument> History);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    [Fact]
    public async Task ListingReturnsTheSeededGoalsNewestFirst()
    {
        var response = await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var goals = await response.ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.Equal(4, goals.Count);
        Assert.Equal(
            goals.OrderByDescending(goal => goal.CreatedAt).Select(goal => goal.Id),
            goals.Select(goal => goal.Id));
    }

    /// <summary>
    /// The schedule survives the round trip as the shape it was created in —
    /// which is the whole reason it stopped being an enum.
    /// </summary>
    [Fact]
    public async Task AQuotaScheduleComesBackWithItsCountAndItsPeriod()
    {
        var goal = await OneAsync(AutomatedTestSeed.QuotaGoalId);

        Assert.Equal(ScheduleKind.Times, goal.Schedule.Kind);
        Assert.Equal(3, goal.Schedule.Times);
        Assert.Equal(QuotaPeriod.Week, goal.Schedule.Period);
    }

    /// <summary>
    /// "Noch 2 von 3" — the sentence the whole stage exists to be able to say.
    /// </summary>
    [Fact]
    public async Task AQuotaWindowSaysHowMuchIsLeftOfIt()
    {
        var goal = await OneAsync(AutomatedTestSeed.QuotaGoalId);

        Assert.NotNull(goal.Current);
        Assert.Equal(3, goal.Current.RequiredProofs);
        Assert.Equal(1, goal.Current.ConfirmedProofs);
        Assert.Equal(2, goal.Current.RemainingProofs);
    }

    [Fact]
    public async Task WhatIsLeftIsDerivedRatherThanSentTwice()
    {
        var goals = await ListAsync();

        Assert.All(
            goals.Where(goal => goal.Current is not null),
            goal => Assert.Equal(
                Math.Max(0, goal.Current!.RequiredProofs - goal.Current.ConfirmedProofs),
                goal.Current.RemainingProofs));
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
    public async Task GettingOneGoalReturnsItsTeamAndItsResolvedWindows()
    {
        var response = await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var detail = await response.ReadAsync<GoalDetailDocument>();

        Assert.Equal(AutomatedTestSeed.ActiveGoalId, detail.Goal.Id);
        Assert.Single(detail.Team);
        Assert.Equal("Test Person Two", detail.Team[0].Person.DisplayName);

        // Two delivered days behind it, and the open one is not in the history.
        Assert.Equal(2, detail.History.Count);
        Assert.All(detail.History, window => Assert.NotEqual(GoalInstanceStatus.Open, window.Status));
        Assert.DoesNotContain(detail.History, window => window.Id == detail.Goal.Current!.Id);
    }

    [Fact]
    public async Task TheHistoryComesBackNewestFirst()
    {
        var detail = await (await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken)).ReadAsync<GoalDetailDocument>();

        Assert.Equal(
            detail.History.OrderByDescending(window => window.DueOn).Select(window => window.Id),
            detail.History.Select(window => window.Id));
    }

    [Fact]
    public async Task ParticipantsComeBackInAStableOrder()
    {
        // The same goal must not produce two different avatar stacks depending
        // on which query loaded it.
        var first = await OneAsync(AutomatedTestSeed.ActiveGoalId);
        var second = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(
            first.Participants.Select(person => person.Id),
            second.Participants.Select(person => person.Id));
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
            schedule = new { kind = "Times", times = 3, period = "Week" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadAsync<GoalDocument>();

        Assert.Equal($"/api/goals/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal("Neues Ziel", created.Title);
        Assert.Equal("flame", created.Icon);
        Assert.Equal(ScheduleKind.Times, created.Schedule.Kind);
        Assert.Single(created.Participants);
    }

    /// <summary>
    /// The stage's headline: "dreimal die Woche" can be created, and it comes
    /// back with a window that asks for three.
    /// </summary>
    [Fact]
    public async Task AGoalCreatedThreeTimesAWeekOpensAWindowThatWantsThree()
    {
        var created = await (await Client.PostJsonAsync("/api/goals", new
        {
            title = "Dreimal die Woche",
            schedule = new { kind = "Times", times = 3, period = "Week" },
        })).ReadAsync<GoalDocument>();

        Assert.NotNull(created.Current);
        Assert.Equal(3, created.Current.RequiredProofs);
        Assert.Equal(0, created.Current.ConfirmedProofs);

        // The window is the calendar week, so it starts on a Monday.
        Assert.Equal(DayOfWeek.Monday, created.Current.StartsOn.DayOfWeek);
        Assert.Equal(created.Current.StartsOn.AddDays(6), created.Current.DueOn);
    }

    [Fact]
    public async Task AGoalCreatedOnNamedWeekdaysWaitsForOneOfThem()
    {
        var created = await (await Client.PostJsonAsync("/api/goals", new
        {
            title = "Montags und donnerstags",
            schedule = new { kind = "Weekdays", weekdays = new[] { "Monday", "Thursday" } },
        })).ReadAsync<GoalDocument>();

        Assert.Equal([Weekday.Monday, Weekday.Thursday], created.Schedule.Weekdays);
        Assert.NotNull(created.Current);
        Assert.Contains(created.Current.DueOn.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Thursday });
    }

    [Fact]
    public async Task SayingNothingAboutTheScheduleMeansEveryDay()
    {
        var created = await (await Client.PostJsonAsync("/api/goals", new { title = "Ohne Angabe" }))
            .ReadAsync<GoalDocument>();

        Assert.Equal(ScheduleKind.Interval, created.Schedule.Kind);
        Assert.Equal(1, created.Schedule.EveryDays);
        Assert.Equal(created.Current!.StartsOn, created.Current.DueOn);
    }

    [Fact]
    public async Task ACreatedGoalIsInTheListAfterwards()
    {
        await Client.PostJsonAsync("/api/goals", new { title = "Taucht in der Liste auf" });

        var goals = await ListAsync();

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
    public async Task AScheduleThatCannotMeanAnythingIsAValidationProblem()
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Anything",
            schedule = new { kind = "Weekdays", weekdays = Array.Empty<string>() },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ValidationDocument>();
        Assert.Contains("Schedule", problem.Errors.Keys);
    }

    /// <summary>
    /// A goal nobody shares has nobody to ask, so its photograph is believed
    /// the moment it arrives — the self-report q2 has always had, kept for the
    /// one case where a vote would be ceremony.
    /// </summary>
    [Fact]
    public async Task DeliveringAProofFillsTheOpenWindowOfASoloGoal()
    {
        var before = await OneAsync(AutomatedTestSeed.QuotaGoalId);

        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.QuotaGoalId);

        var after = await OneAsync(AutomatedTestSeed.QuotaGoalId);

        Assert.Equal(before.Current!.ConfirmedProofs + 1, after.Current!.ConfirmedProofs);
        Assert.Equal(before.Current.Id, after.Current.Id);
    }

    /// <summary>
    /// The change stage 4 is for: on a shared goal the window does **not** move
    /// when the photograph arrives. It moves when somebody believes it.
    /// </summary>
    [Fact]
    public async Task AProofOnASharedGoalWaitsForItsFriends()
    {
        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(ProofStatus.Voting, proof.Status);

        var after = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(before.Streak, after.Streak);
        Assert.Equal(before.WindowsDone, after.WindowsDone);
        Assert.Equal(proof.Id, after.Current!.PendingProofId);

        // And the camera is not offered again while one is being looked at.
        Assert.False(after.Current.AcceptsProof);
    }

    [Fact]
    public async Task TheWindowClosesAndTheStreakGrowsWhenTheProofIsBelieved()
    {
        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var voted = await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm });
        voted.EnsureSuccessStatusCode();

        var after = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        // A daily window takes one proof, so it closes on the first believed one.
        Assert.Equal(before.Streak + 1, after.Streak);
        Assert.Equal(before.WindowsDone + 1, after.WindowsDone);
        Assert.Null(after.Current);
    }

    [Fact]
    public async Task AProofOnAWindowThatIsAlreadyFullIsRefused()
    {
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.QuotaGoalId);
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.QuotaGoalId);

        // "Three times a week" with one already in the seed: the third fills it,
        // and the fourth has nowhere to go.
        var response = await Client.DeliverProofAsync(AutomatedTestSeed.QuotaGoalId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The commitment is one person's. Everybody on a shared goal can see it
    /// and vote on it; somebody else delivering for them is not help.
    /// </summary>
    [Fact]
    public async Task OnlyTheOwnerDeliversAProof()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.DeliverProofAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeliveringAProofOnAGoalThatDoesNotExistIs404()
    {
        var response = await Client.DeliverProofAsync(Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeliveringAProofPublishesSomethingForFriendsToSee()
    {
        // A goal nobody shares is believed at once, so the feed hears about it
        // in the same request.
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.QuotaGoalId);

        await Factory.WithDatabaseAsync(async database =>
        {
            var published = database.ActivityEvents
                .Where(activity => activity.SourceId == AutomatedTestSeed.QuotaGoalId)
                .ToList();

            Assert.Contains(published, activity => activity.Kind == Features.Activity.ActivityKind.TaskCompleted);
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// "What is on my plate" is a window that covers today, not one that is due
    /// today: three runs by Sunday is something you can do on Tuesday.
    /// </summary>
    [Fact]
    public async Task TodayListsTheGoalsWhoseWindowCoversIt()
    {
        var response = await Client.GetAsync("/api/today", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var due = await response.ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.NotEmpty(due);
        Assert.Contains(due, goal => goal.Id == AutomatedTestSeed.QuotaGoalId);
        Assert.All(due, goal => Assert.NotNull(goal.Current));
    }

    [Fact]
    public async Task TodayDropsAGoalOnceItsWindowIsDelivered()
    {
        // Delivered means believed. The photograph alone leaves the goal on
        // today's list, which is the honest answer: nothing has been settled.
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var stillDue = await (await Client.GetAsync("/api/today", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.Contains(stillDue, goal => goal.Id == AutomatedTestSeed.ActiveGoalId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm });

        var due = await (await Client.GetAsync("/api/today", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

        Assert.DoesNotContain(due, goal => goal.Id == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task NothingAboutAPersonIsInventedByTheContract()
    {
        // Every avatar colour a client receives has to be one it can render
        // white text on.
        var goals = await ListAsync();

        Assert.All(
            goals.SelectMany(goal => goal.Participants),
            person => Assert.Contains(person.AvatarColor, AvatarColors.All));
    }

    private async Task<IReadOnlyList<GoalDocument>> ListAsync() =>
        await (await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

    private async Task<GoalDocument> OneAsync(Guid id) =>
        (await (await Client.GetAsync($"/api/goals/{id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal;
}
