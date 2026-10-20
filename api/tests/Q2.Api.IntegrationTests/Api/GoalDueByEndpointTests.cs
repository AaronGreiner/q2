using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A Do whose day ends at a time rather than at midnight (#98): made, counted
/// down to, refusing a photograph after it, and starting tomorrow when today's
/// time has already passed.
/// </summary>
[Trait("Category", "Integration")]
public class GoalDueByEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record WindowDocument(DateOnly StartsOn, DateOnly DueOn, DateTimeOffset DueAt, GoalInstanceStatus Status, bool AcceptsProof);

    private sealed record GoalDocument(Guid Id, TimeOnly? DueBy, DateOnly? StartsOn, WindowDocument? Current);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    /// <summary>An instant at a local time in Europe/Berlin on the fixture's day, or days after.</summary>
    private static DateTimeOffset Local(int hour, int minute = 0, int daysLater = 0)
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var day = Q2ApiFactory.Today.AddDays(daysLater).ToDateTime(new TimeOnly(hour, minute));

        return new DateTimeOffset(day, berlin.GetUtcOffset(day));
    }

    private Task<HttpResponseMessage> CreateAsync(string dueBy, object? schedule = null, string? reminderAt = null) =>
        Client.PostJsonAsync("/api/goals", new
        {
            title = "Automated test: vor der Arbeit laufen",
            category = "Sport",
            schedule = schedule ?? new { kind = "Interval", everyDays = 1 },
            reminderAt,
            dueBy,
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

    [Fact]
    public async Task TheWindowEndsAtThatTimeAndTakesNoPhotographAfterIt()
    {
        Clock.Set(Local(11));

        var created = await (await CreateAsync("18:00")).ReadAsync<GoalDocument>();

        Assert.Equal(new TimeOnly(18, 0), created.DueBy);
        Assert.Equal(Local(18), created.Current!.DueAt);
        Assert.True(created.Current.AcceptsProof);

        // A minute after six in the evening: the day is over for this goal.
        Clock.Set(Local(18, 1));
        var late = await Client.DeliverProofAsync(created.Id);

        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
    }

    [Fact]
    public async Task ADayThatHasAlreadyEndedStartsTomorrow()
    {
        Clock.Set(Local(11));

        var created = await (await CreateAsync("07:00")).ReadAsync<GoalDocument>();

        Assert.Equal(Q2ApiFactory.Today.AddDays(1), created.StartsOn);
        Assert.Null(created.Current);
    }

    [Fact]
    public async Task OnlyADoOnSetDaysEndsItsDayAtATimeAndItsReminderComesFirst()
    {
        Clock.Set(Local(5));

        var quota = await CreateAsync("07:00", new { kind = "Times", times = 3, period = "Week" });
        Assert.Equal(HttpStatusCode.BadRequest, quota.StatusCode);
        Assert.Contains("dueBy", await quota.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);

        var reminder = await CreateAsync("07:00", reminderAt: "08:00:00");
        Assert.Equal(HttpStatusCode.BadRequest, reminder.StatusCode);
        Assert.Contains("reminderAt", await reminder.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    public override async ValueTask DisposeAsync()
    {
        Clock.Reset();
        await base.DisposeAsync();
    }
}
