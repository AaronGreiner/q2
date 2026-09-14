using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
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

        var first = NextAsync<ChangeDocument>(live, LiveEvents.Changed, _ => true);

        await SendAsync("Automated test: not for the stranger");

        (await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(LiveArea.Friends, (await first).Area);
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
