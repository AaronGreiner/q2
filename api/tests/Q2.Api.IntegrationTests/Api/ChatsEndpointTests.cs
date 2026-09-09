using System.Net;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
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
        int UnreadCount);

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

    private sealed record PinnedGoalDocument(Guid Id, string Title, WindowDocument? Current, int Streak);

    private sealed record ThreadDocument(
        Guid Id,
        ConversationKind Kind,
        string Name,
        int MemberCount,
        PinnedGoalDocument? PinnedGoal,
        IReadOnlyList<MessageDocument> Messages);

    private async Task<IReadOnlyList<SummaryDocument>> ListAsync(string query = "") =>
        await (await Client.GetAsync($"/api/chats{query}", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>();

    private async Task<ThreadDocument> ThreadAsync(Guid id) =>
        await (await Client.GetAsync($"/api/chats/{id}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();

    [Fact]
    public async Task TheListOnlyContainsConversationsYouAreIn()
    {
        var chats = await ListAsync();

        Assert.Single(chats);
        Assert.DoesNotContain(chats, chat => chat.Id == AutomatedTestSeed.ForeignConversationId);
    }

    [Fact]
    public async Task ADirectConversationIsNamedAfterTheOtherPerson()
    {
        var chat = (await ListAsync()).Single();

        Assert.Equal(ConversationKind.Direct, chat.Kind);
        Assert.Equal("Test Person Two", chat.Name);
        Assert.Equal("T2", chat.Initials);
    }

    [Fact]
    public async Task TheListShowsWhatIsUnread()
    {
        Assert.Equal(1, (await ListAsync()).Single().UnreadCount);
    }

    [Fact]
    public async Task OpeningAThreadMarksItRead()
    {
        await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        // Opening a conversation *is* reading it; a separate call the client
        // could forget would leave a badge on a chat somebody is looking at.
        Assert.Equal(0, (await ListAsync()).Single().UnreadCount);
    }

    [Fact]
    public async Task AThreadCarriesItsPinnedGoal()
    {
        var thread = await ThreadAsync(AutomatedTestSeed.DirectConversationId);

        Assert.NotNull(thread.PinnedGoal);
        Assert.Equal(AutomatedTestSeed.ActiveGoalId, thread.PinnedGoal.Id);

        // The banner says what is left of the open window rather than a
        // percentage of a counter that no longer exists.
        Assert.NotNull(thread.PinnedGoal.Current);
        Assert.Equal(1, thread.PinnedGoal.Current.RemainingProofs);
        Assert.Equal(2, thread.PinnedGoal.Streak);
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
    public async Task AGroupCannotExposeAPrivateGoalToItsMembers()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: private goal leak",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
            goalId = AutomatedTestSeed.GoalWithoutTargetDateId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AGroupCanPinAGoalEveryMemberMaySee()
    {
        var response = await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: shared goal group",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
            goalId = AutomatedTestSeed.ActiveGoalId,
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            AutomatedTestSeed.ActiveGoalId,
            (await response.ReadAsync<ThreadDocument>()).PinnedGoal?.Id);
    }

    [Fact]
    public async Task AThreadOnlyReturnsAPinnedGoalToPeopleAllowedToSeeIt()
    {
        var conversationId = Guid.CreateVersion7();

        await Factory.WithDatabaseAsync(async database =>
        {
            var conversation = Conversation.CreateGroup(
                conversationId,
                "Automated test: inconsistent legacy group",
                "target",
                AutomatedTestSeed.GoalWithoutTargetDateId,
                Q2ApiFactory.Now);
            conversation.AddParticipant(Guid.CreateVersion7(), AutomatedTestSeed.CurrentPersonId);
            conversation.AddParticipant(Guid.CreateVersion7(), AutomatedTestSeed.FriendPersonId);

            database.Conversations.Add(conversation);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        Assert.NotNull((await ThreadAsync(conversationId)).PinnedGoal);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var friendsThread = await (await friend.GetAsync(
            $"/api/chats/{conversationId}",
            TestContext.Current.CancellationToken)).ReadAsync<ThreadDocument>();

        Assert.Null(friendsThread.PinnedGoal);
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
