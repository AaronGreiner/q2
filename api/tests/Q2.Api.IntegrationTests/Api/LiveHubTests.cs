using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence;
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

    /// <summary>
    /// The iOS app's way in: no cookie, a bearer token in the address, which is
    /// where a browser's WebSocket has to put it. Negotiation is the request
    /// that decides, so it is enough to see it accepted.
    /// </summary>
    [Fact]
    public async Task ATokenInTheAddressOpensTheLiveConnection()
    {
        var signedIn = await AnonymousClient.PostJsonAsync("/api/auth/token", new
        {
            email = AutomatedTestSeed.FriendEmail,
            password = SeedAccounts.Password,
        });
        signedIn.EnsureSuccessStatusCode();

        var token = (await signedIn.ReadAsync<TokenDocument>()).AccessToken;

        var response = await AnonymousClient.PostAsync(
            $"{LiveHub.Path}/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record TokenDocument(string AccessToken);

    /// <summary>
    /// The handshake is answered before the hub has joined the person's group
    /// or counted them as watching, so <c>StartAsync</c> returning says nothing
    /// about either (#67). <see cref="LiveEvents.Listening"/> is what does, and
    /// it comes with the numbers the app would otherwise read to catch up.
    /// </summary>
    [Fact]
    public async Task TheServerSaysItIsListeningOnlyOnceItIs()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var watching = Factory.Services.GetRequiredService<LiveConnections>();
        var live = Unstarted(friend);

        var watchingWhenSaid = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listening = NextAsync<CountsDocument>(live, LiveEvents.Listening, _ =>
        {
            watchingWhenSaid.TrySetResult(watching.IsWatching(AutomatedTestSeed.FriendPersonId));
            return true;
        });

        await live.StartAsync(TestContext.Current.CancellationToken);

        var counts = await listening;
        Assert.True(await watchingWhenSaid.Task);

        var read = await (await friend.GetAsync("/api/counts", TestContext.Current.CancellationToken))
            .ReadAsync<CountsDocument>();
        Assert.Equal(read, counts);
    }

    /// <summary>
    /// The order inside the hub, seen at the moment it speaks: when it says
    /// <see cref="LiveEvents.Listening"/>, it has already joined the person's
    /// group and counted them as watching. Over a real connection the client
    /// hears it too late to tell the difference, so this calls the hub itself,
    /// with the services the real one gets and a record of what it did.
    /// </summary>
    /// <remarks>
    /// The principal is the one Identity's cookie carries — the account id as
    /// the name identifier — and it authenticates nothing: no request is made.
    /// </remarks>
    [Fact]
    public async Task ItSaysItIsListeningAfterJoiningTheGroupAndCountingThePersonAsWatching()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var accountId = await services.GetRequiredService<Q2DbContext>().Users
            .Where(user => user.PersonId == AutomatedTestSeed.FriendPersonId)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var watching = new LiveConnections();
        var groups = new RecordingGroups();
        var group = LiveHub.GroupOf(AutomatedTestSeed.FriendPersonId);
        var caller = new RecordingCaller(() =>
            (groups.Joined.Contains(group), watching.IsWatching(AutomatedTestSeed.FriendPersonId)));

        var hub = new LiveHub(
            services.GetRequiredService<CurrentPerson>(),
            watching,
            services.GetRequiredService<CountsService>(),
            NullLogger<LiveHub>.Instance)
        {
            Context = new CallerContext(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())],
                authenticationType: "Identity.Application"))),
            Groups = groups,
            Clients = new CallerOnly(caller),
        };

        await hub.OnConnectedAsync();

        var (method, (joined, counted)) = Assert.Single(caller.Sent);
        Assert.Equal(LiveEvents.Listening, method);
        Assert.True(joined, "Listening was said before the connection joined the person's group.");
        Assert.True(counted, "Listening was said before the person was counted as watching.");
    }

    private sealed class CallerContext(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "live-hub-test";

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal User => user;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => TestContext.Current.CancellationToken;

        public override void Abort() => throw new InvalidOperationException("The connection was aborted.");
    }

    private sealed class RecordingGroups : IGroupManager
    {
        public List<string> Joined { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Joined.Add(groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>The caller's end, noting what was true at the moment each message was sent.</summary>
    private sealed class RecordingCaller(Func<(bool Joined, bool Counted)> observe) : ISingleClientProxy
    {
        public List<(string Method, (bool Joined, bool Counted) State)> Sent { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, observe()));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The hub never asks a client anything.");
    }

    /// <summary>Only the caller: the hub speaks to nobody else while a connection opens.</summary>
    private sealed class CallerOnly(ISingleClientProxy caller) : IHubCallerClients
    {
        public ISingleClientProxy Caller => caller;

        IClientProxy IHubCallerClients<IClientProxy>.Caller => caller;

        public IClientProxy Others => throw Unexpected();

        public IClientProxy All => throw Unexpected();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw Unexpected();

        public ISingleClientProxy Client(string connectionId) => throw Unexpected();

        IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => throw Unexpected();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw Unexpected();

        public IClientProxy Group(string groupName) => throw Unexpected();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw Unexpected();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw Unexpected();

        public IClientProxy OthersInGroup(string groupName) => throw Unexpected();

        public IClientProxy User(string userId) => throw Unexpected();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw Unexpected();

        private static NotSupportedException Unexpected() => new("Only the caller is spoken to while a connection opens.");
    }

    /// <summary>
    /// What #67 was about, from the app's side: once it counts itself as
    /// listening, the very next notification is a banner — and not a push to a
    /// phone that is lying beside the open app.
    /// </summary>
    [Fact]
    public async Task TheFirstNotificationAfterListeningIsABannerAndNotAPush()
    {
        const string device = "https://push.example/friend";

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await friend.SubscribeDeviceAsync(device);

        var live = Unstarted(friend);
        var listening = NextAsync<CountsDocument>(live, LiveEvents.Listening, _ => true);
        var announced = NextAsync<LineDocument>(live, LiveEvents.Notification, _ => true);

        await live.StartAsync(TestContext.Current.CancellationToken);
        await listening;
        await SendAsync("Automated test: the moment it listens");

        Assert.Equal("Automated test: the moment it listens", (await announced).Excerpt);
        Assert.DoesNotContain(Factory.PushSender.Sent, message => message.Endpoint == device);
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

    /// <summary>
    /// A banner names the prompt in the language of whoever is looking, as a
    /// push does (#105).
    /// </summary>
    [Fact]
    public async Task TheChallengeIsAnnouncedOnScreenInTheReadersLanguage()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        (await friend.PutJsonAsync("/api/settings", new { language = LanguagePreference.English }))
            .EnsureSuccessStatusCode();

        var mine = await ConnectAsync(Client);
        var theirs = await ConnectAsync(friend);

        var toMe = NextAsync<LineDocument>(mine, LiveEvents.Notification, line => line.Kind == NotificationKind.ChallengePublished);
        var toThem = NextAsync<LineDocument>(theirs, LiveEvents.Notification, line => line.Kind == NotificationKind.ChallengePublished);

        var worker = ActivatorUtilities.CreateInstance<ChallengeQueueWorker>(Factory.Services);
        Assert.Equal(1, await worker.AnnounceAsync(TestContext.Current.CancellationToken));

        Assert.Equal(AutomatedTestSeed.ChallengePrompt, (await toMe).Subject);
        Assert.Equal(AutomatedTestSeed.ChallengePromptEnglish, (await toThem).Subject);
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

    /// <summary>
    /// Connected, and listening: returns once the server has said so, which is
    /// later than <c>StartAsync</c> (#67). A test that sent something the
    /// moment <c>StartAsync</c> returned would be testing that race instead.
    /// </summary>
    private async Task<HubConnection> ConnectAsync(HttpClient signedIn)
    {
        var connection = Unstarted(signedIn);
        var listening = NextAsync<CountsDocument>(connection, LiveEvents.Listening, _ => true);

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await listening;

        return connection;
    }

    /// <summary>A connection with nothing listening yet, for a handler that has to be there from the first message.</summary>
    private HubConnection Unstarted(HttpClient signedIn)
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
