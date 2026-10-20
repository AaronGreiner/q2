using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A goal's daily reminder: moving it — the one thing about a running goal its
/// owner changes on their own — and having it actually sent (#82).
/// </summary>
[Trait("Category", "Integration")]
public class GoalReminderEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record GoalDocument(Guid Id, GoalStatus Status, TimeOnly? ReminderAt);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private Task<HttpResponseMessage> SetAsync(HttpClient client, Guid goalId, string? reminderAt) =>
        client.PutJsonAsync($"/api/goals/{goalId}/reminder", new { reminderAt });

    [Fact]
    public async Task TheOwnerMovesTheReminderAndItStays()
    {
        var response = await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TimeOnly(7, 30), (await response.ReadAsync<GoalDocument>()).ReminderAt);

        var detail = await (await Client.GetAsync(
                $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
                TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        Assert.Equal(new TimeOnly(7, 30), detail.Goal.ReminderAt);
    }

    [Fact]
    public async Task NullTakesTheReminderAway()
    {
        await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        var response = await SetAsync(Client, AutomatedTestSeed.ActiveGoalId, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadAsync<GoalDocument>()).ReminderAt);
    }

    [Fact]
    public async Task AFriendOnTheGoalCannotMoveIt()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await SetAsync(friend, AutomatedTestSeed.ActiveGoalId, "07:30:00");

        // Not theirs, and a 404 rather than a 403: the answer is the same as for
        // a goal that does not exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AStoppedGoalKeepsItsReminder()
    {
        var response = await SetAsync(Client, AutomatedTestSeed.CompletedGoalId, "07:30:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- sending it ---------------------------------------------------------

    private const string Device = "https://push.example/reminded";

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    /// <summary>An instant at a local time in Europe/Berlin on the fixture's day, or days after.</summary>
    private static DateTimeOffset Local(int hour, int minute = 0, int daysLater = 0)
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var day = Q2ApiFactory.Today.AddDays(daysLater).ToDateTime(new TimeOnly(hour, minute));

        return new DateTimeOffset(day, berlin.GetUtcOffset(day));
    }

    private async Task<Guid> CreateRemindedDoAsync(object schedule, string reminderAt = "12:00:00")
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Meditation",
            schedule,
            reminderAt,
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<GoalDocument>()).Id;
    }

    private async Task RunMaintenanceAtAsync(DateTimeOffset now)
    {
        Clock.Set(now);

        // A new worker every time, which is also what a restart is.
        using var scope = Factory.Services.CreateScope();
        await ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider)
            .RunOnceAsync(TestContext.Current.CancellationToken);
    }

    private int RemindersFor(Guid goalId) =>
        Factory.PushSender.Sent.Count(message => message.Endpoint == Device
            && message.Payload.Kind == NotificationKind.ReminderDue
            && message.Payload.TargetId == goalId);

    [Fact]
    public async Task ADoIsRemindedOnceAtItsTime()
    {
        await Client.SubscribeDeviceAsync(Device);
        var goal = await CreateRemindedDoAsync(new { kind = "Interval", everyDays = 1 });

        await RunMaintenanceAtAsync(Local(11, 50));
        Assert.Equal(0, RemindersFor(goal));

        await RunMaintenanceAtAsync(Local(12, 5));
        Assert.Equal(1, RemindersFor(goal));

        // The next pass, and a restarted one, say nothing more today.
        await RunMaintenanceAtAsync(Local(12, 15));
        await RunMaintenanceAtAsync(Local(18, 0));
        Assert.Equal(1, RemindersFor(goal));

        // Not kept in the bell: the start screen shows what is still to do.
        var bell = await (await Client.GetAsync("/api/notifications", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(nameof(NotificationKind.ReminderDue), bell, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AQuotaIsRemindedEachDayUntilItIsMet()
    {
        await Client.SubscribeDeviceAsync(Device);
        var goal = await CreateRemindedDoAsync(new { kind = "Times", times = 3, period = "Week" });

        await RunMaintenanceAtAsync(Local(12, 5));
        await RunMaintenanceAtAsync(Local(12, 5, daysLater: 1));

        Assert.Equal(2, RemindersFor(goal));
    }

    [Fact]
    public async Task ADeliveredWindowIsNotRemindedAbout()
    {
        await Client.SubscribeDeviceAsync(Device);
        var goal = await CreateRemindedDoAsync(new { kind = "Interval", everyDays = 1 });

        await Client.DeliverAcceptedProofAsync(goal);
        await RunMaintenanceAtAsync(Local(12, 5));

        Assert.Equal(0, RemindersFor(goal));
    }

    [Fact]
    public async Task ItsSwitchSilencesIt()
    {
        await Client.SubscribeDeviceAsync(Device);
        var goal = await CreateRemindedDoAsync(new { kind = "Interval", everyDays = 1 });

        (await Client.PutJsonAsync("/api/settings", new { notifications = new { reminders = false } }))
            .EnsureSuccessStatusCode();

        await RunMaintenanceAtAsync(Local(12, 5));

        Assert.Equal(0, RemindersFor(goal));
    }

    [Fact]
    public async Task QuietHoursDropIt()
    {
        await Client.SubscribeDeviceAsync(Device);
        var goal = await CreateRemindedDoAsync(new { kind = "Interval", everyDays = 1 }, reminderAt: "22:30:00");

        await RunMaintenanceAtAsync(Local(22, 35));

        // Dropped rather than held, like every interruption in quiet hours.
        Assert.Equal(0, RemindersFor(goal));
    }

    public override async ValueTask DisposeAsync()
    {
        Clock.Reset();
        await base.DisposeAsync();
    }
}
