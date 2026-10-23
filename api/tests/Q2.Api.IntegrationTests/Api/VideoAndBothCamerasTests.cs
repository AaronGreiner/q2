using System.Net;
using System.Net.Http.Headers;
using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A proof that is a short clip (#124), and one taken with both cameras at
/// once (#125), through the real pipeline and the real file store.
/// </summary>
/// <remarks>
/// What is worth the pipeline here is what a unit test cannot say: that a clip
/// is refused before it is stored, that it can be played from the middle, and
/// that the front camera's picture and a clip's still have exactly the
/// audience of the proof — no wider, and gone with it.
/// </remarks>
[Trait("Category", "Integration")]
public class VideoAndBothCamerasTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record UploadDocument(Guid Id, ImagePurpose Purpose, int Width, int Height, MediaKind Kind, int? DurationMilliseconds);

    private sealed record QuotaDocument(long BytesUsed, long ByteLimit, long VideoBytesUsed, long VideoByteLimit, int VideoMaxBytes, int VideoMaxSeconds);

    private sealed record OwnProofDocument(Guid Id, Guid ImageId, MediaKind Media, Guid? PosterImageId, Guid? InsetImageId, ProofSource Source);

    private sealed record ChatPhotoDocument(Guid Id, Guid ImageId, bool IsProof, MediaKind Media, Guid? PosterImageId, Guid? InsetImageId);

    [Fact]
    public async Task AClipIsStoredAsAProofWithTheLengthItsHeadersGive()
    {
        var response = await UploadAsync(TestVideos.Mp4(durationMilliseconds: 7_250), ImagePurpose.Proof);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var uploaded = await response.ReadAsync<UploadDocument>();
        Assert.Equal(MediaKind.Video, uploaded.Kind);
        Assert.Equal(7_250, uploaded.DurationMilliseconds);
        Assert.Equal(1280, uploaded.Width);
        Assert.Equal(720, uploaded.Height);
    }

    [Fact]
    public async Task APictureIsStillAPhotographWithNoLength()
    {
        var response = await UploadAsync(TestImages.Jpeg(640, 480), ImagePurpose.Proof, ImageFormatReader.Jpeg);

        var uploaded = await response.ReadAsync<UploadDocument>();
        Assert.Equal(MediaKind.Photo, uploaded.Kind);
        Assert.Null(uploaded.DurationMilliseconds);
    }

    [Theory]
    [InlineData(ImagePurpose.Avatar)]
    [InlineData(ImagePurpose.ChatPhoto)]
    [InlineData(ImagePurpose.ChallengeEntry)]
    public async Task OnlyAProofMayBeAClip(ImagePurpose purpose)
    {
        var response = await UploadAsync(TestVideos.Mp4(), purpose);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AClipLongerThanTheLimitIsRefused()
    {
        var response = await UploadAsync(TestVideos.Mp4(durationMilliseconds: 30_000), ImagePurpose.Proof);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AClipLargerThanItsByteLimitIsRefusedBeforeItIsRead()
    {
        var response = await UploadAsync(TestVideos.Mp4(padding: StoredImage.MaxVideoBytes), ImagePurpose.Proof);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    /// <summary>
    /// A picture is still held to a picture's limit when it arrives labelled
    /// as a clip: the header sized the read, the bytes decided the rest.
    /// </summary>
    [Fact]
    public async Task APictureSentAsAClipIsHeldToAPicturesLimit()
    {
        var response = await UploadAsync(
            TestImages.Jpeg(640, 480, padding: StoredImage.MaxBytes),
            ImagePurpose.Proof,
            ImageFormatReader.Mp4);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AClipInACodecNotEveryPhoneCanPlayIsRefused()
    {
        var response = await UploadAsync(TestVideos.Mp4(videoCodec: "vp09"), ImagePurpose.Proof);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Safari plays a clip only from a server that answers a byte range, and
    /// seeking needs one everywhere. The headers that keep a copy out of every
    /// cache still apply.
    /// </summary>
    [Fact]
    public async Task AClipIsServedARangeAtATime()
    {
        var bytes = TestVideos.Mp4();
        var uploaded = await (await UploadAsync(bytes, ImagePurpose.Proof)).ReadAsync<UploadDocument>();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{uploaded.Id}");
        request.Headers.Range = new RangeHeaderValue(0, 99);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(ImageFormatReader.Mp4, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes.Length, response.Content.Headers.ContentRange?.Length);
        Assert.Equal(bytes[..100], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task APhotographIsStillAlwaysFetchedWhole()
    {
        var uploaded = await (await UploadAsync(TestImages.Jpeg(640, 480), ImagePurpose.Proof, ImageFormatReader.Jpeg))
            .ReadAsync<UploadDocument>();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/images/{uploaded.Id}");
        request.Headers.Range = new RangeHeaderValue(0, 9);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheQuotaSaysWhatClipsTakeAndWhatTheyMay()
    {
        var bytes = TestVideos.Mp4();
        await UploadAsync(bytes, ImagePurpose.Proof);
        await UploadAsync(TestImages.Jpeg(640, 480), ImagePurpose.Proof, ImageFormatReader.Jpeg);

        var quota = await (await Client.GetAsync("/api/images/quota", TestContext.Current.CancellationToken))
            .ReadAsync<QuotaDocument>();

        Assert.Equal(bytes.Length, quota.VideoBytesUsed);
        Assert.True(quota.BytesUsed > quota.VideoBytesUsed);
        Assert.Equal(StoredImage.MaxVideoBytesPerPerson, quota.VideoByteLimit);
        Assert.Equal(StoredImage.MaxVideoBytes, quota.VideoMaxBytes);
        Assert.Equal(StoredImage.MaxVideoSeconds, quota.VideoMaxSeconds);
    }

    [Fact]
    public async Task AClipIsDeliveredWithItsStillAndFriendsMaySeeBoth()
    {
        var clip = await Client.UploadProofVideoAsync();
        var still = await Client.UploadProofImageAsync();

        var proof = await DeliverAsync(new { imageId = clip, posterImageId = still, source = ProofSource.Live });

        Assert.Equal(MediaKind.Video, proof.Media);
        Assert.Equal(still, proof.PosterImageId);
        Assert.Null(proof.InsetImageId);

        await AssertOnlyTheGoalsPeopleSeeAsync(clip, still);
    }

    /// <summary>The still is what every grid shows; a clip without one would be a blank tile.</summary>
    [Fact]
    public async Task AClipWithoutAStillIsRefused()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof",
            new { imageId = await Client.UploadProofVideoAsync(), source = ProofSource.Live });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AClipIsOnlyEverLive()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof",
            new
            {
                imageId = await Client.UploadProofVideoAsync(),
                posterImageId = await Client.UploadProofImageAsync(),
                source = ProofSource.Screenshot,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AStillHasToBeAPhotograph()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof",
            new
            {
                imageId = await Client.UploadProofVideoAsync(),
                posterImageId = await Client.UploadProofVideoAsync(),
                source = ProofSource.Live,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BothCamerasAreDeliveredTogetherAndFriendsMaySeeBoth()
    {
        var rear = await Client.UploadProofImageAsync();
        var front = await Client.UploadProofImageAsync();

        var proof = await DeliverAsync(new { imageId = rear, insetImageId = front, source = ProofSource.Live });

        Assert.Equal(MediaKind.Photo, proof.Media);
        Assert.Equal(front, proof.InsetImageId);
        Assert.Equal(ProofSource.Live, proof.Source);

        await AssertOnlyTheGoalsPeopleSeeAsync(rear, front);
    }

    [Fact]
    public async Task AClipFromBothCamerasCarriesAllThree()
    {
        var clip = await Client.UploadProofVideoAsync();
        var still = await Client.UploadProofImageAsync();
        var front = await Client.UploadProofImageAsync();

        var proof = await DeliverAsync(new { imageId = clip, posterImageId = still, insetImageId = front, source = ProofSource.Live });

        Assert.Equal(MediaKind.Video, proof.Media);
        Assert.Equal(still, proof.PosterImageId);
        Assert.Equal(front, proof.InsetImageId);

        await AssertOnlyTheGoalsPeopleSeeAsync(clip, still, front);
    }

    [Fact]
    public async Task APickedFileCannotClaimASecondCamera()
    {
        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof",
            new
            {
                imageId = await Client.UploadProofImageAsync(),
                insetImageId = await Client.UploadProofImageAsync(),
                source = ProofSource.Screenshot,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Naming somebody else's picture as your front camera would otherwise be
    /// a way to show it to your own friends.
    /// </summary>
    [Fact]
    public async Task SomebodyElsesPictureCannotBeTheFrontCamera()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await friend.UploadProofImageAsync();

        var response = await Client.PostJsonAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof",
            new { imageId = await Client.UploadProofImageAsync(), insetImageId = theirs, source = ProofSource.Live });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheGalleryAndTheConversationsGridSayWhatEachProofIs()
    {
        var clip = await Client.UploadProofVideoAsync();
        var still = await Client.UploadProofImageAsync();
        var front = await Client.UploadProofImageAsync();
        var proof = await DeliverAsync(new { imageId = clip, posterImageId = still, insetImageId = front, source = ProofSource.Live });

        var mine = await (await Client.GetAsync("/api/proofs/mine", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<OwnProofDocument>>();
        var tile = Assert.Single(mine, entry => entry.Id == proof.Id);
        Assert.Equal(MediaKind.Video, tile.Media);
        Assert.Equal(still, tile.PosterImageId);
        Assert.Equal(front, tile.InsetImageId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var grid = await (await friend.GetAsync(
                $"/api/chats/{AutomatedTestSeed.SharedGoalConversationId}/photos",
                TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ChatPhotoDocument>>();
        var cell = Assert.Single(grid, entry => entry.Id == proof.Id);
        Assert.True(cell.IsProof);
        Assert.Equal(MediaKind.Video, cell.Media);
        Assert.Equal(still, cell.PosterImageId);
        Assert.Equal(front, cell.InsetImageId);
    }

    /// <summary>
    /// A deleted goal takes every picture of its proofs with it — the front
    /// camera's and a clip's still are not left behind on disk.
    /// </summary>
    [Fact]
    public async Task DeletingTheGoalTakesEveryPartOfItsProofsWithIt()
    {
        var clip = await Client.UploadProofVideoAsync();
        var still = await Client.UploadProofImageAsync();
        var front = await Client.UploadProofImageAsync();
        await DeliverAsync(new { imageId = clip, posterImageId = still, insetImageId = front, source = ProofSource.Live });

        (await Client.PostJsonAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/close", new { completed = true }))
            .EnsureSuccessStatusCode();
        (await Client.DeleteAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        foreach (var id in new[] { clip, still, front })
        {
            var response = await Client.GetAsync($"/api/images/{id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task AUseIsLoggedWithAPhotographNeverAClip()
    {
        var created = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Keine Süßigkeiten",
            kind = "Dont",
            limit = 2,
            proofMode = "HonestOnly",
            schedule = new { kind = "Limit", period = "Week" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var goal = await created.ReadAsync<CreatedGoalDocument>();

        var response = await Client.PostJsonAsync(
            $"/api/goals/{goal.Id}/uses",
            new { imageId = await Client.UploadProofVideoAsync() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CreatedGoalDocument(Guid Id);

    private async Task<ProofDocument> DeliverAsync(object body)
    {
        var response = await Client.PostJsonAsync($"/api/goals/{AutomatedTestSeed.ActiveGoalId}/proof", body);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return (await response.ReadAsync<DeliveredProofDocument>()).Proof;
    }

    /// <summary>The goal's friend may open every part; somebody not on the goal gets 404 for each.</summary>
    private async Task AssertOnlyTheGoalsPeopleSeeAsync(params Guid[] ids)
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        foreach (var id in ids)
        {
            (await friend.GetAsync($"/api/images/{id}", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

            var refused = await stranger.GetAsync($"/api/images/{id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> UploadAsync(byte[] bytes, ImagePurpose purpose, string contentType = ImageFormatReader.Mp4)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return await Client.PostAsync($"/api/images?purpose={purpose}", content, TestContext.Current.CancellationToken);
    }
}
