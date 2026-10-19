using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Editing a running goal, over real HTTP against a real database, and from
/// both ends: the owner who changes it, and the checkers who can object.
/// </summary>
/// <remarks>
/// The rules themselves are unit-tested in <c>GoalEditTests</c>; these are the
/// things only the whole pipeline can show — who may edit, who is refused,
/// who gets into and out of the goal's conversation, and that the nightly job
/// applies a change nobody is looking at
/// (docs/adr/0044-editing-a-running-goal.md).
/// </remarks>
[Trait("Category", "Integration")]
public class GoalEditEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private static string GoalUrl => $"/api/goals/{AutomatedTestSeed.ActiveGoalId}";

    private sealed record ScheduleDocument(ScheduleKind Kind, int? EveryDays, int? Times, QuotaPeriod? Period);

    private sealed record WindowDocument(Guid Id, DateOnly StartsOn, DateOnly DueOn, int RequiredProofs, GoalInstanceStatus Status);

    private sealed record PersonDocument(Guid Id, string DisplayName);

    private sealed record ChangeDocument(
        Guid Id,
        GoalChangeStatus Status,
        bool Objectable,
        DateOnly? AppliesOn,
        string? TitleTo,
        ScheduleDocument? ScheduleFrom,
        ScheduleDocument? ScheduleTo,
        IReadOnlyList<PersonDocument> CheckersAdded,
        IReadOnlyList<PersonDocument> CheckersRemoved,
        int VetoCount,
        int VetoesRequired,
        bool VetoedByMe,
        bool CanVeto);

    private sealed record GoalDocument(
        Guid Id,
        string Title,
        ScheduleDocument Schedule,
        WindowDocument? Current,
        int Streak,
        IReadOnlyList<PersonDocument> Participants,
        ChangeDocument? PendingChange);

    private sealed record GoalDetailDocument(GoalDocument Goal, Guid? ConversationId);

    private sealed record PreviewDocument(ChangeDocument? Change, bool WithdrawsPending);

    private sealed record EventDocument(string Key, GoalEventKind Kind, ChangeDocument? Change);

    private sealed record ChatDocument(string Name, IReadOnlyList<EventDocument> Events);

    private sealed record ChatRowDocument(Guid Id);

    private sealed record LineDocument(NotificationKind Kind, Guid? TargetId);

    private sealed record ValidationDocument(IReadOnlyDictionary<string, string[]> Errors);

    [Fact]
    public async Task ATitleChangesAtOnceAndTheConversationShowsIt()
    {
        var response = await Client.PatchJsonAsync(GoalUrl, new { title = "Automated test: renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Automated test: renamed", (await response.ReadAsync<GoalDocument>()).Title);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var chat = await ChatAsync(friend);

        // The conversation is called whatever the goal is called, and says
        // what changed — derived from the edit, never written as a message.
        Assert.Equal("Automated test: renamed", chat.Name);
        var line = Assert.Single(chat.Events, entry => entry.Kind == GoalEventKind.Edited);
        Assert.Equal("Automated test: renamed", line.Change!.TitleTo);
    }

    /// <summary>
    /// The heart of it: the open window keeps its deadline, the streak is not
    /// touched, the change waits a day for objections, and then the new
    /// schedule governs the windows that follow — applied by the nightly job,
    /// with nobody looking.
    /// </summary>
    [Fact]
    public async Task AScheduleChangeKeepsTheOpenWindowAndTheStreakAndGovernsTheNext()
    {
        var before = await GoalAsync(Client);

        var edited = await (await Client.PatchJsonAsync(GoalUrl, new { schedule = new { kind = "Interval", everyDays = 2 } }))
            .ReadAsync<GoalDocument>();

        Assert.Equal(before.Current!.DueOn, edited.Current!.DueOn);
        Assert.Equal(before.Streak, edited.Streak);
        Assert.Equal(1, edited.Schedule.EveryDays);

        var pending = edited.PendingChange!;
        Assert.Equal(GoalChangeStatus.Pending, pending.Status);
        Assert.True(pending.Objectable);
        Assert.Equal(2, pending.ScheduleTo!.EveryDays);
        Assert.Equal(Q2ApiFactory.Today.AddDays(2), pending.AppliesOn);

        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(2));

        var applied = await GoalAsync(Client);
        Assert.Null(applied.PendingChange);
        Assert.Equal(2, applied.Schedule.EveryDays);

        // Every other day from the last window: none on the day it applied,
        // the next one the day after.
        Assert.Null(applied.Current);

        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(3));

        Assert.Equal(Q2ApiFactory.Today.AddDays(3), (await GoalAsync(Client)).Current!.StartsOn);
    }

    /// <summary>
    /// Asking more of yourself needs nobody's consent: no objection, and it
    /// applies with the next window.
    /// </summary>
    [Fact]
    public async Task AStricterChangeAppliesWithTheNextWindowWithoutObjection()
    {
        var url = $"/api/goals/{AutomatedTestSeed.QuotaGoalId}";

        var edited = await (await Client.PatchJsonAsync(url, new { schedule = new { kind = "Times", times = 4, period = "Week" } }))
            .ReadAsync<GoalDocument>();

        Assert.False(edited.PendingChange!.Objectable);
        Assert.Equal(3, edited.Current!.RequiredProofs);

        // The next Monday.
        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(7));

        var next = await (await Client.GetAsync(url, TestContext.Current.CancellationToken)).ReadAsync<GoalDetailDocument>();
        Assert.Equal(4, next.Goal.Current!.RequiredProofs);
        Assert.Null(next.Goal.PendingChange);
    }

    [Fact]
    public async Task EnoughObjectionsLiftAnEasierChange()
    {
        await BefriendRequesterAsync();
        await Client.PatchJsonAsync(GoalUrl, new { participantIds = new[] { AutomatedTestSeed.FriendPersonId, AutomatedTestSeed.RequestingPersonId } });
        await Client.PatchJsonAsync(GoalUrl, new { schedule = new { kind = "Interval", everyDays = 3 } });

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var requester = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        var seen = (await GoalAsync(friend)).PendingChange!;
        Assert.True(seen.CanVeto);
        Assert.Equal(2, seen.VetoesRequired);

        var once = await (await friend.PostJsonAsync($"{GoalUrl}/change/veto", new { })).ReadAsync<GoalDocument>();
        Assert.Equal(1, once.PendingChange!.VetoCount);
        Assert.True(once.PendingChange.VetoedByMe);

        var lifted = await (await requester.PostJsonAsync($"{GoalUrl}/change/veto", new { })).ReadAsync<GoalDocument>();
        Assert.Null(lifted.PendingChange);

        // The owner hears the consequence, from nobody in particular.
        Assert.Contains(await BellAsync(Client), line => line.Kind == NotificationKind.GoalChangeLifted);

        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(3));

        Assert.Equal(1, (await GoalAsync(Client)).Schedule.EveryDays);

        var chat = await ChatAsync(friend);
        Assert.Contains(chat.Events, entry => entry.Kind == GoalEventKind.ChangeOverturned);
    }

    /// <summary>
    /// The people who can object are told that there is something to object
    /// to; a stricter change is only a line in the conversation.
    /// </summary>
    [Fact]
    public async Task TheCheckersAreToldAboutAnEasierChangeOnly()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await Client.PatchJsonAsync(GoalUrl, new { description = "Mindestens zwanzig Minuten." });

        Assert.Contains(
            await BellAsync(friend),
            line => line.Kind == NotificationKind.GoalChangeProposed && line.TargetId == AutomatedTestSeed.ActiveGoalId);
    }

    [Fact]
    public async Task OnlyTheOwnerEdits()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.PatchJsonAsync(GoalUrl, new { title = "Mine now" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("Mine now", (await GoalAsync(Client)).Title);
    }

    [Fact]
    public async Task TheOwnerCannotObjectToTheirOwnChange()
    {
        await Client.PatchJsonAsync(GoalUrl, new { schedule = new { kind = "Interval", everyDays = 2 } });

        var response = await Client.PostJsonAsync($"{GoalUrl}/change/veto", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemovingTheLastCheckerIsRefused()
    {
        var empty = await Client.PatchJsonAsync(GoalUrl, new { participantIds = Array.Empty<Guid>() });
        var onlyMe = await Client.PatchJsonAsync(GoalUrl, new { participantIds = new[] { AutomatedTestSeed.CurrentPersonId } });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, onlyMe.StatusCode);
        Assert.True((await onlyMe.ReadAsync<ValidationDocument>()).Errors.ContainsKey("ParticipantIds"));
        Assert.Single((await GoalAsync(Client)).Participants);
    }

    [Fact]
    public async Task AddingSomebodyWhoIsNotAFriendIsRefused()
    {
        var response = await Client.PatchJsonAsync(
            GoalUrl,
            new { participantIds = new[] { AutomatedTestSeed.FriendPersonId, AutomatedTestSeed.UnconnectedPersonId } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync<ValidationDocument>()).Errors.ContainsKey("ParticipantIds"));
        Assert.Single((await GoalAsync(Client)).Participants);
    }

    [Fact]
    public async Task ALimitOnADoIsRefused()
    {
        var response = await Client.PatchJsonAsync(GoalUrl, new { limit = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync<ValidationDocument>()).Errors.ContainsKey("Limit"));
    }

    /// <summary>
    /// Somebody added is one of the people who decide the goal from that
    /// moment: they see it, they are in its conversation, and they vote on the
    /// next photograph.
    /// </summary>
    [Fact]
    public async Task AnAddedCheckerSeesTheGoalAndItsConversationAndVotesOnTheNextProof()
    {
        await BefriendRequesterAsync();

        var edited = await (await Client.PatchJsonAsync(
                GoalUrl,
                new { participantIds = new[] { AutomatedTestSeed.FriendPersonId, AutomatedTestSeed.RequestingPersonId } }))
            .ReadAsync<GoalDocument>();

        Assert.Equal(2, edited.Participants.Count);
        Assert.Null(edited.PendingChange);

        var requester = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync(GoalUrl, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Contains(await ChatRowsAsync(requester), row => row.Id == AutomatedTestSeed.SharedGoalConversationId);
        Assert.Contains(await BellAsync(requester), line => line.Kind == NotificationKind.GoalInvitation);

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var vote = await requester.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm });

        Assert.Equal(HttpStatusCode.OK, vote.StatusCode);
    }

    [Fact]
    public async Task ARemovedCheckerLosesAccessOnceTheChangeApplies()
    {
        await BefriendRequesterAsync();
        await Client.PatchJsonAsync(GoalUrl, new { participantIds = new[] { AutomatedTestSeed.FriendPersonId, AutomatedTestSeed.RequestingPersonId } });

        var edited = await (await Client.PatchJsonAsync(GoalUrl, new { participantIds = new[] { AutomatedTestSeed.RequestingPersonId } }))
            .ReadAsync<GoalDocument>();

        Assert.True(edited.PendingChange!.Objectable);
        Assert.Equal(AutomatedTestSeed.FriendPersonId, Assert.Single(edited.PendingChange.CheckersRemoved).Id);

        // Still on it while the change waits, and able to object to it.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        Assert.True((await GoalAsync(friend)).PendingChange!.CanVeto);

        await RunMaintenanceAtAsync(Q2ApiFactory.Now.AddDays(2));

        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync(GoalUrl, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await friend.GetAsync($"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.DoesNotContain(await ChatRowsAsync(friend), row => row.Id == AutomatedTestSeed.SharedGoalConversationId);

        // The others still read who left, by name.
        var chat = await ChatAsync(Client);
        var applied = Assert.Single(chat.Events, entry => entry.Kind == GoalEventKind.ChangeApplied);
        Assert.Equal("Test Person Two", Assert.Single(applied.Change!.CheckersRemoved).DisplayName);
    }

    [Fact]
    public async Task ThePreviewSaysWhatWouldHappenWithoutSavingIt()
    {
        var response = await Client.PostJsonAsync(
            $"{GoalUrl}/change/preview",
            new { title = "Automated test: previewed", schedule = new { kind = "Interval", everyDays = 2 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.ReadAsync<PreviewDocument>();
        Assert.Equal(GoalChangeStatus.Pending, preview.Change!.Status);
        Assert.True(preview.Change.Objectable);
        Assert.Equal(Q2ApiFactory.Today.AddDays(2), preview.Change.AppliesOn);
        Assert.Equal("Automated test: previewed", preview.Change.TitleTo);

        var goal = await GoalAsync(Client);
        Assert.NotEqual("Automated test: previewed", goal.Title);
        Assert.Null(goal.PendingChange);
    }

    [Fact]
    public async Task TheOwnerCanWithdrawAWaitingChange()
    {
        await Client.PatchJsonAsync(GoalUrl, new { schedule = new { kind = "Interval", everyDays = 2 } });

        var response = await Client.DeleteAsync($"{GoalUrl}/change", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadAsync<GoalDocument>()).PendingChange);
        Assert.Contains((await ChatAsync(Client)).Events, entry => entry.Kind == GoalEventKind.ChangeWithdrawn);

        var again = await Client.DeleteAsync($"{GoalUrl}/change", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task AStoppedGoalCannotBeEdited()
    {
        await Client.PostJsonAsync($"{GoalUrl}/close", new { completed = false });

        var response = await Client.PatchJsonAsync(GoalUrl, new { title = "Too late" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task BefriendRequesterAsync() =>
        (await Client.PostJsonAsync($"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept", new { }))
            .EnsureSuccessStatusCode();

    private static async Task<GoalDocument> GoalAsync(HttpClient client) =>
        (await (await client.GetAsync(GoalUrl, TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>()).Goal;

    private static async Task<ChatDocument> ChatAsync(HttpClient client) =>
        await (await client.GetAsync($"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ChatDocument>();

    private static async Task<IReadOnlyList<ChatRowDocument>> ChatRowsAsync(HttpClient client) =>
        await (await client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ChatRowDocument>>();

    private static async Task<IReadOnlyList<LineDocument>> BellAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<LineDocument>>();

    private async Task RunMaintenanceAtAsync(DateTimeOffset now)
    {
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Set(now);

        using var scope = Factory.Services.CreateScope();
        var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        // The clock is shared with every other test in the assembly.
        ((FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>()).Reset();

        await base.DisposeAsync();
    }
}
