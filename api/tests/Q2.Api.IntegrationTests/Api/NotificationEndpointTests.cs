using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Notifications: who gets told, and what stops it.
/// </summary>
/// <remarks>
/// The encryption is checked against RFC 8291's own worked example in
/// <c>WebPushCryptoTests</c>, so nothing here is about crypto. What needs a
/// pipeline is the three gates in front of it — the person's switch, their
/// quiet hours in their own zone, and whether the recipient set matches the one
/// the activity feed already uses.
///
/// The last of those is the rule this stage turns on: push is a delivery route
/// for something that already exists, so a notification nobody could then go
/// and look at would be a new product rather than a new route.
/// </remarks>
[Trait("Category", "Integration")]
public class NotificationEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record KeyDocument(bool IsAvailable, string? PublicKey);

    private const string Endpoint = "https://push.example/subscription/one";

    /// <summary>A real P-256 point and secret; nothing here decrypts them.</summary>
    private const string BrowserKey =
        "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";

    private const string BrowserSecret = "BTBZMqHH6r4Tts7J_aSIgg";

    private static Task<HttpResponseMessage> SubscribeAsync(
        HttpClient client,
        string endpoint = Endpoint) =>
        client.PostJsonAsync(
            "/api/notifications/subscribe",
            new { endpoint, publicKey = BrowserKey, authSecret = BrowserSecret });

    /// <summary>Evening in Berlin, which is when a warning is allowed out at all.</summary>
    private static readonly DateTimeOffset Evening = new(2026, 6, 15, 19, 0, 0, TimeSpan.Zero);

    private async Task<int> RunMaintenanceAsync()
    {
        var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(Factory.Services);
        return await worker.RunOnceAsync(TestContext.Current.CancellationToken);
    }

    private void At(DateTimeOffset instant) =>
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(instant);

    [Fact]
    public async Task TheKeyEndpointSaysWhetherThisDeploymentSendsAnything()
    {
        var available = await (await Client.GetAsync("/api/notifications/key", TestContext.Current.CancellationToken))
            .ReadAsync<KeyDocument>();

        Assert.True(available.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(available.PublicKey));

        // A deployment without VAPID keys is a supported state, not a broken
        // one: the settings screen says so rather than offering a dead switch.
        Factory.PushSender.IsConfigured = false;

        var absent = await (await Client.GetAsync("/api/notifications/key", TestContext.Current.CancellationToken))
            .ReadAsync<KeyDocument>();

        Assert.False(absent.IsAvailable);
        Assert.Null(absent.PublicKey);
    }

    [Fact]
    public async Task ABrowserSubscribesAndCanBeForgottenAgain()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await SubscribeAsync(Client)).StatusCode);

        await Factory.WithDatabaseAsync(async database =>
            Assert.True(await database.PushSubscriptions.AnyAsync(
                subscription => subscription.PersonId == AutomatedTestSeed.CurrentPersonId,
                TestContext.Current.CancellationToken)));

        var removed = await Client.PostJsonAsync("/api/notifications/unsubscribe", new { endpoint = Endpoint });
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.PushSubscriptions.AnyAsync(TestContext.Current.CancellationToken)));

        // Unsubscribing is what somebody does when they are unsure of their
        // state, so doing it twice is not an error.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostJsonAsync("/api/notifications/unsubscribe", new { endpoint = Endpoint })).StatusCode);
    }

    [Fact]
    public async Task SubscribingTwiceIsOneDevice()
    {
        await SubscribeAsync(Client);
        await SubscribeAsync(Client);

        await Factory.WithDatabaseAsync(async database =>
            Assert.Equal(1, await database.PushSubscriptions.CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// A shared device where the previous person signed out: the browser quite
    /// correctly hands the same endpoint to whoever is there now.
    /// </summary>
    [Fact]
    public async Task AnEndpointChangesHandsRatherThanColliding()
    {
        await SubscribeAsync(Client);
        await SubscribeAsync(await ClientForAsync(AutomatedTestSeed.FriendEmail));

        await Factory.WithDatabaseAsync(async database =>
        {
            var subscription = await database.PushSubscriptions.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(AutomatedTestSeed.FriendPersonId, subscription.PersonId);
        });
    }

    [Fact]
    public async Task ARequestWithoutAUsableEndpointIsRefused()
    {
        var empty = await Client.PostJsonAsync("/api/notifications/subscribe", new { });

        var insecure = await Client.PostJsonAsync(
            "/api/notifications/subscribe",
            new { endpoint = "http://push.example/one", publicKey = BrowserKey, authSecret = BrowserSecret });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, insecure.StatusCode);
    }

    /// <summary>
    /// The recipient set is the one the feed already uses: the owner's friends,
    /// and never the owner.
    /// </summary>
    [Fact]
    public async Task AWarningReachesTheFriendsDeviceAndNotTheOwnersOwn()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await SubscribeAsync(friend, "https://push.example/friend");
        await SubscribeAsync(Client, "https://push.example/owner");

        At(Evening);
        await RunMaintenanceAsync();

        var sent = Factory.PushSender.Sent;

        Assert.Contains(sent, message =>
            message.Endpoint == "https://push.example/friend"
            && message.Payload.Kind == PushKind.WindowAtRisk);

        // The one person who does not need telling that they are running out of
        // time is the person running out of time.
        Assert.DoesNotContain(sent, message => message.Endpoint == "https://push.example/owner");
    }

    /// <summary>
    /// The wording is not in the payload. A kind, a subject and an amount go
    /// out, and the service worker composes the sentence in the person's own
    /// language from the catalogue it already has.
    /// </summary>
    [Fact]
    public async Task ThePayloadCarriesTheFactsRatherThanASentence()
    {
        await SubscribeAsync(await ClientForAsync(AutomatedTestSeed.FriendEmail), "https://push.example/friend");

        At(Evening);
        await RunMaintenanceAsync();

        // More than one window in this seed is at risk in the evening, which is
        // the same thing RiskAndBalanceEndpointTests records — so this picks
        // the one it is about rather than insisting there is only one.
        var warning = Assert.Single(
            Factory.PushSender.Sent,
            message => message.Payload.Subject == "Automated test: shared active goal");

        Assert.False(string.IsNullOrWhiteSpace(warning.Payload.Subject));
        Assert.NotNull(warning.Payload.Amount);

        var json = warning.Payload.ToJson();

        Assert.Contains("\"kind\":\"WindowAtRisk\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SomebodyWhoTurnedRemindersOffIsNotTold()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await SubscribeAsync(friend, "https://push.example/friend");
        (await friend.PutJsonAsync("/api/settings", new { notifyReminders = false })).EnsureSuccessStatusCode();

        At(Evening);
        await RunMaintenanceAsync();

        Assert.DoesNotContain(Factory.PushSender.Sent, message => message.Payload.Kind == PushKind.WindowAtRisk);
    }

    /// <summary>
    /// Quiet hours are read against the recipient's own clock. The evening
    /// warning and the default window are set so that they meet rather than
    /// cancel — 20:00 for the warning, 22:00 for the silence.
    /// </summary>
    [Fact]
    public async Task NothingArrivesDuringQuietHours()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await SubscribeAsync(friend, "https://push.example/friend");

        // 23:00 in Berlin, which is inside the default 22:00–07:00 window.
        At(new DateTimeOffset(2026, 6, 15, 21, 0, 0, TimeSpan.Zero));
        await RunMaintenanceAsync();

        Assert.Empty(Factory.PushSender.Sent);
    }

    [Fact]
    public async Task TurningQuietHoursOffLetsTheEveningThrough()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await SubscribeAsync(friend, "https://push.example/friend");

        (await friend.PutJsonAsync("/api/settings", new { quietHoursEnabled = false })).EnsureSuccessStatusCode();

        At(new DateTimeOffset(2026, 6, 15, 21, 0, 0, TimeSpan.Zero));
        await RunMaintenanceAsync();

        Assert.Contains(Factory.PushSender.Sent, message => message.Payload.Kind == PushKind.WindowAtRisk);
    }

    /// <summary>
    /// The push service's own verdict that a device is gone: an uninstalled
    /// app, a cleared browser, a revoked permission.
    /// </summary>
    [Fact]
    public async Task ADeviceThePushServiceCallsGoneIsForgotten()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await SubscribeAsync(friend, "https://push.example/friend");

        Factory.PushSender.Outcome = PushOutcome.Gone;

        At(Evening);
        await RunMaintenanceAsync();

        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.PushSubscriptions.AnyAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// A device that is merely off produces timeouts rather than a verdict, and
    /// dropping it on the first one would unsubscribe somebody for going on
    /// holiday.
    /// </summary>
    [Fact]
    public async Task ADeviceThatIsMerelyUnreachableIsKept()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await SubscribeAsync(friend, "https://push.example/friend");

        Factory.PushSender.Outcome = PushOutcome.Failed;

        At(Evening);
        await RunMaintenanceAsync();

        await Factory.WithDatabaseAsync(async database =>
        {
            var subscription = await database.PushSubscriptions.SingleAsync(TestContext.Current.CancellationToken);

            // Still there, and counting. The number is however many windows
            // this seed had at risk this evening; what matters is that it is
            // below the threshold and the device was kept.
            Assert.InRange(subscription.ConsecutiveFailures, 1, PushSubscription.MaxConsecutiveFailures - 1);
        });
    }

    /// <summary>
    /// The one notification in q2 that is not scoped to somebody's friends,
    /// because the prompt is deliberately the same for everybody.
    /// </summary>
    [Fact]
    public async Task TheChallengeIsAnnouncedOnceToEverybodyWithADevice()
    {
        await SubscribeAsync(Client);

        var worker = ActivatorUtilities.CreateInstance<ChallengeQueueWorker>(Factory.Services);

        Assert.Equal(1, await worker.AnnounceAsync(TestContext.Current.CancellationToken));

        var announcement = Assert.Single(Factory.PushSender.Sent);
        Assert.Equal(PushKind.ChallengePublished, announcement.Payload.Kind);
        Assert.Equal(AutomatedTestSeed.ChallengePrompt, announcement.Payload.Subject);

        // Twice a day is worse than never: an hourly pass must not mean an
        // hourly notification.
        Assert.Equal(0, await worker.AnnounceAsync(TestContext.Current.CancellationToken));
        Assert.Single(Factory.PushSender.Sent);
    }

    [Fact]
    public async Task SomebodyWhoTurnedTheChallengeOffIsNotTold()
    {
        await SubscribeAsync(Client);
        (await Client.PutJsonAsync("/api/settings", new { notifyChallenge = false })).EnsureSuccessStatusCode();

        var worker = ActivatorUtilities.CreateInstance<ChallengeQueueWorker>(Factory.Services);

        Assert.Equal(0, await worker.AnnounceAsync(TestContext.Current.CancellationToken));
        Assert.Empty(Factory.PushSender.Sent);
    }

    [Fact]
    public async Task SubscribingNeedsASession()
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/notifications/subscribe",
            new { endpoint = Endpoint, publicKey = BrowserKey, authSecret = BrowserSecret });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
