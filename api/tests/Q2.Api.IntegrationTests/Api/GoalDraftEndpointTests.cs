using System.Net;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The Do somebody without friends can leave waiting, and the moment it starts
/// (docs/adr/0039-first-run-onboarding.md).
/// </summary>
/// <remarks>
/// What is worth a pipeline is the second half: that every way a friendship
/// comes about — an invite link at registration, one followed by an existing
/// account, a request accepted — turns the waiting Do into a goal with the
/// friend on it, its first window open and its conversation there, in the same
/// save as the friendship.
/// </remarks>
[Trait("Category", "Integration")]
public class GoalDraftEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record SessionDocument(SessionPerson Person, bool IsOnboarded);

    private sealed record SessionPerson(Guid Id, string Handle);

    private sealed record DraftDocument(Guid Id, string Title, string Category, string Icon, ScheduleDocument Schedule, string? ReminderAt);

    private sealed record ScheduleDocument(string Kind, int? EveryDays, int? Times, string? Period);

    private sealed record DraftStateDocument(DraftDocument? Draft, bool HasFriends);

    private sealed record GoalDocument(Guid Id, string Title, bool IsMine, IReadOnlyList<ParticipantDocument> Participants);

    private sealed record ParticipantDocument(Guid Id);

    private sealed record InviteDocument(string Code);

    private sealed record ProblemDocument(Dictionary<string, string[]> Errors);

    private static readonly object Reading = new
    {
        title = "Jeden Abend lesen",
        category = "Reading",
        schedule = new { kind = "Times", times = 3, period = "Week" },
        reminderAt = "19:30:00",
    };

    private async Task<(SessionPerson Person, HttpClient Client)> RegisterAsync(
        string name,
        string email,
        string? inviteCode = null)
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/auth/register",
            new { name, email, password = SeedAccounts.Password, inviteCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return ((await response.ReadAsync<SessionDocument>()).Person, await ClientForAsync(email));
    }

    private static async Task<DraftStateDocument> StateOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/goals/draft", TestContext.Current.CancellationToken))
            .ReadAsync<DraftStateDocument>();

    private static async Task<DraftDocument?> DraftOfAsync(HttpClient client) => (await StateOfAsync(client)).Draft;

    private static async Task<List<GoalDocument>> GoalsOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/goals", TestContext.Current.CancellationToken))
            .ReadAsync<List<GoalDocument>>();

    [Fact]
    public async Task HavingNoneIsTheOrdinaryAnswer()
    {
        var state = await StateOfAsync(Client);

        Assert.Null(state.Draft);
        Assert.True(state.HasFriends);
    }

    [Fact]
    public async Task ANewAccountIsToldItHasNobodyYet()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        Assert.Equal(new DraftStateDocument(null, false), await StateOfAsync(client));
    }

    [Fact]
    public async Task SomebodyWithoutFriendsCanLeaveADoWaiting()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var saved = await client.PutJsonAsync("/api/goals/draft", Reading);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var draft = await DraftOfAsync(client);

        Assert.NotNull(draft);
        Assert.Equal("Jeden Abend lesen", draft.Title);
        Assert.Equal("Reading", draft.Category);
        Assert.Equal("book-open", draft.Icon);
        Assert.Equal(new ScheduleDocument("Times", null, 3, "Week"), draft.Schedule);
        Assert.Equal("19:30:00", draft.ReminderAt);

        // Not a goal: nothing to deliver into, nothing in the chat list.
        Assert.Empty(await GoalsOfAsync(client));
    }

    [Fact]
    public async Task WritingItAgainChangesTheSameOne()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var first = await (await client.PutJsonAsync("/api/goals/draft", Reading)).ReadAsync<DraftDocument>();
        var second = await (await client.PutJsonAsync("/api/goals/draft", new { title = "Laufen" })).ReadAsync<DraftDocument>();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Laufen", second.Title);

        // Nothing said about the schedule is every day, as for a goal.
        Assert.Equal(new ScheduleDocument("Interval", 1, null, null), second.Schedule);
    }

    [Fact]
    public async Task ItCanBeThrownAway()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");
        await client.PutJsonAsync("/api/goals/draft", Reading);

        var discarded = await client.DeleteAsync("/api/goals/draft", TestContext.Current.CancellationToken);
        var again = await client.DeleteAsync("/api/goals/draft", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Null(await DraftOfAsync(client));
    }

    [Fact]
    public async Task AOneOffCannotWait()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var response = await client.PutJsonAsync("/api/goals/draft", new { title = "Umzug", schedule = new { kind = "Once" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Schedule", (await response.ReadAsync<ProblemDocument>()).Errors.Keys);
    }

    [Fact]
    public async Task ATitleIsRequired()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");

        var response = await client.PutJsonAsync("/api/goals/draft", new { title = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Title", (await response.ReadAsync<ProblemDocument>()).Errors.Keys);
    }

    [Fact]
    public async Task SomebodyWithAFriendMakesTheGoalInstead()
    {
        // test.one has friends: there is somebody to make it in front of.
        var response = await Client.PutJsonAsync("/api/goals/draft", Reading);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Friends", (await response.ReadAsync<ProblemDocument>()).Errors.Keys);
        Assert.Null(await DraftOfAsync(Client));
    }

    [Fact]
    public async Task SomebodyArrivingThroughTheLinkStartsIt()
    {
        var (host, hostClient) = await RegisterAsync("Nora Neu", "nora.neu@example.test");
        await hostClient.PutJsonAsync("/api/goals/draft", Reading);

        var invite = await (await hostClient.GetAsync("/api/invite", TestContext.Current.CancellationToken))
            .ReadAsync<InviteDocument>();

        var (guest, guestClient) = await RegisterAsync("Gast Freund", "gast.freund@example.test", invite.Code);

        // The waiting Do is a goal now, with whoever arrived checking it.
        Assert.Null(await DraftOfAsync(hostClient));

        var goal = Assert.Single(await GoalsOfAsync(hostClient));
        Assert.Equal("Jeden Abend lesen", goal.Title);
        Assert.True(goal.IsMine);
        Assert.Equal(guest.Id, Assert.Single(goal.Participants).Id);

        // The friend sees it as somebody else's goal to check.
        var theirs = Assert.Single(await GoalsOfAsync(guestClient));
        Assert.Equal(goal.Id, theirs.Id);
        Assert.False(theirs.IsMine);

        await Factory.WithDatabaseAsync(async database =>
        {
            var cancellation = TestContext.Current.CancellationToken;

            var stored = await database.Goals
                .Include(candidate => candidate.Instances)
                .SingleAsync(candidate => candidate.Id == goal.Id, cancellation);

            // Running like any goal made in front of somebody: a window to
            // deliver into, the reminder it was drafted with, a conversation
            // with both of them in it.
            Assert.NotNull(stored.CurrentInstance);
            Assert.Equal(new TimeOnly(19, 30), stored.ReminderAt);
            Assert.Equal(ScheduleKind.Times, stored.Schedule.Kind);

            var conversation = await database.Conversations
                .Include(candidate => candidate.Participants)
                .SingleAsync(candidate => candidate.Kind == ConversationKind.Goal && candidate.GoalId == goal.Id, cancellation);

            Assert.Equal(
                new[] { host.Id, guest.Id }.Order(),
                conversation.Participants.Select(participant => participant.PersonId).Order());

            // The friend is told they were put on it.
            Assert.True(await database.Notifications.AnyAsync(
                notification => notification.RecipientPersonId == guest.Id
                    && notification.Kind == NotificationKind.GoalInvitation,
                cancellation));
        });
    }

    [Fact]
    public async Task AnExistingAccountFollowingTheLinkStartsItToo()
    {
        var (host, hostClient) = await RegisterAsync("Nora Neu", "nora.neu@example.test");
        await hostClient.PutJsonAsync("/api/goals/draft", Reading);

        var invite = await (await hostClient.GetAsync("/api/invite", TestContext.Current.CancellationToken))
            .ReadAsync<InviteDocument>();

        // test.four has an account and knows nobody here.
        var visitor = await ClientForAsync("test.four@" + SeedAccounts.EmailDomain);
        var accepted = await visitor.PostJsonAsync("/api/invite/accept", new { code = invite.Code });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Null(await DraftOfAsync(hostClient));

        var goal = Assert.Single(await GoalsOfAsync(hostClient));
        Assert.Single(goal.Participants);
        Assert.Contains(await GoalsOfAsync(visitor), candidate => candidate.Id == goal.Id);
        Assert.NotEqual(Guid.Empty, host.Id);
    }

    [Fact]
    public async Task AcceptingAFriendRequestStartsIt()
    {
        var (newcomer, newcomerClient) = await RegisterAsync("Nora Neu", "nora.neu@example.test");
        await newcomerClient.PutJsonAsync("/api/goals/draft", Reading);

        // test.one asks; the newcomer says yes.
        var asked = await Client.PostAsync($"/api/friends/{newcomer.Id}/request", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);

        // Asked is not friends: nothing starts yet.
        Assert.NotNull(await DraftOfAsync(newcomerClient));

        var accepted = await newcomerClient.PostAsync(
            $"/api/friends/{AutomatedTestSeed.CurrentPersonId}/accept",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Null(await DraftOfAsync(newcomerClient));

        var goal = Assert.Single(await GoalsOfAsync(newcomerClient));
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, Assert.Single(goal.Participants).Id);
    }

    [Fact]
    public async Task ItLeavesWithItsOwner()
    {
        var (_, client) = await RegisterAsync("Nora Neu", "nora.neu@example.test");
        await client.PutJsonAsync("/api/goals/draft", Reading);

        var deleted = await client.DeleteJsonAsync("/api/auth/account", new { password = SeedAccounts.Password });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.GoalDrafts.AnyAsync(TestContext.Current.CancellationToken)));
    }
}
