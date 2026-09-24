using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Photographs sent as messages: sending one, who may open it, and that it
/// goes when its conversation does.
/// </summary>
[Trait("Category", "Integration")]
public class ChatPhotoTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ImageDocument(Guid Id, ImagePurpose Purpose, int Width, int Height);

    private sealed record MessageDocument(Guid Id, string Text, ImageDocument? Image, bool IsMine);

    private sealed record ThreadDocument(Guid Id, IReadOnlyList<MessageDocument> Messages);

    private sealed record SummaryDocument(Guid Id, string? LastMessage, bool LastMessageHasPhoto, bool LastMessageIsMine);

    private static readonly Guid Direct = AutomatedTestSeed.DirectConversationId;

    [Fact]
    public async Task APhotographIsSentAsAMessageAndComesBackWithItsSize()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(1200, 900));

        var thread = await SendAsync(Client, Direct, new { imageId = photo.Id });

        var sent = thread.Messages[^1];
        Assert.True(sent.IsMine);
        Assert.Equal(string.Empty, sent.Text);
        Assert.NotNull(sent.Image);
        Assert.Equal(photo.Id, sent.Image.Id);

        // The thread reserves the frame at these before the bytes arrive.
        Assert.Equal(1200, sent.Image.Width);
        Assert.Equal(900, sent.Image.Height);
    }

    [Fact]
    public async Task APhotographCanCarryWordsAsWell()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));

        var thread = await SendAsync(Client, Direct, new { text = "  Schau mal  ", imageId = photo.Id });

        Assert.Equal("Schau mal", thread.Messages[^1].Text);
        Assert.Equal(photo.Id, thread.Messages[^1].Image?.Id);
    }

    [Fact]
    public async Task TheListSaysAPhotographArrivedRatherThanShowingNothing()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));
        await SendAsync(Client, Direct, new { imageId = photo.Id });

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var row = (await (await friend.GetAsync("/api/chats", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<SummaryDocument>>())
            .Single(chat => chat.Id == Direct);

        Assert.Null(row.LastMessage);
        Assert.True(row.LastMessageHasPhoto);
        Assert.False(row.LastMessageIsMine);
    }

    [Fact]
    public async Task TheOtherPersonInTheConversationCanOpenIt()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        // Uploaded but not sent: nobody's but the owner's yet.
        Assert.Equal(HttpStatusCode.NotFound, (await GetImageAsync(friend, photo.Id)).StatusCode);

        await SendAsync(Client, Direct, new { imageId = photo.Id });

        Assert.Equal(HttpStatusCode.OK, (await GetImageAsync(friend, photo.Id)).StatusCode);
    }

    [Fact]
    public async Task SomebodyOutsideTheConversationIsToldItDoesNotExist()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));
        await SendAsync(Client, Direct, new { imageId = photo.Id });

        var outsider = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        // Not 403: that would confirm the picture exists.
        Assert.Equal(HttpStatusCode.NotFound, (await GetImageAsync(outsider, photo.Id)).StatusCode);
    }

    [Fact]
    public async Task SomebodyElsesPhotographCannotBeSent()
    {
        // Otherwise sending an id would be a way to read a picture: the
        // message would make it visible to everybody in the thread.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await UploadAsync(friend, TestImages.Jpeg(400, 400));

        var response = await Client.PostJsonAsync($"/api/chats/{Direct}/messages", new { imageId = theirs.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APictureUploadedForSomethingElseCannotBeSent()
    {
        var avatar = await UploadAsync(Client, TestImages.Jpeg(400, 400), ImagePurpose.Avatar);

        var response = await Client.PostJsonAsync($"/api/chats/{Direct}/messages", new { imageId = avatar.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APhotographIsSentOnce()
    {
        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));
        await SendAsync(Client, Direct, new { imageId = photo.Id });

        var again = await Client.PostJsonAsync($"/api/chats/{Direct}/messages", new { imageId = photo.Id });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task AGroupsPhotographsGoWithTheLastPersonOut()
    {
        var group = await (await Client.PostJsonAsync("/api/chats/groups", new
        {
            title = "Automated test: photographs",
            memberIds = new[] { AutomatedTestSeed.FriendPersonId },
        })).ReadAsync<ThreadDocument>();

        var photo = await UploadAsync(Client, TestImages.Jpeg(400, 400));
        await SendAsync(Client, group.Id, new { imageId = photo.Id });

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await LeaveAsync(friend, group.Id);

        // Somebody is still in it, so the picture stays — for them only.
        Assert.Equal(HttpStatusCode.OK, (await GetImageAsync(Client, photo.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetImageAsync(friend, photo.Id)).StatusCode);

        await LeaveAsync(Client, group.Id);

        await AssertGoneAsync(photo.Id);
    }

    [Fact]
    public async Task DeletingAGoalTakesEverybodysPhotographsInItsConversation()
    {
        // The friend's picture, not the owner's: the owner's own would go with
        // their images anyway, and this is about what is not theirs.
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var photo = await UploadAsync(friend, TestImages.Jpeg(400, 400));
        await SendAsync(friend, AutomatedTestSeed.SharedGoalConversationId, new { imageId = photo.Id });

        (await Client.PostJsonAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/close", new { completed = true }))
            .EnsureSuccessStatusCode();
        (await Client.DeleteAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        await AssertGoneAsync(photo.Id);
    }

    [Fact]
    public async Task DeletingAnAccountTakesTheOtherSidesPhotographsInADirectThread()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var photo = await UploadAsync(friend, TestImages.Jpeg(400, 400));
        await SendAsync(friend, Direct, new { imageId = photo.Id });

        (await Client.DeleteJsonAsync("/api/auth/account", new { password = SeedAccounts.Password }))
            .EnsureSuccessStatusCode();

        await AssertGoneAsync(photo.Id);
    }

    private async Task AssertGoneAsync(Guid imageId)
    {
        await Factory.WithDatabaseAsync(async database =>
            Assert.False(await database.Images.AnyAsync(
                image => image.Id == imageId,
                TestContext.Current.CancellationToken)));

        Assert.DoesNotContain(
            Directory.Exists(Factory.ImageRootPath)
                ? Directory.EnumerateFiles(Factory.ImageRootPath, "*", SearchOption.AllDirectories)
                : [],
            path => Path.GetFileName(path).StartsWith(imageId.ToString("N"), StringComparison.Ordinal));
    }

    private static async Task<ImageDocument> UploadAsync(
        HttpClient client,
        byte[] bytes,
        ImagePurpose purpose = ImagePurpose.ChatPhoto)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(ImageFormatReader.Jpeg);

        var response = await client.PostAsync(
            $"/api/images?purpose={purpose}",
            content,
            TestContext.Current.CancellationToken);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<ImageDocument>();
    }

    private static async Task<ThreadDocument> SendAsync(HttpClient client, Guid conversationId, object body)
    {
        var response = await client.PostJsonAsync($"/api/chats/{conversationId}/messages", body);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<ThreadDocument>();
    }

    private static Task<HttpResponseMessage> GetImageAsync(HttpClient client, Guid id) =>
        client.GetAsync($"/api/images/{id}", TestContext.Current.CancellationToken);

    private static async Task LeaveAsync(HttpClient client, Guid conversationId) =>
        (await client.PostAsync($"/api/chats/{conversationId}/leave", content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
}
