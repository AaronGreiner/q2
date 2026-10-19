using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Looking after the messages of a conversation: correcting and taking back
/// your own (#25), its photographs in one grid, and pinning it (#24).
/// </summary>
[Trait("Category", "Integration")]
public class ChatMessageManagementTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ImageDocument(Guid Id);

    private sealed record ReactionDocument(string Kind, int Count, bool IsMine);

    private sealed record MessageDocument(
        Guid Id,
        string Text,
        ImageDocument? Image,
        bool IsMine,
        IReadOnlyList<ReactionDocument> Reactions,
        DateTimeOffset? EditedAt,
        bool IsDeleted,
        bool CanEdit);

    private sealed record ThreadDocument(Guid Id, IReadOnlyList<MessageDocument> Messages, bool IsPinned);

    private sealed record SummaryDocument(Guid Id, string Name, string? LastMessage, bool LastMessageIsDeleted, bool IsPinned);

    private sealed record PhotoDocument(Guid Id, Guid ImageId, bool IsProof, string AuthorName, DateTimeOffset At, string? Source);

    private sealed record GoalDocument(Guid Id, Guid? ConversationId);

    private static readonly Guid Direct = AutomatedTestSeed.DirectConversationId;

    private FixedTimeProvider Clock => (FixedTimeProvider)Factory.Services.GetRequiredService<TimeProvider>();

    // -- editing ------------------------------------------------------------

    [Fact]
    public async Task TheSenderCorrectsTheWordsAndTheOthersSeeThatTheyChanged()
    {
        var sent = await SendAsync(Client, Direct, new { text = "Bis morgn" });
        Assert.True(sent.CanEdit);

        var edited = await (await EditAsync(Client, Direct, sent.Id, "Bis morgen")).ReadAsync<MessageDocument>();

        Assert.Equal("Bis morgen", edited.Text);
        Assert.NotNull(edited.EditedAt);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var seen = Assert.Single((await ThreadAsync(friend, Direct)).Messages, message => message.Id == sent.Id);

        Assert.Equal("Bis morgen", seen.Text);
        Assert.NotNull(seen.EditedAt);
        Assert.False(seen.CanEdit);
    }

    [Fact]
    public async Task TheWordsCanOnlyBeCorrectedWithinFifteenMinutes()
    {
        var sent = await SendAsync(Client, Direct, new { text = "Zu spät bemerkt" });

        Clock.Set(Q2ApiFactory.Now + ChatMessage.EditWindow + TimeSpan.FromMinutes(1));

        var refused = await EditAsync(Client, Direct, sent.Id, "Korrigiert");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(FieldErrorCodes.TooLateToEdit, (await refused.ReadAsync<CodedProblemDocument>()).CodeOf("Text").Code);

        Assert.False(Assert.Single((await ThreadAsync(Client, Direct)).Messages, message => message.Id == sent.Id).CanEdit);
    }

    [Fact]
    public async Task NobodyCanCorrectOrTakeBackSomebodyElsesMessage()
    {
        var sent = await SendAsync(Client, Direct, new { text = "Meins" });
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await EditAsync(friend, Direct, sent.Id, "Ihres")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(friend, Direct, sent.Id)).StatusCode);

        // And somebody outside the conversation does not learn that it exists.
        var outsider = await ClientForAsync(AutomatedTestSeed.RequesterEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await EditAsync(outsider, Direct, sent.Id, "Fremd")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(outsider, Direct, sent.Id)).StatusCode);
    }

    // -- taking back --------------------------------------------------------

    /// <summary>
    /// The place stays and says so; the words, the photograph and the
    /// reactions go for everybody, and the photograph leaves the disk.
    /// </summary>
    [Fact]
    public async Task ADeletedMessageKeepsItsPlaceAndLosesEverythingElse()
    {
        var photo = await UploadAsync(Client);
        var sent = await SendAsync(Client, Direct, new { text = "Schau mal", imageId = photo });
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        (await friend.PostJsonAsync($"/api/chats/{Direct}/messages/{sent.Id}/reactions", new { kind = "Fire" })).EnsureSuccessStatusCode();

        var deleted = await (await DeleteAsync(Client, Direct, sent.Id)).ReadAsync<MessageDocument>();

        Assert.True(deleted.IsDeleted);
        Assert.Equal(string.Empty, deleted.Text);
        Assert.Null(deleted.Image);
        Assert.Empty(deleted.Reactions);
        Assert.False(deleted.CanEdit);

        var seen = Assert.Single((await ThreadAsync(friend, Direct)).Messages, message => message.Id == sent.Id);
        Assert.True(seen.IsDeleted);
        Assert.Equal(string.Empty, seen.Text);

        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync($"/api/images/{photo}", TestContext.Current.CancellationToken)).StatusCode);
        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Images.AnyAsync(image => image.Id == photo, TestContext.Current.CancellationToken)));

        // The list says what happened rather than showing an empty preview.
        var row = Assert.Single(await ListAsync(friend), summary => summary.Id == Direct);
        Assert.True(row.LastMessageIsDeleted);
        Assert.Null(row.LastMessage);
    }

    [Fact]
    public async Task OneMessageCanBeReadAgainByItsId()
    {
        var sent = await SendAsync(Client, Direct, new { text = "Einmal" });
        (await EditAsync(Client, Direct, sent.Id, "Noch einmal")).EnsureSuccessStatusCode();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var one = await (await friend.GetAsync($"/api/chats/{Direct}/messages/{sent.Id}", TestContext.Current.CancellationToken))
            .ReadAsync<MessageDocument>();

        Assert.Equal("Noch einmal", one.Text);
        Assert.NotNull(one.EditedAt);
    }

    // -- photographs --------------------------------------------------------

    [Fact]
    public async Task TheGridHoldsTheSentPhotographsNewestFirstAndNotTheTakenBackOnes()
    {
        var first = await UploadAsync(Client);
        await SendAsync(Client, Direct, new { imageId = first });
        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(1));
        var second = await UploadAsync(Client);
        var withdrawn = await SendAsync(Client, Direct, new { imageId = second });
        Clock.Set(Q2ApiFactory.Now + TimeSpan.FromMinutes(2));
        var third = await UploadAsync(Client);
        await SendAsync(Client, Direct, new { imageId = third });

        (await DeleteAsync(Client, Direct, withdrawn.Id)).EnsureSuccessStatusCode();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var photos = await PhotosAsync(friend, Direct);

        Assert.Equal([third, first], photos.Select(photo => photo.ImageId));
        Assert.All(photos, photo => Assert.False(photo.IsProof));
    }

    [Fact]
    public async Task AGoalsConversationShowsItsProofsInTheGridWithWhereTheyCameFrom()
    {
        var goal = await (await Client.GetAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDocument>();
        var conversation = goal.ConversationId ?? throw new InvalidOperationException("The seeded goal has no conversation.");

        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var photo = Assert.Single(await PhotosAsync(Client, conversation));
        Assert.True(photo.IsProof);
        Assert.Equal(proof.Id, photo.Id);
        Assert.NotNull(photo.Source);
    }

    [Fact]
    public async Task TheGridOfSomebodyElsesConversationIsNotFound()
    {
        var outsider = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await outsider.GetAsync($"/api/chats/{Direct}/photos", TestContext.Current.CancellationToken)).StatusCode);
    }

    // -- pinning ------------------------------------------------------------

    /// <summary>A pin is the reader's own: the other end's list does not move.</summary>
    [Fact]
    public async Task APinnedConversationComesFirstForTheOneWhoPinnedItOnly()
    {
        var pinned = await (await PinAsync(Client, Direct, pinned: true)).ReadAsync<ThreadDocument>();
        Assert.True(pinned.IsPinned);

        Assert.True(Assert.Single(await ListAsync(Client), row => row.Id == Direct).IsPinned);
        Assert.Equal(Direct, (await ListAsync(Client))[0].Id);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        Assert.False(Assert.Single(await ListAsync(friend), row => row.Id == Direct).IsPinned);

        var unpinned = await (await PinAsync(Client, Direct, pinned: false)).ReadAsync<ThreadDocument>();
        Assert.False(unpinned.IsPinned);
    }

    [Fact]
    public async Task AtMostThreeConversationsArePinned()
    {
        // As many groups as it takes for one more conversation than may be pinned.
        while ((await ListAsync(Client)).Count <= ChatService.MaxPinned)
        {
            (await Client.PostJsonAsync("/api/chats/groups", new
            {
                title = "Automated test: pinning",
                memberIds = new[] { AutomatedTestSeed.FriendPersonId },
            })).EnsureSuccessStatusCode();
        }

        var conversations = (await ListAsync(Client)).Select(row => row.Id).ToList();

        foreach (var id in conversations.Take(ChatService.MaxPinned))
        {
            (await PinAsync(Client, id, pinned: true)).EnsureSuccessStatusCode();
        }

        var refused = await PinAsync(Client, conversations[ChatService.MaxPinned], pinned: true);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var code = (await refused.ReadAsync<CodedProblemDocument>()).CodeOf("Pinned");
        Assert.Equal(FieldErrorCodes.TooMany, code.Code);
        Assert.Equal(ChatService.MaxPinned, code.Max);

        // Pinning one that already is pinned is not a fourth.
        (await PinAsync(Client, conversations[0], pinned: true)).EnsureSuccessStatusCode();
    }

    private static async Task<MessageDocument> SendAsync(HttpClient client, Guid conversationId, object body)
    {
        var response = await client.PostJsonAsync($"/api/chats/{conversationId}/messages", body);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<ThreadDocument>()).Messages[^1];
    }

    private static Task<HttpResponseMessage> EditAsync(HttpClient client, Guid conversationId, Guid messageId, string text) =>
        client.PutJsonAsync($"/api/chats/{conversationId}/messages/{messageId}", new { text });

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid conversationId, Guid messageId) =>
        client.DeleteAsync($"/api/chats/{conversationId}/messages/{messageId}", TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> PinAsync(HttpClient client, Guid conversationId, bool pinned) =>
        client.PutJsonAsync($"/api/chats/{conversationId}/pin", new { pinned });

    private static async Task<ThreadDocument> ThreadAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/chats/{id}", TestContext.Current.CancellationToken)).ReadAsync<ThreadDocument>();

    private static async Task<IReadOnlyList<SummaryDocument>> ListAsync(HttpClient client) =>
        await (await client.GetAsync("/api/chats", TestContext.Current.CancellationToken)).ReadAsync<List<SummaryDocument>>();

    private static async Task<IReadOnlyList<PhotoDocument>> PhotosAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/chats/{id}/photos", TestContext.Current.CancellationToken)).ReadAsync<List<PhotoDocument>>();

    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        var content = new ByteArrayContent(TestImages.Jpeg(400, 400));
        content.Headers.ContentType = new MediaTypeHeaderValue(ImageFormatReader.Jpeg);

        var response = await client.PostAsync($"/api/images?purpose={ImagePurpose.ChatPhoto}", content, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.ReadAsync<ImageDocument>()).Id;
    }
}
