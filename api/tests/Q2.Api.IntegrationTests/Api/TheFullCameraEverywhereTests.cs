using System.Net;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The full camera everywhere but a profile picture (#152): a challenge
/// contribution, a chat photograph and a Don't's logged use may each be a
/// clip, with the front camera's picture or clip beside it — through the real
/// pipeline and the real file store.
/// </summary>
/// <remarks>
/// What is worth the pipeline here is the same as for a proof: that every part
/// has exactly the audience of the picture it came with, no wider, and goes
/// with it — withdrawn, replaced, taken back or undone.
/// </remarks>
[Trait("Category", "Integration")]
public class TheFullCameraEverywhereTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record EntryDocument(
        Guid Id,
        Guid? ImageId,
        ProofSource Source,
        MediaKind Media,
        Guid? PosterImageId,
        Guid? InsetImageId,
        MediaKind? InsetMedia,
        Guid? InsetPosterImageId);

    private sealed record RoomDocument(EntryDocument? OwnEntry, IReadOnlyList<EntryDocument> Entries, bool IsRevealed);

    private sealed record ImageDocument(Guid Id, MediaKind Kind);

    private sealed record MessageDocument(
        Guid Id,
        ImageDocument? Image,
        bool IsMine,
        Guid? PosterImageId,
        Guid? InsetImageId,
        MediaKind? InsetMedia,
        Guid? InsetPosterImageId);

    private sealed record ThreadDocument(IReadOnlyList<MessageDocument> Messages, IReadOnlyList<EventDocument> Events);

    private sealed record EventDocument(
        GoalEventKind Kind,
        Guid? ImageId,
        MediaKind? Media,
        Guid? PosterImageId,
        Guid? InsetImageId,
        MediaKind? InsetMedia,
        Guid? InsetPosterImageId);

    private sealed record GridDocument(Guid Id, bool IsProof, MediaKind Media, Guid? PosterImageId, Guid? InsetImageId, MediaKind? InsetMedia, Guid? InsetPosterImageId);

    private sealed record WindowDocument(Guid? LatestUseId);

    private sealed record GoalDocument(Guid Id, WindowDocument? Current);

    private sealed record GoalDetailDocument(GoalDocument Goal, Guid? ConversationId);

    /// <summary>A clip, its still, and the front camera's clip with its still, all uploaded for <paramref name="purpose"/>.</summary>
    private sealed record TwoClips(Guid Clip, Guid Still, Guid FrontClip, Guid FrontStill)
    {
        public Guid[] All => [Clip, Still, FrontClip, FrontStill];

        public object Body() => new
        {
            imageId = Clip,
            posterImageId = Still,
            insetImageId = FrontClip,
            insetPosterImageId = FrontStill,
            source = ProofSource.Live,
        };
    }

    private static async Task<TwoClips> UploadTwoClipsAsync(HttpClient client, ImagePurpose purpose) =>
        new(
            await client.UploadVideoAsync(purpose),
            await client.UploadImageAsync(purpose),
            await client.UploadVideoAsync(purpose),
            await client.UploadImageAsync(purpose));

    private static async Task<RoomDocument> ContributeAsync(HttpClient client, object body)
    {
        var response = await client.PostJsonAsync("/api/challenges/today/entry", body);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<RoomDocument>();
    }

    private static async Task<HttpStatusCode> StatusOfAsync(HttpClient client, Guid imageId) =>
        (await client.GetAsync($"/api/images/{imageId}", TestContext.Current.CancellationToken)).StatusCode;

    /// <summary>Only an avatar may not move: a challenge contribution and a chat photograph may now be clips.</summary>
    [Theory]
    [InlineData(ImagePurpose.ChallengeEntry)]
    [InlineData(ImagePurpose.ChatPhoto)]
    public async Task AChallengeContributionAndAChatPhotographMayBeClips(ImagePurpose purpose)
    {
        var clip = await Client.UploadVideoAsync(purpose);

        Assert.NotEqual(Guid.Empty, clip);
    }

    [Fact]
    public async Task AChallengeContributionMayBeTwoClipsFilmedAtOnce()
    {
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.ChallengeEntry);

        var room = await ContributeAsync(Client, parts.Body());

        var mine = room.OwnEntry!;
        Assert.Equal(MediaKind.Video, mine.Media);
        Assert.Equal(parts.Still, mine.PosterImageId);
        Assert.Equal(parts.FrontClip, mine.InsetImageId);
        Assert.Equal(MediaKind.Video, mine.InsetMedia);
        Assert.Equal(parts.FrontStill, mine.InsetPosterImageId);
    }

    /// <summary>
    /// The room's rule covers every part: a friend sees none of them until they
    /// have taken part, and then all of them; a stranger never.
    /// </summary>
    [Fact]
    public async Task EveryPartOfAContributionIsCoveredUntilYouHaveTakenPart()
    {
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.ChallengeEntry);
        await ContributeAsync(Client, parts.Body());

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var covered = Assert.Single((await (await friend.GetAsync("/api/challenges/today", TestContext.Current.CancellationToken))
            .ReadAsync<TodayDocument>()).Room!.Entries);
        Assert.Null(covered.ImageId);
        Assert.Null(covered.PosterImageId);
        Assert.Null(covered.InsetImageId);
        Assert.Null(covered.InsetPosterImageId);

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(friend, id));
        }

        var opened = await ContributeAsync(friend, new { imageId = await friend.UploadImageAsync(ImagePurpose.ChallengeEntry), source = ProofSource.Live });
        var revealed = Assert.Single(opened.Entries);
        Assert.Equal(parts.FrontClip, revealed.InsetImageId);

        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(friend, id));
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(stranger, id));
        }
    }

    private sealed record TodayDocument(RoomDocument? Room);

    /// <summary>The picker stays the challenge's route — as a screenshot, and never for a clip or a second camera.</summary>
    [Fact]
    public async Task APickedContributionCannotBeAClipOrClaimASecondCamera()
    {
        var clip = await Client.PostJsonAsync("/api/challenges/today/entry", new
        {
            imageId = await Client.UploadVideoAsync(ImagePurpose.ChallengeEntry),
            posterImageId = await Client.UploadImageAsync(ImagePurpose.ChallengeEntry),
            source = ProofSource.Screenshot,
        });
        Assert.Equal(HttpStatusCode.BadRequest, clip.StatusCode);

        var front = await Client.PostJsonAsync("/api/challenges/today/entry", new
        {
            imageId = await Client.UploadImageAsync(ImagePurpose.ChallengeEntry),
            insetImageId = await Client.UploadImageAsync(ImagePurpose.ChallengeEntry),
            source = ProofSource.Screenshot,
        });
        Assert.Equal(HttpStatusCode.BadRequest, front.StatusCode);

        var screenshot = await ContributeAsync(Client, new { imageId = await Client.UploadImageAsync(ImagePurpose.ChallengeEntry), source = ProofSource.Screenshot });
        Assert.Equal(ProofSource.Screenshot, screenshot.OwnEntry!.Source);
    }

    /// <summary>A part uploaded for something else would be a way to show it to the room.</summary>
    [Fact]
    public async Task AContributionsPartsAreUploadedForIt()
    {
        var response = await Client.PostJsonAsync("/api/challenges/today/entry", new
        {
            imageId = await Client.UploadImageAsync(ImagePurpose.ChallengeEntry),
            insetImageId = await Client.UploadProofImageAsync(),
            source = ProofSource.Live,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReplacingOrWithdrawingAContributionTakesEveryPartWithIt()
    {
        var first = await UploadTwoClipsAsync(Client, ImagePurpose.ChallengeEntry);
        await ContributeAsync(Client, first.Body());

        var second = await UploadTwoClipsAsync(Client, ImagePurpose.ChallengeEntry);
        await ContributeAsync(Client, second.Body());

        foreach (var id in first.All)
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(Client, id));
        }

        (await Client.DeleteAsync("/api/challenges/today/entry", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        foreach (var id in second.All)
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(Client, id));
        }
    }

    [Fact]
    public async Task AChatPhotographMayBeTwoClipsAndEveryPartIsTheConversations()
    {
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.ChatPhoto);

        var sent = await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", parts.Body());
        Assert.True(sent.IsSuccessStatusCode, await sent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var thread = await (await friend.GetAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();
        var message = Assert.Single(thread.Messages, entry => entry.Image?.Id == parts.Clip);
        Assert.Equal(MediaKind.Video, message.Image!.Kind);
        Assert.Equal(parts.Still, message.PosterImageId);
        Assert.Equal(parts.FrontClip, message.InsetImageId);
        Assert.Equal(MediaKind.Video, message.InsetMedia);
        Assert.Equal(parts.FrontStill, message.InsetPosterImageId);

        var grid = await (await friend.GetAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/photos", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<GridDocument>>();
        var cell = Assert.Single(grid, entry => entry.Id == message.Id);
        Assert.False(cell.IsProof);
        Assert.Equal(MediaKind.Video, cell.Media);
        Assert.Equal(parts.FrontStill, cell.InsetPosterImageId);

        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(friend, id));
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(stranger, id));
        }
    }

    /// <summary>Once only, for every part: the same id in two threads would be readable from both.</summary>
    [Fact]
    public async Task APartOfAChatPhotographCannotBeSentTwice()
    {
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.ChatPhoto);
        (await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", parts.Body())).EnsureSuccessStatusCode();

        var again = await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", new
        {
            imageId = await Client.UploadImageAsync(ImagePurpose.ChatPhoto),
            insetImageId = parts.FrontStill,
        });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task TakingAChatPhotographBackTakesEveryPartWithIt()
    {
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.ChatPhoto);
        (await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", parts.Body())).EnsureSuccessStatusCode();

        var thread = await (await Client.GetAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();
        var message = Assert.Single(thread.Messages, entry => entry.Image?.Id == parts.Clip);

        (await Client.DeleteAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages/{message.Id}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(Client, id));
        }
    }

    private async Task<GoalDocument> CreateDontAsync()
    {
        var created = await Client.PostJsonAsync("/api/goals", new
        {
            title = "Keine Süßigkeiten",
            kind = "Dont",
            limit = 3,
            proofMode = "HonestOnly",
            schedule = new { kind = "Limit", period = "Week" },
            participantIds = new[] { AutomatedTestSeed.FriendPersonId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return await created.ReadAsync<GoalDocument>();
    }

    [Fact]
    public async Task AUseMayBeLoggedWithTwoClipsThatTheGoalsPeopleSee()
    {
        var goal = await CreateDontAsync();
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.Proof);

        var logged = await Client.PostJsonAsync($"/api/goals/{goal.Id}/uses", parts.Body());
        Assert.True(logged.IsSuccessStatusCode, await logged.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var detail = await (await Client.GetAsync($"/api/goals/{goal.Id}", TestContext.Current.CancellationToken))
            .ReadAsync<GoalDetailDocument>();

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var thread = await (await friend.GetAsync($"/api/chats/{detail.ConversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();
        var use = Assert.Single(thread.Events, entry => entry.Kind == GoalEventKind.UseLogged);
        Assert.Equal(parts.Clip, use.ImageId);
        Assert.Equal(MediaKind.Video, use.Media);
        Assert.Equal(parts.Still, use.PosterImageId);
        Assert.Equal(parts.FrontClip, use.InsetImageId);
        Assert.Equal(MediaKind.Video, use.InsetMedia);
        Assert.Equal(parts.FrontStill, use.InsetPosterImageId);

        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(friend, id));
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(stranger, id));
        }
    }

    /// <summary>A clip without its still would be a blank line in the conversation.</summary>
    [Fact]
    public async Task AUsesClipNeedsItsStill()
    {
        var goal = await CreateDontAsync();

        var response = await Client.PostJsonAsync($"/api/goals/{goal.Id}/uses", new { imageId = await Client.UploadProofVideoAsync() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UndoingAUseTakesEveryPartWithIt()
    {
        var goal = await CreateDontAsync();
        var parts = await UploadTwoClipsAsync(Client, ImagePurpose.Proof);

        var logged = await (await Client.PostJsonAsync($"/api/goals/{goal.Id}/uses", parts.Body())).ReadAsync<GoalDocument>();

        (await Client.DeleteAsync($"/api/goals/{goal.Id}/uses/{logged.Current!.LatestUseId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        foreach (var id in parts.All)
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusOfAsync(Client, id));
        }
    }
}
