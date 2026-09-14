using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The open app's line back from the server, tested from the client's end.
/// </summary>
/// <remarks>
/// Signed in the way everything else here is: with the cookie the real sign-in
/// handed out, and no fake authentication in between. Long polling rather than
/// a WebSocket because the in-memory test server speaks HTTP; the hub, its
/// guard and its groups are the same whichever transport carries them.
/// </remarks>
[Trait("Category", "Integration")]
public class LiveHubTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private sealed record ChangeDocument(LiveArea Area, Guid? Id);

    private sealed record CountsDocument(
        int UnreadChats,
        int PendingFriendRequests,
        int UnseenNotifications,
        int ProofsAwaitingVote);

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

    private readonly List<HubConnection> _connections = [];

    [Fact]
    public async Task WithoutASessionThereIsNoConnection()
    {
        var response = await AnonymousClient.PostAsync(
            $"{LiveHub.Path}/negotiate?negotiateVersion=1",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AMessageArrivesLiveAndTheBadgeMovesWithIt()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var live = await ConnectAsync(friend);

        var changed = NextAsync<ChangeDocument>(
            live,
            LiveEvents.Changed,
            change => change.Area == LiveArea.Chats && change.Id == AutomatedTestSeed.DirectConversationId);

        var counts = NextAsync<CountsDocument>(live, LiveEvents.Counts, _ => true);

        await SendAsync("Automated test: arriving live");

        Assert.Equal(LiveArea.Chats, (await changed).Area);
        Assert.Equal(1, (await counts).UnreadChats);
    }

    /// <summary>
    /// An open app already shows it, and a phone vibrating beside the laptop
    /// that just updated is the same thing said twice. Once the app is put
    /// away, the phone rings again.
    /// </summary>
    [Fact]
    public async Task SomebodyWhoIsLookingIsNotAlsoBuzzed()
    {
        const string device = "https://push.example/friend";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.SubscribeDeviceAsync(device);

        var live = await ConnectAsync(friend);

        await SendAsync("Automated test: while looking");
        Assert.DoesNotContain(Factory.PushSender.Sent, message => message.Endpoint == device);

        await live.StopAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() =>
            !Factory.Services.GetRequiredService<LiveConnections>().IsWatching(AutomatedTestSeed.FriendPersonId));

        await SendAsync("Automated test: after looking away");
        Assert.Contains(
            Factory.PushSender.Sent,
            message => message.Endpoint == device && message.Payload.Kind == NotificationKind.MessageReceived);
    }

    /// <summary>
    /// What they are shown instead is what the push would have said: the same
    /// parts, from the same payload, kept nowhere.
    /// </summary>
    [Fact]
    public async Task SomebodyWhoIsLookingIsShownWhatAPushWouldHaveSaid()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var live = await ConnectAsync(friend);

        var announced = NextAsync<LineDocument>(live, LiveEvents.Notification, _ => true);

        await SendAsync("Automated test: on screen");

        var line = await announced;

        Assert.Equal(NotificationKind.MessageReceived, line.Kind);
        Assert.Equal("Automated test: on screen", line.Excerpt);
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, line.Actor?.Id);
        Assert.Equal(NotificationTarget.Conversation, line.Target);
        Assert.Equal(AutomatedTestSeed.DirectConversationId, line.TargetId);
        Assert.Null(line.Id);
    }

    /// <summary>
    /// Muting keeps a conversation quiet on screen as well as on the phone.
    /// Proved by order: the muted message goes first and an audible one second,
    /// so had the first been announced it would have arrived first.
    /// </summary>
    [Fact]
    public async Task AMutedConversationIsNotAnnouncedOnScreenEither()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var live = await ConnectAsync(friend);

        var first = NextAsync<LineDocument>(live, LiveEvents.Notification, _ => true);

        await MuteAsync(friend, muted: true);
        await SendAsync("Automated test: muted");
        await MuteAsync(friend, muted: false);
        await SendAsync("Automated test: audible");

        Assert.Equal("Automated test: audible", (await first).Excerpt);
    }

    /// <summary>
    /// The one notification addressed to everybody reaches whoever is looking
    /// as well — on screen, since they are not buzzed.
    /// </summary>
    [Fact]
    public async Task TheChallengeIsAnnouncedOnScreenToWhoeverIsLooking()
    {
        var live = await ConnectAsync(Client);

        var announced = NextAsync<LineDocument>(
            live,
            LiveEvents.Notification,
            line => line.Kind == NotificationKind.ChallengePublished);

        var worker = ActivatorUtilities.CreateInstance<ChallengeQueueWorker>(Factory.Services);
        Assert.Equal(1, await worker.AnnounceAsync(TestContext.Current.CancellationToken));

        var line = await announced;

        Assert.Equal(AutomatedTestSeed.ChallengePrompt, line.Subject);
        Assert.Null(line.Actor);
    }

    [Fact]
    public async Task OpeningTheBellClearsItsBadgeOnEveryOtherDevice()
    {
        var live = await ConnectAsync(Client);
        var cleared = NextAsync<CountsDocument>(live, LiveEvents.Counts, counts => counts.UnseenNotifications == 0);

        var otherDevice = await ClientForAsync(AutomatedTestSeed.CurrentPersonEmail);
        (await otherDevice.GetAsync("/api/notifications", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(0, (await cleared).UnseenNotifications);
    }

    /// <summary>
    /// A connection hears its own person's news and nobody else's. Proved by
    /// order rather than by waiting for silence: the message goes first and
    /// something that does concern this person second, so if the message had
    /// leaked it would have arrived before it.
    /// </summary>
    [Fact]
    public async Task NothingOfSomebodyElsesArrives()
    {
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);
        var live = await ConnectAsync(stranger);

        var firstChange = NextAsync<ChangeDocument>(live, LiveEvents.Changed, _ => true);
        var firstAnnouncement = NextAsync<LineDocument>(live, LiveEvents.Notification, _ => true);

        await SendAsync("Automated test: not for the stranger");

        (await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(LiveArea.Friends, (await firstChange).Area);
        Assert.Equal(NotificationKind.FriendshipStarted, (await firstAnnouncement).Kind);
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
        await base.DisposeAsync();
    }

    private async Task SendAsync(string text) =>
        (await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", new { text }))
            .EnsureSuccessStatusCode();

    private static async Task MuteAsync(HttpClient client, bool muted) =>
        (await client.PutJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/mute", new { muted }))
            .EnsureSuccessStatusCode();

    private async Task<HubConnection> ConnectAsync(HttpClient signedIn)
    {
        var cookie = signedIn.DefaultRequestHeaders.GetValues("Cookie").Single();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, LiveHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .AddJsonProtocol(protocol =>
                protocol.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        _connections.Add(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    /// <summary>The next <paramref name="name"/> event that matches, or a timeout.</summary>
    private static Task<T> NextAsync<T>(HubConnection connection, string name, Func<T, bool> matches)
    {
        var arrived = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<T>(name, value =>
        {
            if (matches(value))
            {
                arrived.TrySetResult(value);
            }
        });

        return arrived.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The condition never came true.");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}
