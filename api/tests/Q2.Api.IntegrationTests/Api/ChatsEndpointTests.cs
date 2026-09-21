using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Conversations: reading them, writing into them, and who may.
/// </summary>
[Trait("Category", "Integration")]
public class ChatsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record SummaryDocument(
        Guid Id,
        ConversationKind Kind,
        string Name,
        string Initials,
        string AvatarColor,
        bool IsOnline,
        string? LastMessage,
        string? LastMessageSenderName,
        bool LastMessageIsMine,
        DateTimeOffset? LastMessageAt,
        int UnreadCount,
        GoalEventKind? LastEvent,
        Guid? GoalId,
        bool IsMyGoal,
        bool AwaitingMyVote);

    private sealed record ReactionDocument(KudosKind Kind, int Count, bool IsMine);

    private sealed record MessageDocument(
        Guid Id,
        Guid SenderId,
        string? SenderName,
        string Text,
        bool IsMine,
        DateTimeOffset SentAt,
        IReadOnlyList<ReactionDocument> Reactions);

    private sealed record WindowDocument(
        Guid Id,
        DateOnly StartsOn,
        DateOnly DueOn,
        DateTimeOffset DueAt,
        int RequiredProofs,
        int ConfirmedProofs,
        int RemainingProofs,
        GoalInstanceStatus Status);

    private sealed record PinnedGoalDocument(Guid Id, string Title, WindowDocument? Current, int Streak, bool IsMine);

    private sealed record EventDocument(
        string Key,
        GoalEventKind Kind,
        DateTimeOffset At,
        string? ActorName,
        bool IsMine,
        int? Streak,
        int? ConfirmedProofs,
        int? RequiredProofs,
        DateOnly? Until,
        ProofDocument? Proof);

    private sealed record ThreadDocument(
        Guid Id,
        ConversationKind Kind,
        string Name,
        int MemberCount,
        PinnedGoalDocument? PinnedGoal,
        IReadOnlyList<MessageDocument> Messages,
        IReadOnlyList<EventDocument> Events);

    private async Task<IReadOnlyList<SummaryDocument>> ListAsync(string query = "") =>
        await (await Client.GetAsync($"/api/chats{query}", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

    private async Task<ThreadDocument> ThreadAsync(Guid id) =>
        await ThreadAsync(Client, id);

    private static async Task<ThreadDocument> ThreadAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/chats/{id}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();

    private static async Task<IReadOnlyList<SummaryDocument>> ListAsync(HttpClient client) =>
        await (await client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

    private async Task<SummaryDocument> DirectRowAsync() =>
        (await ListAsync()).Single(chat => chat.Id == AutomatedTestSeed.DirectConversationId);

    [Fact]
    public async Task TheListOnlyContainsConversationsYouAreIn()
    {
        var chats = await ListAsync();

        Assert.Equal(
            [AutomatedTestSeed.DirectConversationId, AutomatedTestSeed.SharedGoalConversationId],
            chats.Select(chat => chat.Id).Order());
        Assert.DoesNotContain(chats, chat => chat.Id == AutomatedTestSeed.ForeignConversationId);
    }

    [Fact]
    public async Task ADirectConversationIsNamedAfterTheOtherPerson()
    {
        var chat = await DirectRowAsync();

        Assert.Equal(ConversationKind.Direct, chat.Kind);
        Assert.Equal("Test Person Two", chat.Name);
        Assert.Equal("T2", chat.Initials);
    }

    [Fact]
    public async Task TheListShowsWhatIsUnread()
    {
        Assert.Equal(1, (await DirectRowAsync()).UnreadCount);
    }

    [Fact]
    public async Task OpeningAThreadMarksItRead()
    {
        await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        // Opening a conversation *is* reading it; a separate call the client
        // could forget would leave a badge on a chat somebody is looking at.
        Assert.Equal(0, (await DirectRowAsync()).UnreadCount);
    }

    [Fact]
    public async Task AGoalsConversationCarriesItsGoal()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.SharedGoalConversationId);

        Assert.Equal(ConversationKind.Goal, thread.Kind);
        Assert.Equal("Automated test: shared active goal", thread.Name);

        // The owner and the one friend invited to check it.
        Assert.Equal(2, thread.MemberCount);

        Assert.NotNull(thread.PinnedGoal);
        Assert.Equal(AutomatedTestSeed.ActiveGoalId, thread.PinnedGoal.Id);
        Assert.True(thread.PinnedGoal.IsMine);

        // The banner says what is left of the open window rather than a
        // percentage of a counter that no longer exists.
        Assert.NotNull(thread.PinnedGoal.Current);
        Assert.Equal(1, thread.PinnedGoal.Current.RemainingProofs);
        Assert.Equal(2, thread.PinnedGoal.Streak);
    }

    [Fact]
    public async Task ADirectConversationIsAboutNoGoal()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        Assert.Null(thread.PinnedGoal);
        Assert.Empty(thread.Events);
    }

    [Fact]
    public async Task AGoalsConversationIsListedAsTheOwnersOnOneSideAndAFriendsOnTheOther()
    {
        var mine = (await ListAsync()).Single(chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId);

        Assert.Equal(ConversationKind.Goal, mine.Kind);
        Assert.Equal("Automated test: shared active goal", mine.Name);
        Assert.Equal(AutomatedTestSeed.ActiveGoalId, mine.GoalId);
        Assert.True(mine.IsMyGoal);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = (await ListAsync(friend)).Single(chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId);

        Assert.False(theirs.IsMyGoal);
        Assert.False(theirs.AwaitingMyVote);
    }

    [Fact]
    public async Task AGoalsConversationShowsItsHistoryBetweenTheMessages()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.SharedGoalConversationId);

        // Created ten days ago, then the two kept windows the seed laid down,
        // each with the streak it brought the goal to.
        Assert.Equal(
            [GoalEventKind.Created, GoalEventKind.WindowDone, GoalEventKind.WindowDone],
            thread.Events.Select(entry => entry.Kind));
        Assert.True(thread.Events[0].IsMine);
        Assert.Equal([1, 2], thread.Events.Skip(1).Select(entry => entry.Streak));
        Assert.Equal(thread.Events.OrderBy(entry => entry.At).Select(entry => entry.Key), thread.Events.Select(entry => entry.Key));
        Assert.Equal(thread.Events.Count, thread.Events.Select(entry => entry.Key).Distinct().Count());

        // And for the friend, the creation is somebody else's, by name.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await ThreadAsync(friend, AutomatedTestSeed.SharedGoalConversationId);

        Assert.False(theirs.Events[0].IsMine);
        Assert.Equal("Test Person One", theirs.Events[0].ActorName);
    }

    [Fact]
    public async Task APhotographArrivesInTheGoalsConversationAndIsVotedOnThere()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var row = (await ListAsync(friend)).Single(chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId);

        // The newest thing in the thread is the photograph, and it is waiting
        // for this friend — which the row says apart from "unread".
        Assert.Equal(GoalEventKind.ProofDelivered, row.LastEvent);
        Assert.Null(row.LastMessage);
        Assert.True(row.AwaitingMyVote);

        var thread = await ThreadAsync(friend, AutomatedTestSeed.SharedGoalConversationId);
        var delivered = thread.Events.Single(entry => entry.Kind == GoalEventKind.ProofDelivered);

        Assert.NotNull(delivered.Proof);
        Assert.Equal(proof.Id, delivered.Proof.Id);
        Assert.True(delivered.Proof.Votes.CanIVote);

        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Confirm }))
            .EnsureSuccessStatusCode();

        var after = await ThreadAsync(friend, AutomatedTestSeed.SharedGoalConversationId);
        var voted = after.Events.Single(entry => entry.Kind == GoalEventKind.ProofDelivered).Proof!;

        Assert.Equal(VoteValue.Confirm, voted.Votes.MyVote);
        Assert.False(voted.Votes.CanIVote);
        Assert.False((await ListAsync(friend)).Single(chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId).AwaitingMyVote);

        // The only friend on it believed it, so the window is kept, in the
        // thread as much as on the goal.
        Assert.Equal(GoalEventKind.WindowDone, after.Events[^1].Kind);
        Assert.Equal(3, after.Events[^1].Streak);

        // The owner cannot vote on their own photograph, in the thread either.
        var own = (await ThreadAsync(AutomatedTestSeed.SharedGoalConversationId))
            .Events.Single(entry => entry.Kind == GoalEventKind.ProofDelivered).Proof!;

        Assert.False(own.Votes.CanIVote);
    }

    [Fact]
    public async Task ADoubtIsCountedInTheThreadButNeverNamed()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        (await friend.PostJsonAsync($"/api/proofs/{proof.Id}/vote", new { value = VoteValue.Doubt }))
            .EnsureSuccessStatusCode();

        var response = await Client.GetAsync(
            $"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var shown = (await response.ReadAsync<ThreadDocument>())
            .Events.Single(entry => entry.Kind == GoalEventKind.ProofDelivered).Proof!;

        Assert.Equal(1, shown.Votes.DoubtCount);
        Assert.Empty(shown.Votes.ConfirmedBy);

        // The doubter is in the thread as a member, and still not attached to
        // the photograph anywhere in it.
        Assert.DoesNotContain(AutomatedTestSeed.FriendPersonId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APauseInTheThreadSaysUntilWhenButNeverWhy()
    {
        const string reason = "Automated test: a private reason for the pause";

        (await Client.PostJsonAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/pause", new { reason, days = 3 }))
            .EnsureSuccessStatusCode();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var response = await friend.GetAsync(
            $"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var paused = (await response.ReadAsync<ThreadDocument>())
            .Events.Single(entry => entry.Kind == GoalEventKind.PauseStarted);

        Assert.NotNull(paused.Until);
        Assert.Equal("Test Person One", paused.ActorName);
        Assert.DoesNotContain(reason, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGoalsConversationCannotBeLeft()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await friend.PostAsync(
            $"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}/leave",
            content: null,
            TestContext.Current.CancellationToken);

        // Leaving would quietly take a vote off the goal. Muting is the way to
        // stop hearing about it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(await ListAsync(friend), chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId);
    }

    [Fact]
    public async Task AMessageInAGoalsConversationNamesItsSender()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        (await friend.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}/messages",
            new { text = "Automated test: go on" })).EnsureSuccessStatusCode();

        var thread = await ThreadAsync(AutomatedTestSeed.SharedGoalConversationId);

        Assert.Equal("Test Person Two", thread.Messages.Single().SenderName);

        var row = (await ListAsync()).Single(chat => chat.Id == AutomatedTestSeed.SharedGoalConversationId);

        Assert.Equal("Automated test: go on", row.LastMessage);
        Assert.Equal("Test Person Two", row.LastMessageSenderName);
        Assert.Null(row.LastEvent);
    }

    [Fact]
    public async Task TheMaintenancePassOpensTheConversationAGoalIsMissing()
    {
        // A goal from before goals came with a conversation.
        await Factory.WithDatabaseAsync(async database =>
        {
            await database.Conversations
                .Where(conversation => conversation.GoalId == AutomatedTestSeed.ActiveGoalId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });

        using (var scope = Factory.Services.CreateScope())
        {
            var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider);
            await worker.RunOnceAsync(TestContext.Current.CancellationToken);
            await worker.RunOnceAsync(TestContext.Current.CancellationToken);
        }

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var opened = (await ListAsync(friend)).Single(chat => chat.GoalId == AutomatedTestSeed.ActiveGoalId);

        // Once, however often it runs, and with everybody on the goal in it —
        // but not for a goal nobody else is on.
        Assert.Contains(await ListAsync(), chat => chat.Id == opened.Id);
        Assert.DoesNotContain(await ListAsync(), chat => chat.GoalId == AutomatedTestSeed.GoalWithoutTargetDateId);
    }

    [Fact]
    public async Task MessagesComeBackOldestFirstAndSayWhichAreYours()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        Assert.Equal(thread.Messages.OrderBy(m => m.SentAt).Select(m => m.Id), thread.Messages.Select(m => m.Id));
        Assert.True(thread.Messages[0].IsMine);
        Assert.False(thread.Messages[1].IsMine);
    }

    [Fact]
    public async Task ADirectConversationDoesNotRepeatTheSendersName()
    {
        // The header already says who is speaking.
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        Assert.All(thread.Messages, message => Assert.Null(message.SenderName));
    }

    [Fact]
    public async Task SendingAMessageReturnsTheWholeThread()
    {
        var response = await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages",
            new { text = "Bis gleich!" });

        response.EnsureSuccessStatusCode();
        var thread = await response.ReadAsync<ThreadDocument>();

        Assert.Equal(3, thread.Messages.Count);
        Assert.Equal("Bis gleich!", thread.Messages[^1].Text);
        Assert.True(thread.Messages[^1].IsMine);
    }

    [Fact]
    public async Task AnEmptyMessageIsAValidationProblemRatherThanAnEmptyBubble()
    {
        var response = await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages",
            new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AReactionIsAddedAndTakenBackByTheSameCall()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);
        var theirs = thread.Messages.First(message => !message.IsMine);

        var added = await (await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages/{theirs.Id}/reactions",
            new { kind = nameof(KudosKind.Fire) })).ReadAsync<ThreadDocument>();

        Assert.Contains(
            added.Messages.Single(m => m.Id == theirs.Id).Reactions,
            reaction => reaction.Kind == KudosKind.Fire && reaction.IsMine);

        var removed = await (await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages/{theirs.Id}/reactions",
            new { kind = nameof(KudosKind.Fire) })).ReadAsync<ThreadDocument>();

        Assert.DoesNotContain(
            removed.Messages.Single(m => m.Id == theirs.Id).Reactions,
            reaction => reaction.Kind == KudosKind.Fire);
    }

    [Fact]
    public async Task AnUnknownReactionIsRejected()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        var response = await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages/{thread.Messages[0].Id}/reactions",
            new { kind = "pizza" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SomebodyElsesConversationIsNotFoundRatherThanForbidden()
    {
        // Telling somebody that a conversation exists but is not theirs is
        // itself a disclosure.
        var response = await Client.GetAsync(
            $"/api/chats/{AutomatedTestSeed.ForeignConversationId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WritingIntoSomebodyElsesConversationIsAlso404()
    {
        var response = await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.ForeignConversationId}/messages",
            new { text = "Hallo?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchingFiltersByTheNameTheRowActuallyShows()
    {
        Assert.Single(await ListAsync("?search=Person Two"));
        Assert.Empty(await ListAsync("?search=nobody-by-this-name"));
    }

    [Fact]
    public async Task StartingAChatWithAFriendOpensTheOneThatAlreadyExists()
    {
        // Idempotent on purpose: the message button appears on the friends
        // screen, on a feed row and on a goal's team list, and all three
        // have to land in the same thread rather than making a new empty one.
        var response = await Client.PostJsonAsync(
            "/api/chats/direct",
            new { personId = AutomatedTestSeed.FriendPersonId });

        response.EnsureSuccessStatusCode();

        var thread = await response.ReadAsync<ThreadDocument>();
        Assert.Equal(AutomatedTestSeed.DirectConversationId, thread.Id);
        Assert.NotEmpty(thread.Messages);
    }

    [Fact]
    public async Task StartingAChatWithAFriendWhoHasNoThreadYetCreatesOne()
    {
        // Become friends first: the seed has this person waiting on an answer.
        await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        var response = await Client.PostJsonAsync(
            "/api/chats/direct",
            new { personId = AutomatedTestSeed.RequestingPersonId });

        response.EnsureSuccessStatusCode();

        var thread = await response.ReadAsync<ThreadDocument>();
        Assert.Empty(thread.Messages);
        Assert.Equal(2, thread.MemberCount);

        // And it is the other person's thread too.
        var theirs = await ClientForAsync(AutomatedTestSeed.RequesterEmail);
        var theirList = await (await theirs.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

        Assert.Contains(theirList, chat => chat.Id == thread.Id);
    }

    [Fact]
    public async Task AChatCannotBeStartedWithSomebodyWhoIsNotAFriend()
    {
        // Otherwise a stranger can open a thread with anybody, which is how a
        // self-care app becomes a place people get shouted at.
        var response = await Client.PostJsonAsync(
            "/api/chats/direct",
            new { personId = AutomatedTestSeed.UnconnectedPersonId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AChatCannotBeStartedWithSomebodyWhoDoesNotExist()
    {
        var response = await Client.PostJsonAsync("/api/chats/direct", new { personId = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AGroupCanBeCreatedFromFriendsAndShowsUpForAllOfThem()
    {
        await Client.PostAsync(
            $"/api/friends/{AutomatedTestSeed.RequestingPersonId}/accept",
            content: null,
            TestContext.Current.CancellationToken);

        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: new group",
            emoji = "\U0001F331",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId, AutomatedTestSeed.RequestingPersonId },
        });

        response.EnsureSuccessStatusCode();

        var thread = await response.ReadAsync<ThreadDocument>();
        Assert.Equal(ConversationKind.Group, thread.Kind);
        Assert.Equal("Automated test: new group", thread.Name);
        Assert.Equal(3, thread.MemberCount);

        var theirs = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirList = await (await theirs.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

        Assert.Contains(theirList, chat => chat.Id == thread.Id);
    }

    [Fact]
    public async Task AGroupCannotContainSomebodyYouAreNotFriendsWith()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: not allowed",
            memberIds = new[] { AutomatedTestSeed.UnconnectedPersonId },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AGroupIsNeverAboutAGoal()
    {
        // A client that still sends the old field gets a free group: a goal has
        // its own conversation, and a second one about it would split it.
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: shared goal group",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
            goalId = AutomatedTestSeed.ActiveGoalId,
        });

        response.EnsureSuccessStatusCode();

        var thread = await response.ReadAsync<ThreadDocument>();

        Assert.Equal(ConversationKind.Group, thread.Kind);
        Assert.Null(thread.PinnedGoal);
    }

    [Fact]
    public async Task AGoalsConversationOnlyShowsItsGoalToPeopleOnTheGoal()
    {
        var conversationId = Guid.CreateVersion7();

        // Inconsistent on purpose: the friend is in the conversation of a goal
        // they are not on. Nothing in the app can produce it; the read has to
        // hold anyway.
        await Factory.WithDatabaseAsync(async database =>
        {
            var conversation = Conversation.CreateForGoal(
                conversationId,
                AutomatedTestSeed.GoalWithoutTargetDateId,
                Q2ApiFactory.Now);
            conversation.AddParticipant(Guid.CreateVersion7(), AutomatedTestSeed.CurrentPersonId);
            conversation.AddParticipant(Guid.CreateVersion7(), AutomatedTestSeed.FriendPersonId);

            database.Conversations.Add(conversation);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var mine = await ThreadAsync(conversationId);

        Assert.NotNull(mine.PinnedGoal);
        Assert.NotEmpty(mine.Events);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var friendsThread = await ThreadAsync(friend, conversationId);

        Assert.Null(friendsThread.PinnedGoal);
        Assert.Empty(friendsThread.Events);
        Assert.DoesNotContain(await ListAsync(friend), chat => chat.Id == conversationId);
    }

    [Fact]
    public async Task AGroupIconMustBeOneOfTheAvailableIcons()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: unknown icon",
            icon = "not-an-icon",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AGroupNeedsSomebodyElseInIt()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: nobody here",
            memberIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AGroupNeedsAName()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "   ",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LeavingAGroupTakesItOffYourListAndLeavesItOnTheirs()
    {
        var created = await (await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: leaving",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
        })).ReadAsync<ThreadDocument>();

        var response = await Client.PostAsync(
            $"/api/chats/{created.Id}/leave",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await ListAsync(), chat => chat.Id == created.Id);

        var theirs = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirList = await (await theirs.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

        // Their conversation is not deleted because somebody else walked out.
        Assert.Contains(theirList, chat => chat.Id == created.Id);
    }

    [Fact]
    public async Task ADirectConversationCannotBeLeft()
    {
        // There would be nothing left of it. Leaving is a group idea.
        var response = await Client.PostAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/leave",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AConversationSomebodyElseIsInCannotBeLeft()
    {
        var response = await Client.PostAsync(
            $"/api/chats/{AutomatedTestSeed.ForeignConversationId}/leave",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMessageArrivesOnTheOtherPersonsScreenAsUnread()
    {
        await Client.PostJsonAsync(
            $"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages",
            new { text = "Automated test: for the other side" });

        var theirs = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirList = await (await theirs.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

        var thread = theirList.Single(chat => chat.Id == AutomatedTestSeed.DirectConversationId);

        Assert.Equal("Automated test: for the other side", thread.LastMessage);
        Assert.False(thread.LastMessageIsMine);
        Assert.True(thread.UnreadCount > 0);
    }
}
