using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The exits: setting a goal aside, stopping it, and deleting it for good.
/// </summary>
/// <remarks>
/// Over real HTTP against a real database, and from both ends where it matters
/// — an objection is something only somebody else can raise, and "only the
/// owner" is only proved by a client that is not them.
/// </remarks>
[Trait("Category", "Integration")]
public class PauseAndArchiveEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private const string Reason = "Grippe, seit Freitag im Bett.";

    private sealed record PauseDocument(
        Guid Id,
        string Reason,
        DateOnly StartsOn,
        DateOnly EndsOn,
        DateTimeOffset EndsAt,
        int Days,
        int VetoCount,
        int VetoesRequired,
        bool VetoedByMe,
        bool CanVeto);

    private sealed record WindowDocument(Guid Id, GoalInstanceStatus Status);

    private sealed record GoalDocument(
        Guid Id,
        string Title,
        GoalStatus Status,
        WindowDocument? Current,
        int Streak,
        int WindowsDone,
        int WindowsMissed,
        DateTimeOffset? ClosedAt,
        bool IsMine,
        PauseDocument? Pause,
        int? RemainingPauses);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    [Fact]
    public async Task SettingAGoalAsideSuspendsItsOpenWindow()
    {
        var response = await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var goal = await response.ReadAsync<GoalDocument>();

        Assert.NotNull(goal.Pause);
        Assert.Equal(Reason, goal.Pause.Reason);
        Assert.Equal(3, goal.Pause.Days);
        Assert.Equal(Q2ApiFactory.Today, goal.Pause.StartsOn);
        Assert.Equal(Q2ApiFactory.Today.AddDays(2), goal.Pause.EndsOn);

        // No open window while it is paused — which is what stops the deadline
        // running and what tells a screen to show the banner rather than a
        // camera.
        Assert.Null(goal.Current);
        Assert.Equal(1, goal.RemainingPauses);
    }

    /// <summary>
    /// The point of the feature: a set-aside window is in neither half of the
    /// balance and does not break the chain.
    /// </summary>
    [Fact]
    public async Task ASuspendedWindowIsNeitherDoneNorMissed()
    {
        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 2);

        var after = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(before.WindowsDone, after.WindowsDone);
        Assert.Equal(before.WindowsMissed, after.WindowsMissed);
        Assert.Equal(before.Streak, after.Streak);
    }

    /// <summary>
    /// The maintenance job runs from every path that touches a goal, including
    /// the one that delivers a photograph. If a pause were invisible to it, a
    /// window somebody is excused from would be failed by the very next
    /// request — so this is the test that would catch a query forgetting to
    /// load them.
    /// </summary>
    [Fact]
    public async Task APausedGoalTakesNoPhotographAndLosesNoWindow()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        var response = await Client.DeliverProofAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.NotNull(after.Pause);
        Assert.Null(after.Current);
        Assert.Equal(before.WindowsMissed, after.WindowsMissed);
        Assert.Equal(before.Streak, after.Streak);
    }

    [Fact]
    public async Task APauseNeedsAReasonSomebodyCanRead()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause",
            new { reason = "krank", days = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ValidationDocument>();

        Assert.True(problem.Errors.ContainsKey("Reason"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task APauseRunsForOneToSevenDays(int days)
    {
        var response = await PauseAsync(AutomatedTestSeed.ActiveGoalId, days);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheOwnerMaySetAGoalAside()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause",
            new { reason = Reason, days = 2 });

        // 404 rather than 403: how this fails must not say anything about
        // whose goal it is.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scarcity is what keeps a pause from becoming a skip button, so it is
    /// asserted over HTTP as well as in the domain.
    /// </summary>
    [Fact]
    public async Task AThirdPauseInTheSameMonthIsRefused()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 1);
        await EndPauseAsync(AutomatedTestSeed.ActiveGoalId);

        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 1);
        await EndPauseAsync(AutomatedTestSeed.ActiveGoalId);

        var third = await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 1);

        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
    }

    [Fact]
    public async Task AnInvitedFriendReadsTheReasonAndMayObject()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var seen = await OneAsync(AutomatedTestSeed.ActiveGoalId, friend);

        Assert.NotNull(seen.Pause);
        Assert.Equal(Reason, seen.Pause.Reason);
        Assert.True(seen.Pause.CanVeto);
        Assert.False(seen.Pause.VetoedByMe);

        // Nobody else's allowance is anybody's business.
        Assert.Null(seen.RemainingPauses);
        Assert.False(seen.IsMine);

        var response = await friend.PostAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause/veto",
            null,
            TestContext.Current.CancellationToken);

        var after = await response.ReadAsync<GoalDocument>();

        Assert.Equal(1, after.Pause!.VetoCount);
        Assert.True(after.Pause.VetoedByMe);
    }

    /// <summary>
    /// With one invited friend a pause cannot be challenged at all — the same
    /// gap the vote on a photograph has, and accepted for the same reason: a
    /// verdict between two people with no second opinion would be worse than
    /// none.
    /// </summary>
    [Fact]
    public async Task ASingleFriendCannotOverturnAPause()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await friend.PostAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause/veto",
            null,
            TestContext.Current.CancellationToken);

        var goal = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.NotNull(goal.Pause);
        Assert.Equal(2, goal.Pause.VetoesRequired);

        // The objection is a number to the person who was objected to, and it
        // stays one — no name reaches this response.
        Assert.Equal(1, goal.Pause.VetoCount);
        Assert.False(goal.Pause.CanVeto);
    }

    [Fact]
    public async Task TheOwnerCannotObjectToTheirOwnPause()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        var response = await Client.PostAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause/veto",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Ending early stops the pause covering any further days. The days it has
    /// already covered stay covered, today included — coming back early is a
    /// good thing, and handing somebody the deadline they were excused from
    /// would make it a punishment.
    /// </summary>
    [Fact]
    public async Task EndingAPauseEarlyReleasesTheGoalWithoutHandingBackTheDeadline()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 5);

        var response = await EndPauseAsync(AutomatedTestSeed.ActiveGoalId);
        var goal = await response.ReadAsync<GoalDocument>();

        Assert.Null(goal.Pause);
        Assert.Equal(GoalStatus.Active, goal.Status);

        // Today was set aside and stays that way; the next window is tomorrow's.
        Assert.Null(goal.Current);

        // And the allowance is not refunded either.
        Assert.Equal(1, goal.RemainingPauses);
    }

    [Fact]
    public async Task EndingAPauseThatIsNotRunningIsRefused()
    {
        var response = await EndPauseAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StoppingAGoalMovesItToTheArchiveWithoutSpoilingTheBalance()
    {
        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        var response = await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: true);
        var goal = await response.ReadAsync<GoalDocument>();

        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.NotNull(goal.ClosedAt);
        Assert.Null(goal.Current);

        // Stopping is not failing. Anything else and nobody would ever use it.
        Assert.Equal(before.WindowsMissed, goal.WindowsMissed);
        Assert.Equal(before.WindowsDone, goal.WindowsDone);

        var archive = await ArchiveAsync();

        Assert.Contains(archive, entry => entry.Id == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task AGoalThatWasGivenUpIsArchivedRatherThanCompleted()
    {
        var goal = await (await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: false))
            .ReadAsync<GoalDocument>();

        Assert.Equal(GoalStatus.Archived, goal.Status);
    }

    [Fact]
    public async Task AStoppedGoalCannotBeStoppedAgain()
    {
        await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: true);

        var response = await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheArchiveHoldsWhatHasStoppedAndNothingElse()
    {
        var archive = await ArchiveAsync();

        // The seeded one-off was delivered, which is the only way a goal
        // reaches Completed on its own.
        Assert.Contains(archive, entry => entry.Id == AutomatedTestSeed.CompletedGoalId);
        Assert.DoesNotContain(archive, entry => entry.Id == AutomatedTestSeed.ActiveGoalId);
    }

    /// <summary>
    /// Stopping and deleting are two decisions, in that order. A goal that is
    /// still running cannot be deleted at all.
    /// </summary>
    [Fact]
    public async Task ARunningGoalCannotBeDeleted()
    {
        var response = await Client.DeleteAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheOwnerMayDelete()
    {
        await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: true);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.DeleteAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The only way anything ever really goes: the goal, its windows, its
    /// photographs and the conversation about it, for everybody on it.
    /// </summary>
    [Fact]
    public async Task DeletingFromTheArchiveTakesTheConversationWithIt()
    {
        await CloseAsync(AutomatedTestSeed.ActiveGoalId, completed: true);

        var response = await Client.DeleteAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var gone = await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var chat = await friend.GetAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, chat.StatusCode);
    }

    /// <summary>
    /// The night the pause is really tested: nobody has the app open, the job
    /// runs anyway, and it has to leave the goal alone.
    /// </summary>
    /// <remarks>
    /// This is the test that catches a query which forgot to load the pauses.
    /// An unloaded collection reads as "no pause", and the job would then spend
    /// the night creating the excused days one after another and missing every
    /// one of them — precisely what the pause was granted to prevent.
    /// </remarks>
    [Fact]
    public async Task TheNightlyJobLeavesAPausedGoalAlone()
    {
        await PauseAsync(AutomatedTestSeed.ActiveGoalId, days: 3);

        var before = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        // Two days later, in the middle of the pause.
        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(2));

        var during = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.NotNull(during.Pause);
        Assert.Null(during.Current);
        Assert.Equal(before.WindowsMissed, during.WindowsMissed);
        Assert.Equal(before.WindowsDone, during.WindowsDone);

        // And the day after the last one it covered, the goal picks up again —
        // with a window for that day, not for the days it was excused from.
        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(3).AddHours(9));

        var after = await OneAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Null(after.Pause);
        Assert.NotNull(after.Current);
        Assert.Equal(before.WindowsMissed, after.WindowsMissed);
    }

    private async Task RunMaintenanceAtAsync(DateTimeOffset now)
    {
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(now);

        using var scope = Factory.Services.CreateScope();
        var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        // The clock is shared with every other test in the assembly, so it goes
        // back even if one of these failed before getting here.
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Reset();

        await base.DisposeAsync();
    }

    private Task<HttpResponseMessage> PauseAsync(Guid goalId, int days) =>
        Client.PostJsonAsync($"/api/goals/{goalId}/pause", new { reason = Reason, days });

    private Task<HttpResponseMessage> EndPauseAsync(Guid goalId) =>
        Client.DeleteAsync($"/api/goals/{goalId}/pause", TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> CloseAsync(Guid goalId, bool completed) =>
        Client.PostJsonAsync($"/api/goals/{goalId}/close", new { completed });

    private async Task<IReadOnlyList<GoalDocument>> ArchiveAsync() =>
        await (await Client.GetAsync("/api/goals/archive", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GoalDocument>>();

    private async Task<GoalDocument> OneAsync(Guid goalId, HttpClient? client = null) =>
        (await (await (client ?? Client).GetAsync(
            $"/api/goals/{goalId}",
            TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal;
}
