using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Spot checks through the real pipeline (docs/adr/0038-spot-checks.md): drawn
/// in secret, asked with an interruption of their own, answered live, voted on
/// by friends — and missed when nobody answers.
/// </summary>
/// <remarks>
/// The rules of the moment and of the three minutes are <c>SpotCheckTests</c>.
/// What is worth the pipeline here is what leaves the server and when: nothing
/// before the moment, a push to the owner at it, and a photograph stored as
/// live whatever the client claims.
/// </remarks>
[Trait("Category", "Integration")]
public class SpotChecksEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record OpenCheckDocument(Guid Id, DateTimeOffset ExpiresAt);

    private sealed record WindowDocument(GoalInstanceStatus Status);

    private sealed record GoalDocument(Guid Id, WindowDocument? Current, OpenCheckDocument? SpotCheck);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    private async Task<GoalDocument> CreateSpotCheckDontAsync()
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Nicht rauchen",
            kind = "Dont",
            limit = 0,
            proofMode = "SpotCheck",
            schedule = new { kind = "Limit", period = "Day" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<GoalDocument>();
    }

    private static async Task<GoalDocument> GoalAsync(HttpClient client, Guid id) =>
        (await (await client.GetAsync($"/api/goals/{id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal;

    private async Task<int> RunWorkerAsync() =>
        await ActivatorUtilities.CreateInstance<SpotCheckWorker>(Factory.Services)
            .RunOnceAsync(TestContext.Current.CancellationToken);

    private async Task<SpotCheck> TodaysCheckAsync(Guid goalId)
    {
        SpotCheck? check = null;

        await Factory.WithDatabaseAsync(async database =>
            check = await database.Set<SpotCheck>()
                .AsNoTracking()
                .SingleAsync(
                    candidate => database.GoalInstances.Any(instance => instance.Id == candidate.GoalInstanceId && instance.GoalId == goalId),
                    TestContext.Current.CancellationToken));

        return check!;
    }

    private async Task AskNowAsync(Guid goalId) =>
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/api/diagnostics/spot-check/{goalId}", content: null, TestContext.Current.CancellationToken)).StatusCode);

    private async Task<HttpResponseMessage> AnswerAsync(Guid goalId, Guid checkId) =>
        await Client.PostJsonAsync(
            $"/api/goals/{goalId}/spot-checks/{checkId}/proof",
            new { imageId = await Client.UploadProofImageAsync() });

    [Fact]
    public async Task ACheckIsDrawnInSecretAndAskedWithAnInterruptionAtItsMoment()
    {
        const string device = "https://push.example/owner";
        await Client.SubscribeDeviceAsync(device);

        var created = await CreateSpotCheckDontAsync();
        await RunWorkerAsync();

        // Drawn, and announced to nobody: not in the goal, not as a push.
        var check = await TodaysCheckAsync(created.Id);
        Assert.Equal(SpotCheckStatus.Scheduled, check.Status);
        Assert.Null((await GoalAsync(Client, created.Id)).SpotCheck);
        Assert.DoesNotContain(Factory.PushSender.Sent, message => message.Endpoint == device);

        Clock.Set(check.ScheduledAt.AddSeconds(10));
        await RunWorkerAsync();

        var pushed = Assert.Single(Factory.PushSender.Sent, message => message.Endpoint == device);
        Assert.Equal(NotificationKind.SpotCheckDue, pushed.Payload.Kind);
        Assert.Equal(created.Id, pushed.Payload.TargetId);

        var open = (await GoalAsync(Client, created.Id)).SpotCheck;
        Assert.NotNull(open);
        Assert.Equal(check.ScheduledAt.AddSeconds(10).AddMinutes(3), open.ExpiresAt);

        // The friend who will vote on it is not shown the open check.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        Assert.Null((await GoalAsync(friend, created.Id)).SpotCheck);

        // Asked once: the next pass rings nobody.
        Clock.Set(check.ScheduledAt.AddSeconds(40));
        await RunWorkerAsync();
        Assert.Single(Factory.PushSender.Sent, message => message.Endpoint == device);
    }

    [Fact]
    public async Task ACheckIsAnsweredLiveAndGoesToItsFriendsVote()
    {
        var created = await CreateSpotCheckDontAsync();
        await AskNowAsync(created.Id);

        var open = (await GoalAsync(Client, created.Id)).SpotCheck!;
        var response = await AnswerAsync(created.Id, open.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var delivered = await response.ReadAsync<DeliveredProofDocument>();
        Assert.Equal(ProofSource.Live, delivered.Proof.Source);
        Assert.Equal(ProofStatus.Voting, delivered.Proof.Status);

        // A check is its own last attempt: refused means refused.
        Assert.Equal(0, delivered.Proof.AttemptsLeft);

        // Answered, so not open any more, and the window is still the limit's.
        var after = await GoalAsync(Client, created.Id);
        Assert.Null(after.SpotCheck);
        Assert.Equal(GoalInstanceStatus.Open, after.Current!.Status);
    }

    [Fact]
    public async Task ACheckNobodyAnsweredInThreeMinutesMissesTheWindow()
    {
        var created = await CreateSpotCheckDontAsync();
        await AskNowAsync(created.Id);
        var open = (await GoalAsync(Client, created.Id)).SpotCheck!;

        Clock.Set(open.ExpiresAt.AddSeconds(1));

        Assert.Equal(HttpStatusCode.BadRequest, (await AnswerAsync(created.Id, open.Id)).StatusCode);

        await RunWorkerAsync();

        Assert.Equal(SpotCheckStatus.Missed, (await TodaysCheckAsync(created.Id)).Status);

        await Factory.WithDatabaseAsync(async database =>
            Assert.Equal(
                GoalInstanceStatus.Missed,
                (await database.GoalInstances.SingleAsync(instance => instance.GoalId == created.Id, TestContext.Current.CancellationToken)).Status));
    }

    [Fact]
    public async Task SomebodyWhoTurnedSpotChecksOffIsNotInterrupted()
    {
        const string device = "https://push.example/owner-quiet";
        await Client.SubscribeDeviceAsync(device);

        (await Client.PutJsonAsync("/api/settings", new { notifications = new { spotChecks = false } })).EnsureSuccessStatusCode();

        var created = await CreateSpotCheckDontAsync();
        await AskNowAsync(created.Id);

        Assert.DoesNotContain(Factory.PushSender.Sent, message => message.Endpoint == device);

        // Still asked: the switch is about being interrupted, not about the check.
        Assert.NotNull((await GoalAsync(Client, created.Id)).SpotCheck);
    }

    [Fact]
    public async Task OnlyASpotCheckDontCanBeAskedAndOnlyByItsOwner()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var created = await CreateSpotCheckDontAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await friend.PostAsync($"/api/diagnostics/spot-check/{created.Id}", content: null, TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Client.PostAsync($"/api/diagnostics/spot-check/{AutomatedTestSeed.ActiveGoalId}", content: null, TestContext.Current.CancellationToken)).StatusCode);
    }
}
