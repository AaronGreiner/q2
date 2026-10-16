using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A Don't through the real pipeline: made with a limit and a proof mode, a use
/// logged in one tap, a relapse that is nobody's notification, and a
/// conversation and a record that say so (docs/adr/0037-donts.md).
/// </summary>
/// <remarks>
/// The rules themselves — kept, relapse, the third relapse, catching up — are
/// <c>DontTests</c>. What is worth the pipeline is who may do what, what comes
/// back, who is told, and who may see a use's photograph.
/// </remarks>
[Trait("Category", "Integration")]
public class DontsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record WindowDocument(
        DateOnly StartsOn,
        DateOnly DueOn,
        int RequiredProofs,
        GoalInstanceStatus Status,
        int? Limit,
        int Uses,
        Guid? LatestUseId);

    private sealed record GoalDocument(
        Guid Id,
        GoalKind Kind,
        int? Limit,
        DontProofMode? ProofMode,
        WindowDocument? Current,
        int Streak,
        int WindowsDone,
        int WindowsMissed,
        int WindowsRelapsed,
        DateOnly? RunStartedOn);

    private sealed record GoalDetailDocument(GoalDocument Goal, IReadOnlyList<WindowDocument> History, Guid? ConversationId);

    private sealed record EventDocument(GoalEventKind Kind, bool IsMine, int? Uses, int? Limit, Guid? ImageId, int? Streak);

    private sealed record ChatDocument(IReadOnlyList<EventDocument> Events);

    private sealed record BalanceDocument(int Done, int Missed, int Relapsed);

    private sealed record DayDocument(int Done, int Total, int Percent);

    private sealed record ProfileDocument(BalanceDocument Balance, DayDocument Today);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    private async Task<HttpResponseMessage> CreateDontAsync(int? limit, string? period = "Day", string? proofMode = "HonestOnly") =>
        await Client.PostJsonAsync("/api/goals", new
        {
            title = "Kein Alkohol",
            kind = "Dont",
            limit,
            proofMode,
            schedule = new { kind = "Limit", period },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

    private async Task<GoalDocument> CreatedDontAsync(int limit, string period = "Day", string proofMode = "HonestOnly")
    {
        var response = await CreateDontAsync(limit, period, proofMode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<GoalDocument>();
    }

    private async Task<HttpResponseMessage> LogUseAsync(HttpClient client, Guid goalId, Guid? imageId = null) =>
        await client.PostJsonAsync($"/api/goals/{goalId}/uses", new { imageId });

    private async Task<HttpResponseMessage> UndoUseAsync(HttpClient client, Guid goalId, Guid useId) =>
        await client.DeleteAsync($"/api/goals/{goalId}/uses/{useId}", TestContext.Current.CancellationToken);

    /// <summary>
    /// A slipped thumb can be taken back for a moment (#110): the count goes
    /// back, and the line in the conversation goes with it.
    /// </summary>
    [Fact]
    public async Task TheOwnerCanTakeBackAUseJustLogged()
    {
        var created = await CreatedDontAsync(limit: 3, period: "Week");
        var first = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        var later = Q2ApiFactory.Now.AddMinutes(5);
        Clock.Set(later);
        var logged = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        Assert.Equal(2, logged.Current!.Uses);
        Assert.NotNull(logged.Current.LatestUseId);
        Assert.NotEqual(first.Current!.LatestUseId, logged.Current.LatestUseId);

        Clock.Set(later.Add(DontUse.UndoPeriod - TimeSpan.FromSeconds(1)));
        var response = await UndoUseAsync(Client, created.Id, logged.Current.LatestUseId.Value);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var undone = await response.ReadAsync<GoalDocument>();
        Assert.Equal(1, undone.Current!.Uses);
        Assert.Equal(GoalInstanceStatus.Open, undone.Current.Status);

        // Once: the one before it was logged minutes ago and is not "just logged".
        Assert.Equal(first.Current.LatestUseId, undone.Current.LatestUseId);
        Assert.Equal(HttpStatusCode.BadRequest, (await UndoUseAsync(Client, created.Id, undone.Current.LatestUseId!.Value)).StatusCode);
    }

    [Fact]
    public async Task AUseCannotBeTakenBackOnceTheMomentHasPassed()
    {
        var created = await CreatedDontAsync(limit: 3, period: "Week");
        var logged = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        Clock.Set(Q2ApiFactory.Now.Add(DontUse.UndoPeriod + TimeSpan.FromSeconds(1)));
        var response = await UndoUseAsync(Client, created.Id, logged.Current!.LatestUseId!.Value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Use", (await response.ReadAsync<ValidationDocument>()).Errors.Keys);
    }

    [Fact]
    public async Task NobodyButTheOwnerTakesAUseBack()
    {
        var created = await CreatedDontAsync(limit: 3, period: "Week");
        var logged = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        // The friend checking it sees the goal, and is still told it is not there.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var response = await UndoUseAsync(friend, created.Id, logged.Current!.LatestUseId!.Value);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The use that goes over the limit decided the period (ADR 0037): the app
    /// asks before that tap, and nothing takes it back afterwards.
    /// </summary>
    [Fact]
    public async Task ARelapseIsNotTakenBack()
    {
        var created = await CreatedDontAsync(limit: 0);
        var relapsed = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        Assert.Equal(GoalInstanceStatus.Relapse, relapsed.Current!.Status);

        var response = await UndoUseAsync(Client, created.Id, relapsed.Current.LatestUseId!.Value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ADontOpensItsPeriodWithItsLimitAndNothingToPhotograph()
    {
        var created = await CreatedDontAsync(limit: 2, period: "Week");

        Assert.Equal(GoalKind.Dont, created.Kind);
        Assert.Equal(2, created.Limit);
        Assert.Equal(DontProofMode.HonestOnly, created.ProofMode);

        Assert.NotNull(created.Current);
        Assert.Equal(2, created.Current.Limit);
        Assert.Equal(0, created.Current.Uses);
        Assert.Equal(0, created.Current.RequiredProofs);
        Assert.Equal(DayOfWeek.Monday, created.Current.StartsOn.DayOfWeek);
        Assert.Equal(created.Current.StartsOn, created.RunStartedOn);
    }

    [Fact]
    public async Task AProofAtTheEndAsksForOnePhotograph()
    {
        var created = await CreatedDontAsync(limit: 0, proofMode: "ProofAtEnd");

        Assert.Equal(1, created.Current!.RequiredProofs);
    }

    [Fact]
    public async Task ADontWithoutALimitOrAWayToCheckItIsRefused()
    {
        var response = await CreateDontAsync(limit: null, proofMode: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<ValidationDocument>();
        Assert.Contains("Limit", problem.Errors.Keys);
        Assert.Contains("ProofMode", problem.Errors.Keys);
    }

    [Fact]
    public async Task ADoCannotBeCountedPerDayOrHaveALimitSchedule()
    {
        var response = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Laufen",
            schedule = new { kind = "Limit", period = "Day" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheUseOverTheLimitIsARelapseAndNobodyIsNotified()
    {
        var created = await CreatedDontAsync(limit: 0);
        var before = await NotificationCountAsync();

        var response = await LogUseAsync(Client, created.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await response.ReadAsync<GoalDocument>();

        // Still this period's window, now decided.
        Assert.NotNull(after.Current);
        Assert.Equal(GoalInstanceStatus.Relapse, after.Current.Status);
        Assert.Equal(1, after.Current.Uses);
        Assert.Equal(1, after.WindowsRelapsed);
        Assert.Equal(0, after.WindowsMissed);

        // The run starts again tomorrow.
        Assert.Equal(Q2ApiFactory.Today.AddDays(1), after.RunStartedOn);

        Assert.Equal(before, await NotificationCountAsync());
    }

    [Fact]
    public async Task UsesWithinTheLimitKeepThePeriodOpen()
    {
        var created = await CreatedDontAsync(limit: 2);

        await LogUseAsync(Client, created.Id);
        var after = await (await LogUseAsync(Client, created.Id)).ReadAsync<GoalDocument>();

        Assert.Equal(GoalInstanceStatus.Open, after.Current!.Status);
        Assert.Equal(2, after.Current.Uses);
    }

    [Fact]
    public async Task OnlyTheOwnerLogsAndOnlyOnADont()
    {
        var created = await CreatedDontAsync(limit: 0);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await LogUseAsync(friend, created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LogUseAsync(Client, AutomatedTestSeed.ActiveGoalId)).StatusCode);
    }

    [Fact]
    public async Task AUsesPhotographIsSeenByTheGoalsFriendsAndNobodyElse()
    {
        var created = await CreatedDontAsync(limit: 1);
        var image = await Client.UploadProofImageAsync();

        (await LogUseAsync(Client, created.Id, image)).EnsureSuccessStatusCode();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        Assert.Equal(
            HttpStatusCode.OK,
            (await friend.GetAsync($"/api/images/{image}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.NotEqual(
            HttpStatusCode.OK,
            (await stranger.GetAsync($"/api/images/{image}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task TheConversationSaysTheUseAndTheRelapse()
    {
        var created = await CreatedDontAsync(limit: 0);
        var image = await Client.UploadProofImageAsync();
        await LogUseAsync(Client, created.Id, image);

        var detail = await (await Client.GetAsync($"/api/goals/{created.Id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var chat = await (await friend.GetAsync($"/api/chats/{detail.ConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ChatDocument>();

        var use = Assert.Single(chat.Events, entry => entry.Kind == GoalEventKind.UseLogged);
        Assert.Equal(1, use.Uses);
        Assert.Equal(0, use.Limit);
        Assert.Equal(image, use.ImageId);
        Assert.False(use.IsMine);

        Assert.Single(chat.Events, entry => entry.Kind == GoalEventKind.WindowRelapse);
    }

    [Fact]
    public async Task APeriodThatRanOutWithinItsLimitIsKept()
    {
        var created = await CreatedDontAsync(limit: 1);
        await LogUseAsync(Client, created.Id);

        Clock.Set(Q2ApiFactory.Now.AddDays(1));

        var detail = await (await Client.GetAsync($"/api/goals/{created.Id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        var kept = Assert.Single(detail.History);
        Assert.Equal(GoalInstanceStatus.Done, kept.Status);
        Assert.Equal(1, kept.Limit);
        Assert.Equal(1, detail.Goal.Streak);

        // The owner's daily flame counts the kept day.
        await Factory.WithDatabaseAsync(async database =>
            Assert.True(await database.DailyCheckIns.AnyAsync(
                checkIn => checkIn.PersonId == AutomatedTestSeed.CurrentPersonId && checkIn.Date == kept.DueOn,
                TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task TheProfileCountsARelapseOnItsOwnAndNotAsAMiss()
    {
        var before = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        var created = await CreatedDontAsync(limit: 0);
        await LogUseAsync(Client, created.Id);

        var after = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        Assert.Equal(before.Balance.Relapsed + 1, after.Balance.Relapsed);
        Assert.Equal(before.Balance.Missed, after.Balance.Missed);
    }

    [Fact]
    public async Task ADontIsNotPartOfTheDaysDeliveries()
    {
        var before = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        await CreatedDontAsync(limit: 0);

        var after = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        // The ring is about what was delivered today; a Don't is kept by its
        // period running out, so it is not an open share of it.
        Assert.Equal(before.Today, after.Today);
    }

    private async Task<int> NotificationCountAsync()
    {
        var count = 0;
        await Factory.WithDatabaseAsync(async database =>
            count = await database.Notifications.CountAsync(TestContext.Current.CancellationToken));
        return count;
    }
}
