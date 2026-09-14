using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Every trigger, seen from the far end: who gets a line in the bell, whose
/// device rings, and who gets neither.
/// </summary>
/// <remarks>
/// One pipeline, three ways out (docs/adr/0024-one-notification-pipeline.md).
/// The rules each test here names are the ones that would be a disclosure or a
/// nuisance if they slipped — a verdict with a name on it, a pause's reason on a
/// lock screen, a blocked person still ringing somebody's phone.
/// </remarks>
[Trait("Category", "Integration")]
public class NotificationPipelineTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private const string UnconnectedEmail = "test.four@" + SeedAccounts.EmailDomain;

    private sealed record ActorDocument(Guid Id, string DisplayName);

    private sealed record LineDocument(
        Guid? Id,
        NotificationKind Kind,
        ActorDocument? Actor,
        string? Subject,
        string? Excerpt,
        int? Amount,
        NotificationTarget Target,
        Guid? TargetId,
        DateTimeOffset OccurredAt,
        bool IsNew);

    private sealed record CountsDocument(
        int UnreadChats,
        int PendingFriendRequests,
        int UnseenNotifications,
        int ProofsAwaitingVote);

    private sealed record ChatDocument(Guid Id, bool IsMuted);

    private sealed record CreatedGoal(Guid Id);

    /// <summary>The signed-in person's own line of history in the seeded feed.</summary>
    private static Guid MyOwnActivityId => SeedIds.For(SeedProfile.AutomatedTest, SeedEntity.Activity, 3);

    // -- the bell -----------------------------------------------------------

    [Fact]
    public async Task OpeningTheBellIsSeeingIt()
    {
        Assert.Equal(1, (await CountsAsync(Client)).UnseenNotifications);

        var line = Assert.Single(await BellAsync(Client));

        Assert.Equal(NotificationKind.ReactionReceived, line.Kind);
        Assert.Equal(AutomatedTestSeed.FriendPersonId, line.Actor?.Id);
        Assert.Equal(AutomatedTestSeed.ActiveGoalId, line.TargetId);
        Assert.True(line.IsNew);

        Assert.Equal(0, (await CountsAsync(Client)).UnseenNotifications);
        Assert.False(Assert.Single(await BellAsync(Client)).IsNew);
    }

    [Fact]
    public async Task KudosReachTheBellAndTakingThemBackTakesTheLine()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await PostAsync(friend, $"/api/feed/{MyOwnActivityId}/kudos");
        Assert.Equal(2, (await CountsAsync(Client)).UnseenNotifications);

        await PostAsync(friend, $"/api/feed/{MyOwnActivityId}/kudos");
        Assert.Equal(1, (await CountsAsync(Client)).UnseenNotifications);

        Assert.Equal(NotificationTarget.Goal, Assert.Single(await BellAsync(Client)).Target);
    }

    /// <summary>
    /// Hidden on read, not deleted, so lifting the block puts the history
    /// back — the choice a direct chat makes too.
    /// </summary>
    [Fact]
    public async Task ABlockHidesSomebodysLinesAndLiftingItBringsThemBack()
    {
        await PostAsync(Client, $"/api/blocks/{AutomatedTestSeed.FriendPersonId}");

        Assert.Equal(0, (await CountsAsync(Client)).UnseenNotifications);
        Assert.Empty(await BellAsync(Client));

        (await Client.DeleteAsync($"/api/blocks/{AutomatedTestSeed.FriendPersonId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        Assert.Single(await BellAsync(Client));
    }

    // -- messages -----------------------------------------------------------

    [Fact]
    public async Task AMessageRingsTheOthersAndNeverTheSender()
    {
        const string mine = "https://push.example/me";
        const string theirs = "https://push.example/friend";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await Client.SubscribeDeviceAsync(mine);
        await friend.SubscribeDeviceAsync(theirs);

        await SendAsync(Client, AutomatedTestSeed.DirectConversationId, "Automated test: ringing");

        var pushed = Assert.Single(PushedTo(theirs));

        Assert.Equal(NotificationKind.MessageReceived, pushed.Kind);
        Assert.Equal("Automated test: ringing", pushed.Excerpt);
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, pushed.Actor?.Id);
        Assert.Equal(NotificationTarget.Conversation, pushed.Target);
        Assert.Equal(AutomatedTestSeed.DirectConversationId, pushed.TargetId);

        Assert.Empty(PushedTo(mine));

        // The chat list is where a message lives; the bell does not copy it.
        Assert.DoesNotContain(await BellAsync(friend), line => line.Kind == NotificationKind.MessageReceived);
    }

    [Fact]
    public async Task AMutedConversationStillCountsButDoesNotRing()
    {
        const string theirs = "https://push.example/friend";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.SubscribeDeviceAsync(theirs);

        var muted = await (await friend.PutJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/mute",
            new { muted = true })).ReadAsync<ChatDocument>();

        Assert.True(muted.IsMuted);

        await SendAsync(Client, AutomatedTestSeed.DirectConversationId, "Automated test: quietly");

        Assert.Empty(PushedTo(theirs));
        Assert.Equal(1, (await CountsAsync(friend)).UnreadChats);

        (await friend.PutJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/mute", new { muted = false }))
            .EnsureSuccessStatusCode();

        await SendAsync(Client, AutomatedTestSeed.DirectConversationId, "Automated test: out loud");

        Assert.Single(PushedTo(theirs));
    }

    [Fact]
    public async Task InAGroupABlockedMembersMessagesDoNotRing()
    {
        const string theirs = "https://push.example/friend";

        var group = await (await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: group",
            icon = "sunrise",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
        })).ReadAsync<ChatDocument>();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.SubscribeDeviceAsync(theirs);
        await PostAsync(friend, $"/api/blocks/{AutomatedTestSeed.CurrentPersonId}");

        // The group is everybody's conversation and stays; the one who was
        // blocked simply does not reach the blocker's phone through it.
        await SendAsync(Client, group.Id, "Automated test: into the group");

        Assert.Empty(PushedTo(theirs));
    }

    // -- friendships ----------------------------------------------------------

    [Fact]
    public async Task ARequestRingsTheOneAskedAndTheAcceptanceLandsInTheAskersBell()
    {
        const string theirs = "https://push.example/four";

        var unconnected = await ClientForAsync(UnconnectedEmail);
        await unconnected.SubscribeDeviceAsync(theirs);

        await PostAsync(Client, $"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request");

        var pushed = Assert.Single(PushedTo(theirs));
        Assert.Equal(NotificationKind.FriendRequestReceived, pushed.Kind);
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, pushed.Actor?.Id);

        // The search screen is where a request lives, with its own count.
        Assert.Equal(1, (await CountsAsync(unconnected)).PendingFriendRequests);
        Assert.Empty(await BellAsync(unconnected));

        await PostAsync(unconnected, $"/api/friends/{AutomatedTestSeed.CurrentPersonId}/accept");

        var accepted = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.FriendshipStarted);
        Assert.Equal(AutomatedTestSeed.UnconnectedPersonId, accepted.Actor?.Id);
    }

    /// <summary>Whoever sent the link hears that it worked.</summary>
    [Fact]
    public async Task SomebodyArrivingThroughYourLinkIsNews()
    {
        var invite = await (await Client.GetAsync("/api/invite", TestContext.Current.CancellationToken))
            .ReadAsync<InviteDocument>();

        (await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Automated Newcomer",
            email = "newcomer@example.test",
            password = "a perfectly long password",
            inviteCode = invite.Code,
        })).EnsureSuccessStatusCode();

        var started = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.FriendshipStarted);
        Assert.Equal("Automated Newcomer", started.Actor?.DisplayName);
    }

    // -- photographs --------------------------------------------------------

    [Fact]
    public async Task ADeliveredPhotographRingsItsVotersAndCountsForThem()
    {
        const string theirs = "https://push.example/friend";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.SubscribeDeviceAsync(theirs);

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        Assert.Equal(ProofStatus.Voting, proof.Status);

        var pushed = Assert.Single(PushedTo(theirs));
        Assert.Equal(NotificationKind.ProofAwaitingVote, pushed.Kind);
        Assert.Equal("Automated test: shared active goal", pushed.Subject);

        // The banner on the start screen is where it lives.
        Assert.Equal(1, (await CountsAsync(friend)).ProofsAwaitingVote);
        Assert.Empty(await BellAsync(friend));
    }

    /// <summary>
    /// Doubt is anonymous, so a verdict never names whoever settled it — not
    /// even when it went their way.
    /// </summary>
    [Fact]
    public async Task TheOwnerHearsTheVerdictButNotWhoDecidedIt()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm }))
            .EnsureSuccessStatusCode();

        var verdict = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.ProofConfirmed);

        Assert.Null(verdict.Actor);
        Assert.Equal(AutomatedTestSeed.ActiveGoalId, verdict.TargetId);
        Assert.Equal(0, (await CountsAsync(friend)).ProofsAwaitingVote);
    }

    [Fact]
    public async Task AVerdictTheDeadlineReachedIsAnnouncedToo()
    {
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        SetClock(Q2ApiFactory.Now.AddHours(ProofVoting.VotingWindowHours).AddMinutes(1));
        await RunAsync<GoalMaintenanceWorker>(worker => worker.RunOnceAsync(TestContext.Current.CancellationToken));

        var verdict = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.ProofConfirmed);
        Assert.Null(verdict.Actor);
    }

    [Theory]
    [InlineData("/api/goals")]
    [InlineData("/api/goals/{id}")]
    [InlineData("/api/goals/{id}/pause")]
    public async Task OpeningOrPausingAGoalBeforeTheWorkerAnnouncesItsExpiredVerdictOnce(string path)
    {
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        SetClock(Q2ApiFactory.Now.AddHours(ProofVoting.VotingWindowHours).AddMinutes(1));
        var url = path.Replace("{id}", AutomatedTestSeed.ActiveGoalId.ToString(), StringComparison.Ordinal);

        if (path.EndsWith("/pause", StringComparison.Ordinal))
        {
            await PostJsonAsync(Client, url, new { reason = "Automated test: rest", days = 2 });
        }
        else
        {
            (await Client.GetAsync(url, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        var verdict = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.ProofConfirmed);
        Assert.Null(verdict.Actor);

        await RunAsync<GoalMaintenanceWorker>(worker => worker.RunOnceAsync(TestContext.Current.CancellationToken));
        Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.ProofConfirmed);
    }

    /// <summary>
    /// Told once per person: a change of mind between the three kinds is not
    /// a second reaction, and taking it back takes back an unseen line.
    /// </summary>
    [Fact]
    public async Task AReactionToAPhotographReachesItsOwnerOnce()
    {
        const string mine = "https://push.example/me";

        await Client.SubscribeDeviceAsync(mine);
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        // The seeded line is seen from here on, so what follows is new. "Seen"
        // is everything up to the moment the bell was read, and the test clock
        // stands still — so the reaction happens a minute later, as it would.
        await BellAsync(Client);
        SetClock(Q2ApiFactory.Now.AddMinutes(1));

        await PostJsonAsync(friend, $"/api/proofs/{proof.Id}/reactions", new { kind = "Fire" });
        await PostJsonAsync(friend, $"/api/proofs/{proof.Id}/reactions", new { kind = "Strong" });

        Assert.Equal(1, (await CountsAsync(Client)).UnseenNotifications);
        Assert.Single(PushedTo(mine), pushed => pushed.Kind == NotificationKind.ReactionReceived);

        await PostJsonAsync(friend, $"/api/proofs/{proof.Id}/reactions", new { kind = "Strong" });

        Assert.Equal(0, (await CountsAsync(Client)).UnseenNotifications);
    }

    // -- goals --------------------------------------------------------------

    [Fact]
    public async Task BeingPutOnAGoalAndItsPauseAreAnnouncedButNeverTheReason()
    {
        const string reason = "Automated test: a reason nobody else should read";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var goal = await CreateGoalAsync("Automated test: invited", AutomatedTestSeed.FriendPersonId);

        var invitation = Assert.Single(await BellAsync(friend), line => line.Kind == NotificationKind.GoalInvitation);
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, invitation.Actor?.Id);
        Assert.Equal("Automated test: invited", invitation.Subject);
        Assert.Equal(goal.Id, invitation.TargetId);

        await PostJsonAsync(Client, $"/api/goals/{goal.Id}/pause", new { reason, days = 3 });

        var json = await (await friend.GetAsync("/api/notifications", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(reason, json, StringComparison.Ordinal);

        var lines = JsonSerializer.Deserialize<List<LineDocument>>(json, TestJson.Options)!;
        Assert.Equal(3, Assert.Single(lines, line => line.Kind == NotificationKind.GoalPaused).Amount);
    }

    /// <summary>
    /// One objection is silent; the consequence of enough of them is not — and
    /// it names nobody.
    /// </summary>
    [Fact]
    public async Task ObjectionsThatLiftAPauseAreAnnouncedWithoutNames()
    {
        // Two people on the goal, because two objections are the least that
        // can lift a pause.
        var fourth = await ClientForAsync(UnconnectedEmail);
        await PostAsync(Client, $"/api/friends/{AutomatedTestSeed.UnconnectedPersonId}/request");
        await PostAsync(fourth, $"/api/friends/{AutomatedTestSeed.CurrentPersonId}/accept");

        var goal = await CreateGoalAsync(
            "Automated test: paused and lifted",
            AutomatedTestSeed.FriendPersonId,
            AutomatedTestSeed.UnconnectedPersonId);

        await PostJsonAsync(Client, $"/api/goals/{goal.Id}/pause", new { reason = "Automated test: away for a bit", days = 2 });

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await PostAsync(friend, $"/api/goals/{goal.Id}/pause/veto");

        Assert.DoesNotContain(await BellAsync(Client), line => line.Kind == NotificationKind.PauseLifted);

        await PostAsync(fourth, $"/api/goals/{goal.Id}/pause/veto");

        var lifted = Assert.Single(await BellAsync(Client), line => line.Kind == NotificationKind.PauseLifted);
        Assert.Null(lifted.Actor);
        Assert.Equal(goal.Id, lifted.TargetId);
    }

    /// <summary>A deleted goal leaves no sentence about itself behind, in the feed or in a bell.</summary>
    [Fact]
    public async Task DeletingAGoalTakesEveryLineAboutItWithIt()
    {
        await PostJsonAsync(Client, $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/close", new { completed = false });

        (await Client.DeleteAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        Assert.Empty(await BellAsync(Client));
    }

    // -- how long, and whose ------------------------------------------------

    [Fact]
    public async Task ALineOlderThanAMonthIsForgotten()
    {
        SetClock(Q2ApiFactory.Now.Add(Notification.KeptFor).AddDays(1));

        var removed = await RunAsync<NotificationRetentionWorker>(worker =>
            worker.RunOnceAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, removed);

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Notifications.AnyAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>Somebody who left is gone from other people's bells too.</summary>
    [Fact]
    public async Task DeletingAnAccountTakesItsNameOutOfOtherBells()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        (await friend.DeleteJsonAsync("/api/auth/account", new { password = SeedAccounts.Password }))
            .EnsureSuccessStatusCode();

        Assert.Empty(await BellAsync(Client));
    }

    // -- helpers ------------------------------------------------------------

    private sealed record InviteDocument(string Code);

    private static async Task<IReadOnlyList<LineDocument>> BellAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications", TestContext.Current.CancellationToken))
            .ReadAsync<List<LineDocument>>();

    private static async Task<CountsDocument> CountsAsync(HttpClient client) =>
        await (await client.GetAsync("/api/counts", TestContext.Current.CancellationToken))
            .ReadAsync<CountsDocument>();

    private static async Task SendAsync(HttpClient client, Guid conversationId, string text) =>
        (await client.PostJsonAsync($"/api/chats/{conversationId}/messages", new { text })).EnsureSuccessStatusCode();

    private static async Task PostAsync(HttpClient client, string url) =>
        (await client.PostAsync(url, content: null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

    private static async Task PostJsonAsync(HttpClient client, string url, object body) =>
        (await client.PostJsonAsync(url, body)).EnsureSuccessStatusCode();

    private async Task<CreatedGoal> CreateGoalAsync(string title, params Guid[] participants) =>
        await (await Client.PostJsonAsync("/api/goals", new
        {
            title,
            icon = "flame",
            schedule = new { kind = "Interval", everyDays = 1 },
            participantIds = participants,
        })).ReadAsync<CreatedGoal>();

    private IReadOnlyList<NotificationResponse> PushedTo(string endpoint) =>
        [.. Factory.PushSender.Sent.Where(message => message.Endpoint == endpoint).Select(message => message.Payload)];

    private void SetClock(DateTimeOffset now) =>
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(now);

    private async Task<T> RunAsync<TWorker, T>(Func<TWorker, Task<T>> run)
        where TWorker : notnull =>
        await run(ActivatorUtilities.CreateInstance<TWorker>(Factory.Services));

    private Task<int> RunAsync<TWorker>(Func<TWorker, Task<int>> run)
        where TWorker : notnull =>
        RunAsync<TWorker, int>(run);
}
