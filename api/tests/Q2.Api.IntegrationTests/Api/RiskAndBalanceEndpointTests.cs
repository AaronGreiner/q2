using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The shame half: the warning that goes out before the deadline, and the
/// record that is left afterwards.
/// </summary>
/// <remarks>
/// These are stage 5's two completion criteria, and both are the sort of rule
/// that is easy to state and easy to lose. The warning has to be rare — once
/// per window, never before the evening — or people mute it and it never works
/// again. The balance has to be scoped to the person asking, or somebody's
/// failures end up on a stranger's screen.
/// </remarks>
[Trait("Category", "Integration")]
public class RiskAndBalanceEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record BalanceDocument(int Done, int Missed);

    private sealed record PersonDocument(Guid Id, string DisplayName, string Handle);

    private sealed record PersonProfileDocument(
        PersonDocument Person,
        FriendshipState State,
        int Streak,
        BalanceDocument Balance,
        int SharedGoals);

    private sealed record RiskDocument(RiskReason Reason, int MissingProofs, int RequiredProofs, int RemainingDays);

    private sealed record WindowDocument(Guid Id, int RequiredProofs, int ConfirmedProofs, GoalInstanceStatus Status);

    private sealed record GoalDocument(Guid Id, string Title, WindowDocument? Current, RiskDocument? Risk);

    private sealed record ProfileDocument(BalanceDocument Balance, IReadOnlyList<GoalDocument> AtRisk);

    private async Task<PersonProfileDocument> PersonAsync(HttpClient client, Guid personId) =>
        await (await client.GetAsync($"/api/people/{personId}", TestContext.Current.CancellationToken))
            .ReadAsync<PersonProfileDocument>();

    private async Task<ProfileDocument> ProfileAsync(HttpClient client) =>
        await (await client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

    // -- the balance, scoped to who is asking -------------------------------

    /// <summary>
    /// Stage 5's second completion criterion, and the one that would be a
    /// disclosure if it were wrong.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesBalanceCoversOnlyWhatYouShare()
    {
        // The seed's shared goal is the only one the two of them are both on;
        // the owner has three more that are nobody else's business.
        var seen = await PersonAsync(await ClientForAsync(AutomatedTestSeed.FriendEmail), AutomatedTestSeed.CurrentPersonId);
        var own = await ProfileAsync(Client);

        Assert.Equal(1, seen.SharedGoals);

        // Two delivered windows on the shared goal, and nothing else of theirs.
        Assert.Equal(2, seen.Balance.Done);

        // The owner's own view is wider, and that difference is the whole rule.
        Assert.True(own.Balance.Done > seen.Balance.Done);
    }

    /// <summary>
    /// The angle that catches the tempting wrong rule.
    /// </summary>
    /// <remarks>
    /// A balance counted over "goals either of us owns and the other is on"
    /// looks fairer and passes the obvious test. It puts the *viewer's* windows
    /// under the *subject's* name, which only shows from here: a goal the
    /// viewer owns and the subject was invited to is not a record of anything
    /// the subject did.
    /// </remarks>
    [Fact]
    public async Task AGoalYouOwnIsNotPartOfSomebodyElsesRecord()
    {
        // The seed's shared goal is the signed-in person's own, and the friend
        // is a participant on it. Looking at *them*, none of it is theirs.
        var seen = await PersonAsync(Client, AutomatedTestSeed.FriendPersonId);

        Assert.Equal(0, seen.SharedGoals);
        Assert.Equal(0, seen.Balance.Done);
        Assert.Equal(0, seen.Balance.Missed);
    }

    [Fact]
    public async Task ANonFriendSeesNothingOfSomebodysRecord()
    {
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        var seen = await PersonAsync(stranger, AutomatedTestSeed.CurrentPersonId);

        // Zero shared goals, and therefore a zero balance — which is not the
        // same statement as "this person has never missed anything", and the
        // count is what lets a screen tell the two apart.
        Assert.Equal(0, seen.SharedGoals);
        Assert.Equal(0, seen.Balance.Done);
        Assert.Equal(0, seen.Balance.Missed);
    }

    [Fact]
    public async Task ThePublicPartOfAProfileIsStillPublic()
    {
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        var seen = await PersonAsync(stranger, AutomatedTestSeed.CurrentPersonId);

        // Name, handle and streak are what a search result already shows.
        // Hiding them here would make the screen behind a search result emptier
        // than the result itself.
        Assert.Equal("@test.one", seen.Person.Handle);
        Assert.False(string.IsNullOrWhiteSpace(seen.Person.DisplayName));
    }

    [Fact]
    public async Task YourOwnProfileThroughThatRouteIsYourWholeRecord()
    {
        var mine = await PersonAsync(Client, AutomatedTestSeed.CurrentPersonId);
        var own = await ProfileAsync(Client);

        Assert.Equal(FriendshipState.Self, mine.State);
        Assert.Equal(own.Balance.Done, mine.Balance.Done);
        Assert.Equal(own.Balance.Missed, mine.Balance.Missed);
    }

    [Fact]
    public async Task APersonWhoDoesNotExistIs404()
    {
        var response = await Client.GetAsync(
            $"/api/people/{Guid.CreateVersion7()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- the evening warning ------------------------------------------------

    /// <summary>
    /// Stage 5's first completion criterion: a friend finds out in the evening
    /// that somebody is about to miss — at most once, and never before eight.
    /// </summary>
    [Fact]
    public async Task FriendsAreWarnedOnceInTheEveningAndNotBefore()
    {
        // Half past six, local. Nothing has been delivered and nothing is said:
        // an open window in the afternoon is an ordinary day.
        await RunMaintenanceAtAsync(LocalHour(18, 30));
        Assert.Empty(await WarningsAsync());

        // Half past eight. Now it is worth saying — for every window that is
        // actually at risk, which in this seed is more than one.
        await RunMaintenanceAtAsync(LocalHour(20, 30));
        var warned = await WarningsAsync();

        var shared = Assert.Single(warned, warning => warning.SourceId == AutomatedTestSeed.ActiveGoalId);
        Assert.Equal(AutomatedTestSeed.CurrentPersonId, shared.ActorPersonId);
        Assert.Equal(1, shared.Amount);

        // And again, and again. A warning that repeats is a warning that gets
        // muted, after which it never works for anything — so the count must
        // not move however often the job runs.
        await RunMaintenanceAtAsync(LocalHour(21, 0));
        await RunMaintenanceAtAsync(LocalHour(22, 0));

        Assert.Equal(warned.Count, (await WarningsAsync()).Count);
        Assert.Single(await WarningsAsync(), warning => warning.SourceId == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task NobodyIsWarnedAboutAWindowThatHasBeenDeliveredInto()
    {
        // A photograph under a running vote counts as delivered. Warning about
        // the person who has just handed in is the fastest way to teach
        // everybody to ignore this.
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        await RunMaintenanceAtAsync(LocalHour(21, 0));

        Assert.DoesNotContain(
            await WarningsAsync(),
            warning => warning.SourceId == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task AMissedWindowIsCountedRatherThanAnnounced()
    {
        // "Kein Nachtreten": a finished failure gets no feed entry at all. The
        // balance is where it shows, and that is the only place it should.
        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(3));

        await Factory.WithDatabaseAsync(async database =>
        {
            var kinds = await database.ActivityEvents
                .AsNoTracking()
                .Select(activity => activity.Kind)
                .Distinct()
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.DoesNotContain(kinds, kind => kind.ToString().Contains("Missed", StringComparison.Ordinal));
        });

        var own = await ProfileAsync(Client);
        Assert.True(own.Balance.Missed > 0);
    }

    /// <summary>
    /// The owner's own view of the same fact. Their friends hear about it once;
    /// they see it for as long as it is true.
    /// </summary>
    [Fact]
    public async Task TheOwnerSeesTheirOwnWindowAtRiskInTheEvening()
    {
        var afternoon = await ProfileAsync(Client);
        Assert.Empty(afternoon.AtRisk);

        SetClock(LocalHour(20, 30));

        var evening = await ProfileAsync(Client);

        Assert.NotEmpty(evening.AtRisk);
        Assert.All(evening.AtRisk, goal => Assert.NotNull(goal.Risk));
        Assert.Contains(evening.AtRisk, goal => goal.Risk!.Reason == RiskReason.LastDay);
    }

    // -- helpers ------------------------------------------------------------

    /// <summary>An instant that is <paramref name="hour"/> in Europe/Berlin on the fixture's day.</summary>
    private static DateTimeOffset LocalHour(int hour, int minute)
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var offset = berlin.GetUtcOffset(Q2ApiFactory.Now);

        return new DateTimeOffset(Q2ApiFactory.Today.ToDateTime(new TimeOnly(hour, minute)), offset);
    }

    private async Task<IReadOnlyList<ActivityEvent>> WarningsAsync()
    {
        List<ActivityEvent> warnings = [];

        await Factory.WithDatabaseAsync(async database =>
            warnings = await database.ActivityEvents
                .AsNoTracking()
                .Where(activity => activity.Kind == ActivityKind.WindowAtRisk)
                .ToListAsync(TestContext.Current.CancellationToken));

        return warnings;
    }

    private void SetClock(DateTimeOffset now) =>
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(now);

    private async Task RunMaintenanceAtAsync(DateTimeOffset now)
    {
        SetClock(now);

        using var scope = Factory.Services.CreateScope();
        var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        // The clock is shared with every other test in the assembly, and these
        // are the only ones that move it. `ResetAsync` puts it back as well,
        // for the case where one of these fails before getting here.
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Reset();

        await base.DisposeAsync();
    }
}
