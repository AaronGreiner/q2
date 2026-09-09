using System.Net;
using System.Net.Http.Headers;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Uploading a picture, reading it back, and — the part that matters — not
/// being able to read somebody else's.
/// </summary>
/// <remarks>
/// These go through the real pipeline and the real file system store, because
/// the interesting failures are exactly the ones a substituted store would hide:
/// a session that is not checked, a cache header that is not set, bytes that
/// stay on disk after the row is gone.
/// </remarks>
[Trait("Category", "Integration")]
public class ImagesEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ImageDocument(
        Guid Id,
        ImagePurpose Purpose,
        int Width,
        int Height,
        int ByteSize,
        DateTimeOffset CreatedAt);

    private sealed record QuotaDocument(long BytesUsed, long ByteLimit, int ImageCount, int ImageLimit);

    private sealed record PersonDocument(Guid Id, string DisplayName, string Initials, Guid? AvatarImageId);

    private sealed record ProfileDocument(PersonDocument Person);

    [Fact]
    public async Task StoresAnUploadAndReadsBackWhatTheBytesSaid()
    {
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(800, 600));

        Assert.Equal(ImagePurpose.Avatar, uploaded.Purpose);
        Assert.Equal(800, uploaded.Width);
        Assert.Equal(600, uploaded.Height);
        Assert.Equal(Q2ApiFactory.Now, uploaded.CreatedAt);
    }

    [Fact]
    public async Task ServesTheSameBytesItWasGiven()
    {
        var bytes = TestImages.Png(400, 400);
        var uploaded = await UploadAsync(Client, bytes, ImageFormatReader.Png);

        var response = await Client.GetAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal(ImageFormatReader.Png, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForbidsEveryCacheFromKeepingACopy()
    {
        // Every picture in q2 belongs to somebody, and a phone is shared. This
        // header is the difference between "signed out" and "signed out, but
        // the last photograph is still in the browser's cache".
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));

        var response = await Client.GetAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task RefusesAnUploadWithoutASession()
    {
        var response = await PostAsync(AnonymousClient, TestImages.Jpeg(200, 200), ImageFormatReader.Jpeg);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefusesToServeAnImageWithoutASession()
    {
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));

        var response = await AnonymousClient.GetAsync(
            $"/api/images/{uploaded.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LetsAnotherSignedInPersonSeeAnAvatar()
    {
        // An avatar is as public as the initials it replaces: it appears in
        // search results, where the viewer is by definition not a friend yet.
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));
        var somebodyElse = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await somebodyElse.GetAsync(
            $"/api/images/{uploaded.Id}",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HidesSomebodyElsesProofBehindANotFound()
    {
        // Not 403: "you may not see this" would confirm the picture exists.
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200), purpose: ImagePurpose.Proof);
        var somebodyElse = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await somebodyElse.GetAsync(
            $"/api/images/{uploaded.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectsSomethingThatIsNotAnImageEvenWhenItSaysItIs()
    {
        var response = await PostAsync(
            Client,
            System.Text.Encoding.ASCII.GetBytes("MZ this is an executable"),
            ImageFormatReader.Jpeg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAFormPostRatherThanTreatingItAsAnUpload()
    {
        // The shape of the request is the CSRF defence: a cross-site HTML form
        // can send multipart, and can never send image/jpeg. If this ever starts
        // succeeding, the endpoint needs an antiforgery token instead.
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(TestImages.Jpeg(200, 200)), "file", "photo.jpg" },
        };

        var response = await Client.PostAsync("/api/images", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task RefusesAnUploadLargerThanTheLimit()
    {
        var oversized = new byte[StoredImage.MaxBytes + 1];
        TestImages.Jpeg(200, 200).CopyTo(oversized, 0);

        var response = await PostAsync(Client, oversized, ImageFormatReader.Jpeg);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task CountsWhatSomebodyHasUsed()
    {
        var before = await QuotaAsync();
        Assert.Equal(0, before.ImageCount);
        Assert.Equal(StoredImage.MaxBytesPerPerson, before.ByteLimit);

        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));

        var after = await QuotaAsync();
        Assert.Equal(1, after.ImageCount);
        Assert.Equal(uploaded.ByteSize, after.BytesUsed);
    }

    [Fact]
    public async Task DeletesTheRowAndTheBytesTogether()
    {
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));

        var deleted = await Client.DeleteAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterwards = await Client.GetAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        // Nothing left on disk either: a deleted photograph that is still a file
        // is a deleted photograph in name only.
        Assert.Empty(Directory.EnumerateFiles(Factory.ImageRootPath, "*", SearchOption.AllDirectories));

        var quota = await QuotaAsync();
        Assert.Equal(0, quota.ImageCount);
        Assert.Equal(0, quota.BytesUsed);
    }

    [Fact]
    public async Task WillNotLetSomebodyDeleteAnImageThatIsNotTheirs()
    {
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(200, 200));
        var somebodyElse = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await somebodyElse.DeleteAsync(
            $"/api/images/{uploaded.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And it is still there for its owner.
        var stillThere = await Client.GetAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);
        stillThere.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SetsAndThenReleasesAnAvatar()
    {
        var uploaded = await UploadAsync(Client, TestImages.Jpeg(512, 512));

        var updated = await (await Client.PutJsonAsync(
            "/api/profile",
            new { avatarImageId = uploaded.Id })).ReadAsync<ProfileDocument>();

        Assert.Equal(uploaded.Id, updated.Person.AvatarImageId);

        // Deleting the picture is also how somebody goes back to their initials
        // — there is no second way to say "remove it", on purpose.
        await Client.DeleteAsync($"/api/images/{uploaded.Id}", TestContext.Current.CancellationToken);

        var afterwards = await (await Client.GetAsync("/api/profile", TestContext.Current.CancellationToken))
            .ReadAsync<ProfileDocument>();

        Assert.Null(afterwards.Person.AvatarImageId);
        Assert.False(string.IsNullOrWhiteSpace(afterwards.Person.Initials));
    }

    [Fact]
    public async Task ReplacingTheAvatarGivesTheOldPictureBack()
    {
        // Changing your picture is the ordinary case, not an edge one. Nothing
        // else ever points at an avatar and no screen lists them, so a replaced
        // one that stayed would occupy this person's allowance for good with no
        // way for them to reach it.
        var first = await UploadAsync(Client, TestImages.Jpeg(256, 256, padding: 64));
        await Client.PutJsonAsync("/api/profile", new { avatarImageId = first.Id });

        var second = await UploadAsync(Client, TestImages.Jpeg(512, 512));
        var updated = await (await Client.PutJsonAsync(
            "/api/profile",
            new { avatarImageId = second.Id })).ReadAsync<ProfileDocument>();

        Assert.Equal(second.Id, updated.Person.AvatarImageId);

        var gone = await Client.GetAsync($"/api/images/{first.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var quota = await QuotaAsync();
        Assert.Equal(1, quota.ImageCount);
        Assert.Equal(second.ByteSize, quota.BytesUsed);
    }

    [Fact]
    public async Task SettingTheSameAvatarTwiceKeepsIt()
    {
        // The "replaced" picture and the new one being the same is a no-op, not
        // an instruction to delete what was just chosen.
        var image = await UploadAsync(Client, TestImages.Jpeg(256, 256));

        await Client.PutJsonAsync("/api/profile", new { avatarImageId = image.Id });
        var updated = await (await Client.PutJsonAsync(
            "/api/profile",
            new { avatarImageId = image.Id })).ReadAsync<ProfileDocument>();

        Assert.Equal(image.Id, updated.Person.AvatarImageId);

        var still = await Client.GetAsync($"/api/images/{image.Id}", TestContext.Current.CancellationToken);
        still.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RenamingLeavesThePictureAlone()
    {
        // The guard is "did the avatar change", not "was an avatar sent".
        var image = await UploadAsync(Client, TestImages.Jpeg(256, 256));
        await Client.PutJsonAsync("/api/profile", new { avatarImageId = image.Id });

        var updated = await (await Client.PutJsonAsync(
            "/api/profile",
            new { displayName = "Mara Sommer" })).ReadAsync<ProfileDocument>();

        Assert.Equal(image.Id, updated.Person.AvatarImageId);

        var still = await Client.GetAsync($"/api/images/{image.Id}", TestContext.Current.CancellationToken);
        still.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WillNotPointAnAvatarAtSomebodyElsesPicture()
    {
        // Without this check, "set my avatar to this id" would be a way to read
        // any image in the database through one's own profile.
        var somebodyElse = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await UploadAsync(somebodyElse, TestImages.Jpeg(200, 200));

        var response = await Client.PutJsonAsync("/api/profile", new { avatarImageId = theirs.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WillNotPointAnAvatarAtAProofPhotograph()
    {
        var proof = await UploadAsync(Client, TestImages.Jpeg(200, 200), purpose: ImagePurpose.Proof);

        var response = await Client.PutJsonAsync("/api/profile", new { avatarImageId = proof.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenamingAlsoMovesTheInitials()
    {
        var updated = await (await Client.PutJsonAsync(
            "/api/profile",
            new { displayName = "Mara Sommer" })).ReadAsync<ProfileDocument>();

        Assert.Equal("Mara Sommer", updated.Person.DisplayName);
        Assert.Equal("MS", updated.Person.Initials);
    }

    [Fact]
    public async Task RefusesAnEmptyName()
    {
        var response = await Client.PutJsonAsync("/api/profile", new { displayName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<QuotaDocument> QuotaAsync() =>
        await (await Client.GetAsync("/api/images/quota", TestContext.Current.CancellationToken))
            .ReadAsync<QuotaDocument>();

    private static async Task<ImageDocument> UploadAsync(
        HttpClient client,
        byte[] bytes,
        string contentType = ImageFormatReader.Jpeg,
        ImagePurpose purpose = ImagePurpose.Avatar)
    {
        var response = await PostAsync(client, bytes, contentType, purpose);

        // The body rather than the bare status: an upload that fails does so
        // for a reason the response already names, and a test that hides it
        // costs a debugging session.
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<ImageDocument>();
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        byte[] bytes,
        string contentType,
        ImagePurpose purpose = ImagePurpose.Avatar)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return client.PostAsync(
            $"/api/images?purpose={purpose}",
            content,
            TestContext.Current.CancellationToken);
    }
}
