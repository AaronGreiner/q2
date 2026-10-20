using System.Net;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The zone a person's days are counted in (#9): a guess from the browser at
/// registration, shown and changed in settings, and used for the first paint.
/// </summary>
[Trait("Category", "Integration")]
public class TimeZoneEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private const string Password = "ein-langes-passwort";

    private sealed record SessionDocument(string TimeZone, int UtcOffsetMinutes);

    private sealed record SettingsDocument(string TimeZone);

    private sealed record WindowDocument(DateTimeOffset DueAt);

    private sealed record GoalDocument(Guid Id, WindowDocument? Current);

    private sealed record GoalDetailDocument(GoalDocument Goal);

    private async Task<SessionDocument> RegisterAsync(string email, string? timeZone) =>
        await (await AnonymousClient.PostJsonAsync("/api/auth/register", new
        {
            name = "Zonen Test",
            email,
            password = Password,
            timeZone,
        })).ReadAsync<SessionDocument>();

    [Fact]
    public async Task ANewAccountIsCountedInTheZoneItsBrowserIsIn()
    {
        var session = await RegisterAsync("zonen.test@kudos.example", "America/New_York");

        Assert.Equal("America/New_York", session.TimeZone);

        // June: New York is four hours behind UTC, and the first paint knows it.
        Assert.Equal(-240, session.UtcOffsetMinutes);
    }

    [Fact]
    public async Task AZoneTheServerDoesNotKnowFallsBackToTheDeployments()
    {
        var session = await RegisterAsync("mars.test@kudos.example", "Mars/Olympus_Mons");

        Assert.Equal("Europe/Berlin", session.TimeZone);
    }

    [Fact]
    public async Task TheZoneIsShownAndChangedInSettings()
    {
        Assert.Equal("Europe/Berlin", (await (await Client.GetAsync("/api/settings", TestContext.Current.CancellationToken)).ReadAsync<SettingsDocument>()).TimeZone);

        var changed = await Client.PutJsonAsync("/api/settings", new { timeZone = "Asia/Tokyo" });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("Asia/Tokyo", (await changed.ReadAsync<SettingsDocument>()).TimeZone);

        var session = await (await Client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken)).ReadAsync<SessionDocument>();
        Assert.Equal("Asia/Tokyo", session.TimeZone);
        Assert.Equal(540, session.UtcOffsetMinutes);
    }

    [Fact]
    public async Task AnUnknownZoneIsRefusedRatherThanCountedInUtc()
    {
        var response = await Client.PutJsonAsync("/api/settings", new { timeZone = "Europe/Atlantis" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("TimeZone", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWindowAlreadyOpenKeepsItsDeadline()
    {
        var before = (await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal.Current!.DueAt;

        await Client.PutJsonAsync("/api/settings", new { timeZone = "Asia/Tokyo" });

        var after = (await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal.Current!.DueAt;

        Assert.Equal(before, after);
    }
}
