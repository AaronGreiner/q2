using System.Net;
using System.Net.Http.Headers;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The daily challenge: one prompt for everybody, a room made of your own
/// friends, and nothing to win.
/// </summary>
/// <remarks>
/// The arithmetic of the queue is covered by <c>ChallengeQueueTests</c>, which
/// can state a week in one line. What is worth a pipeline is everything the
/// arithmetic cannot say: who ends up in whose room, what a picture is worth
/// before you have contributed one yourself, and whether the archive can be
/// talked into holding somebody else's photograph.
/// </remarks>
[Trait("Category", "Integration")]
public class ChallengeEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ChallengeDocument(
        Guid Id,
        string Prompt,
        DateTimeOffset PublishedAt,
        DateTimeOffset ExpiresAt);

    private sealed record EntryDocument(
        Guid Id,
        ChallengePersonDocument Author,
        Guid? ImageId,
        bool CapturedInApp,
        DateTimeOffset CreatedAt,
        bool IsMine,
        IReadOnlyList<ReactionSummaryDocument> Reactions);

    private sealed record ChallengePersonDocument(Guid Id, string DisplayName, string Handle);

    private sealed record RoomDocument(
        ChallengeDocument Challenge,
        EntryDocument? OwnEntry,
        IReadOnlyList<EntryDocument> Entries,
        int FriendCount,
        bool IsRevealed);

    private sealed record TodayDocument(RoomDocument? Room);

    private sealed record ArchiveDocument(ChallengeDocument Challenge, EntryDocument Entry);

    private sealed record UploadedImage(Guid Id);

    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        var content = new ByteArrayContent(TestImages.Jpeg(640, 640));
        content.Headers.ContentType = new MediaTypeHeaderValue(ImageFormatReader.Jpeg);

        var response = await client.PostAsync(
            $"/api/images?purpose={ImagePurpose.ChallengeEntry}",
            content,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<UploadedImage>()).Id;
    }

    private static async Task<HttpResponseMessage> ContributeAsync(HttpClient client, bool capturedInApp = true) =>
        await client.PostJsonAsync(
            "/api/challenges/today/entry",
            new { imageId = await UploadAsync(client), capturedInApp });

    private static async Task<RoomDocument> ContributedRoomAsync(HttpClient client)
    {
        var response = await ContributeAsync(client);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<RoomDocument>();
    }

    private static async Task<TodayDocument> TodayAsync(HttpClient client) =>
        await (await client.GetAsync("/api/challenges/today", TestContext.Current.CancellationToken))
            .ReadAsync<TodayDocument>();

    private static async Task<IReadOnlyList<ArchiveDocument>> ArchiveAsync(HttpClient client) =>
        await (await client.GetAsync("/api/challenges/archive", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<ArchiveDocument>>();

    [Fact]
    public async Task TheDayHasOnePromptAndItStartsWithAnEmptyRoom()
    {
        var today = await TodayAsync(Client);

        Assert.NotNull(today.Room);
        Assert.Equal(AutomatedTestSeed.ChallengePrompt, today.Room.Challenge.Prompt);
        Assert.Null(today.Room.OwnEntry);
        Assert.Empty(today.Room.Entries);
        Assert.False(today.Room.IsRevealed);

        // The denominator of "3 von 12 dabei": one accepted friendship in this
        // world, and a pending request is not one.
        Assert.Equal(1, today.Room.FriendCount);
    }

    [Fact]
    public async Task ContributingOpensTheRoom()
    {
        var room = await ContributedRoomAsync(Client);

        Assert.NotNull(room.OwnEntry);
        Assert.True(room.OwnEntry.IsMine);
        Assert.NotNull(room.OwnEntry.ImageId);
        Assert.True(room.IsRevealed);
    }

    /// <summary>
    /// The reciprocity rule, through the pipeline. Before contributing you are
    /// told who is in and nothing else; the picture is not sent for a browser
    /// to hide.
    /// </summary>
    [Fact]
    public async Task AFriendsPictureArrivesOnlyAfterYouHaveContributedYourOwn()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await ContributedRoomAsync(friend);

        var covered = await TodayAsync(Client);
        var waiting = Assert.Single(covered.Room!.Entries);

        Assert.False(covered.Room.IsRevealed);
        Assert.Null(waiting.ImageId);
        Assert.Empty(waiting.Reactions);

        // Who is already in is the reason to join, and costs nobody anything.
        Assert.Equal(AutomatedTestSeed.FriendPersonId, waiting.Author.Id);

        var opened = await ContributedRoomAsync(Client);
        var revealed = Assert.Single(opened.Entries);

        Assert.True(opened.IsRevealed);
        Assert.NotNull(revealed.ImageId);
    }

    /// <summary>
    /// And the same rule over the bytes. An id kept from an earlier evening,
    /// or read off somebody's screen, still does not open the picture.
    /// </summary>
    [Fact]
    public async Task ThePictureItselfIsRefusedUntilYouHaveContributed()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await ContributedRoomAsync(friend);
        var imageId = theirs.OwnEntry!.ImageId!.Value;

        var before = await Client.GetAsync($"/api/images/{imageId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);

        await ContributedRoomAsync(Client);

        (await Client.GetAsync($"/api/images/{imageId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Everybody sees a different room. That is what spares this product a
    /// public surface and the moderation duty that comes with one.
    /// </summary>
    [Fact]
    public async Task TheRoomHoldsYourFriendsAndNobodyElse()
    {
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);
        await ContributedRoomAsync(stranger);

        var mine = await ContributedRoomAsync(Client);

        // Connected by a request I have not accepted, so not a friend.
        Assert.Empty(mine.Entries);
    }

    [Fact]
    public async Task ASecondPictureReplacesTheFirstAndTheOldOneIsGone()
    {
        var first = await ContributedRoomAsync(Client);
        var firstImage = first.OwnEntry!.ImageId!.Value;

        var second = await ContributedRoomAsync(Client);

        Assert.Equal(first.OwnEntry.Id, second.OwnEntry!.Id);
        Assert.NotEqual(firstImage, second.OwnEntry.ImageId);

        // The challenge is a moment, not a collection: the replaced picture is
        // not left behind spending somebody's storage allowance.
        var orphan = await Client.GetAsync($"/api/images/{firstImage}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, orphan.StatusCode);
    }

    [Fact]
    public async Task WithdrawingTakesThePictureAndTheViewWithIt()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await ContributedRoomAsync(friend);

        var mine = await ContributedRoomAsync(Client);
        var imageId = mine.OwnEntry!.ImageId!.Value;

        var after = await (await Client.DeleteAsync(
            "/api/challenges/today/entry",
            TestContext.Current.CancellationToken)).ReadAsync<TodayDocument>();

        Assert.Null(after.Room!.OwnEntry);

        // The rule reads the same in both directions: an exit that kept the
        // view would be the spectator's way back in.
        Assert.False(after.Room.IsRevealed);
        Assert.Null(Assert.Single(after.Room.Entries).ImageId);

        var gone = await Client.GetAsync($"/api/images/{imageId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task ThereIsNothingToWithdrawIfYouNeverJoinedIn()
    {
        var response = await Client.DeleteAsync(
            "/api/challenges/today/entry",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AContributionNeedsAPictureOfYourOwnUploadedForThis()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await UploadAsync(friend);

        var withoutImage = await Client.PostJsonAsync("/api/challenges/today/entry", new { capturedInApp = true });
        Assert.Equal(HttpStatusCode.BadRequest, withoutImage.StatusCode);

        // Otherwise "contribute this id" would be a way to put somebody else's
        // picture in the room — and contributing is how the room opens.
        var borrowed = await Client.PostJsonAsync(
            "/api/challenges/today/entry",
            new { imageId = theirs, capturedInApp = true });

        Assert.Equal(HttpStatusCode.NotFound, borrowed.StatusCode);
    }

    /// <summary>
    /// A proof photograph and a challenge contribution have different
    /// audiences, so an image uploaded for one is not usable as the other.
    /// </summary>
    [Fact]
    public async Task AProofPhotographIsNotAChallengeContribution()
    {
        var response = await Client.PostJsonAsync(
            "/api/challenges/today/entry",
            new { imageId = await Client.UploadProofImageAsync(), capturedInApp = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AReactionTogglesAndOnlySomebodyInTheRoomMayGiveOne()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await ContributedRoomAsync(friend);
        var entryId = theirs.OwnEntry!.Id;

        // Not yet in the room: reacting is seeing, so the answer is the same
        // 404 that the picture gets.
        var early = await Client.PostJsonAsync(
            $"/api/challenges/entries/{entryId}/reactions",
            new { kind = KudosKind.Applause });

        Assert.Equal(HttpStatusCode.NotFound, early.StatusCode);

        await ContributedRoomAsync(Client);

        var added = await (await Client.PostJsonAsync(
            $"/api/challenges/entries/{entryId}/reactions",
            new { kind = KudosKind.Applause })).ReadAsync<EntryDocument>();

        var reaction = Assert.Single(added.Reactions);
        Assert.Equal(nameof(KudosKind.Applause), reaction.Kind);
        Assert.True(reaction.IsMine);

        var removed = await (await Client.PostJsonAsync(
            $"/api/challenges/entries/{entryId}/reactions",
            new { kind = KudosKind.Applause })).ReadAsync<EntryDocument>();

        Assert.Empty(removed.Reactions);
    }

    [Fact]
    public async Task AStrangerCannotReactToSomethingTheyAreNotShown()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var theirs = await ContributedRoomAsync(friend);

        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);
        await ContributedRoomAsync(stranger);

        var response = await stranger.PostJsonAsync(
            $"/api/challenges/entries/{theirs.OwnEntry!.Id}/reactions",
            new { kind = KudosKind.Fire });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The archive is your own memory and nobody else's collection. It starts
    /// from your contributions, so a friend's picture is never in the result to
    /// be filtered out.
    /// </summary>
    [Fact]
    public async Task TheArchiveHoldsYourOwnContributionsOnly()
    {
        Assert.Empty(await ArchiveAsync(Client));

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        await ContributedRoomAsync(friend);

        var mine = await ContributedRoomAsync(Client);

        var archive = await ArchiveAsync(Client);
        var kept = Assert.Single(archive);

        Assert.Equal(mine.OwnEntry!.Id, kept.Entry.Id);
        Assert.True(kept.Entry.IsMine);

        // The prompt travels with the picture. A photograph of a desk says
        // nothing in six months; the task it answered does.
        Assert.Equal(AutomatedTestSeed.ChallengePrompt, kept.Challenge.Prompt);

        // The friend's contribution is in my room, not in my archive.
        Assert.DoesNotContain(archive, entry => entry.Entry.Author.Id == AutomatedTestSeed.FriendPersonId);
    }

    [Fact]
    public async Task NoneOfItIsReachableWithoutASession()
    {
        var today = await AnonymousClient.GetAsync(
            "/api/challenges/today",
            TestContext.Current.CancellationToken);

        var archive = await AnonymousClient.GetAsync(
            "/api/challenges/archive",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, today.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, archive.StatusCode);
    }
}
