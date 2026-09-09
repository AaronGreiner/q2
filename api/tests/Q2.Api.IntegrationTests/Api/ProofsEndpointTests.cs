using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// The half of the product that is not q2: a photograph other people judge.
/// </summary>
/// <remarks>
/// The arithmetic of the vote is covered by <c>ProofVotingTests</c>, which can
/// state a tally in one line. What is worth going through the pipeline for is
/// everything the arithmetic cannot say on its own — who is allowed to vote,
/// whose name comes back, what a friend can see of somebody else's picture, and
/// whether a deadline decides anything when nobody has the app open.
/// </remarks>
[Trait("Category", "Integration")]
public class ProofsEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record FeedCardDocument(ProofDocument Proof, string GoalTitle, string GoalIcon);

    private async Task<HttpResponseMessage> VoteAsync(HttpClient client, Guid proofId, VoteValue value) =>
        await client.PostJsonAsync($"/api/proofs/{proofId}/vote", new { value });

    private async Task<IReadOnlyList<FeedCardDocument>> PendingAsync(HttpClient client) =>
        await (await client.GetAsync("/api/proofs/pending", TestContext.Current.CancellationToken))
            .ReadAsync<IReadOnlyList<FeedCardDocument>>();

    [Fact]
    public async Task APhotographStartsAVoteWithADeadlineOnIt()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Equal(ProofStatus.Voting, proof.Status);
        Assert.Equal(1, proof.Attempt);
        Assert.Equal(1, proof.AttemptsLeft);
        Assert.Equal(Q2ApiFactory.Now.AddHours(ProofVoting.VotingWindowHours), proof.ExpiresAt);

        // Never their own: the person who made the promise does not get to
        // vouch for themselves.
        Assert.False(proof.Votes.CanIVote);
    }

    [Fact]
    public async Task AFriendOnTheGoalMayVoteAndSeesTheirOwnVoteAfterwards()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var before = await (await friend.GetAsync(
            $"/api/proofs/{proof.Id}",
            TestContext.Current.CancellationToken)).ReadAsync<ProofDocument>();

        Assert.True(before.Votes.CanIVote);
        Assert.Null(before.Votes.MyVote);

        var after = await (await VoteAsync(friend, proof.Id, VoteValue.Confirm)).ReadAsync<ProofDocument>();

        Assert.Equal(VoteValue.Confirm, after.Votes.MyVote);
        Assert.False(after.Votes.CanIVote);
        Assert.Equal(1, after.Votes.ConfirmCount);
    }

    [Fact]
    public async Task OneSayPerPerson()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        (await VoteAsync(friend, proof.Id, VoteValue.Confirm)).EnsureSuccessStatusCode();

        // A vote you can revise once you have seen the tally is a negotiation,
        // not an opinion.
        var second = await VoteAsync(friend, proof.Id, VoteValue.Doubt);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task TheUploaderCannotVoteOnTheirOwnPhotograph()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var response = await VoteAsync(Client, proof.Id, VoteValue.Confirm);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SomebodyWhoIsNotOnTheGoalSeesNothingOfIt()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        // 404 in both directions, because 403 would confirm the photograph
        // exists — and its existence is itself somebody's business.
        var read = await stranger.GetAsync($"/api/proofs/{proof.Id}", TestContext.Current.CancellationToken);
        var vote = await VoteAsync(stranger, proof.Id, VoteValue.Confirm);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, vote.StatusCode);
    }

    /// <summary>
    /// The rule the whole check depends on. If a doubter could be named, most
    /// people would confirm out of politeness and the vote would be theatre.
    /// </summary>
    [Fact]
    public async Task ADoubterIsNeverNamed()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await VoteAsync(friend, proof.Id, VoteValue.Doubt);

        var response = await Client.GetAsync($"/api/proofs/{proof.Id}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = await (await Client.GetAsync(
            $"/api/proofs/{proof.Id}",
            TestContext.Current.CancellationToken)).ReadAsync<ProofDocument>();

        Assert.Equal(1, document.Votes.DoubtCount);
        Assert.Empty(document.Votes.ConfirmedBy);

        // Not merely absent from the mapping: the doubter's id and name are
        // nowhere in the payload at all.
        Assert.DoesNotContain(AutomatedTestSeed.FriendPersonId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test.two", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AConfirmerIsNamed()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        await VoteAsync(friend, proof.Id, VoteValue.Confirm);

        var document = await (await Client.GetAsync(
            $"/api/proofs/{proof.Id}",
            TestContext.Current.CancellationToken)).ReadAsync<ProofDocument>();

        // Agreeing costs nothing to admit, so it comes with a name.
        Assert.Contains(document.Votes.ConfirmedBy, person => person.Id == AutomatedTestSeed.FriendPersonId);
    }

    [Fact]
    public async Task TheFeedHoldsWhatIsWaitingForYouAndNothingElse()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var waiting = await PendingAsync(friend);
        Assert.Contains(waiting, card => card.Proof.Id == proof.Id);
        Assert.All(waiting, card => Assert.False(string.IsNullOrWhiteSpace(card.GoalTitle)));

        // A card you cannot vote on is a card that should not be in this list.
        // Without the goal's participants loaded, every one of them came back
        // saying "your own proof" to the person it was waiting for.
        Assert.All(waiting, card => Assert.True(card.Proof.Votes.CanIVote));

        // Never your own photograph: there is nothing for you to decide.
        Assert.DoesNotContain(await PendingAsync(Client), card => card.Proof.Id == proof.Id);

        await VoteAsync(friend, proof.Id, VoteValue.Confirm);

        // And it leaves the moment you have had your say.
        Assert.DoesNotContain(await PendingAsync(friend), card => card.Proof.Id == proof.Id);
    }

    [Fact]
    public async Task AStrangerNeverSeesAPhotographInTheirFeed()
    {
        await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        Assert.Empty(await PendingAsync(await ClientForAsync(AutomatedTestSeed.RequesterEmail)));
    }

    [Fact]
    public async Task AFriendOnTheGoalMaySeeThePhotographItself()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        // The audience of the picture is the audience of the goal — not
        // "everybody signed in", the way an avatar is, and not "friends", which
        // is not an invitation to somebody's goals.
        (await friend.GetAsync($"/api/images/{proof.ImageId}", TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var refused = await stranger.GetAsync(
            $"/api/images/{proof.ImageId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    [Fact]
    public async Task ReactingIsSeparateFromVoting()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var reacted = await (await friend.PostJsonAsync(
            $"/api/proofs/{proof.Id}/reactions",
            new { kind = "Fire" })).ReadAsync<ProofDocument>();

        // Applauding is not agreeing: the vote has not been cast.
        Assert.Contains(reacted.Reactions, reaction => reaction is { Kind: "Fire", Count: 1, IsMine: true });
        Assert.True(reacted.Votes.CanIVote);
        Assert.Null(reacted.Votes.MyVote);

        // The same kind again takes it back.
        var undone = await (await friend.PostJsonAsync(
            $"/api/proofs/{proof.Id}/reactions",
            new { kind = "Fire" })).ReadAsync<ProofDocument>();

        Assert.Empty(undone.Reactions);
    }

    /// <summary>
    /// "Das Ergebnis steht nach Fristablauf auch ohne offene App fest" — the
    /// stage's own completion criterion, and the reason the maintenance job
    /// exists at all.
    /// </summary>
    [Fact]
    public async Task AVoteNobodyAnsweredIsSettledByItsDeadline()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        await AdvanceBeyondTheVotingDeadlineAsync();

        var settled = await (await Client.GetAsync(
            $"/api/proofs/{proof.Id}",
            TestContext.Current.CancellationToken)).ReadAsync<ProofDocument>();

        // Silence is not mistrust. Nobody objected, so it stands.
        Assert.Equal(ProofStatus.Confirmed, settled.Status);
    }

    [Fact]
    public async Task ADoubtedPhotographCostsTheWindowOnItsSecondAttempt()
    {
        // Two independent doubters is the floor, so this needs a goal with two
        // voters — the seed's shared goal has one, and a second is added here.
        await ShareActiveGoalWithAsync(AutomatedTestSeed.RequestingPersonId);

        var first = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        await DoubtByBothAsync(first.Id);

        var afterFirst = await (await Client.GetAsync(
            $"/api/proofs/{first.Id}",
            TestContext.Current.CancellationToken)).ReadAsync<ProofDocument>();

        // Refused, but not fatal: one more attempt is exactly what stops a bad
        // photograph from costing a streak that was actually earned.
        Assert.Equal(ProofStatus.Rejected, afterFirst.Status);

        var second = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        Assert.Equal(2, second.Attempt);
        Assert.Equal(0, second.AttemptsLeft);

        await DoubtByBothAsync(second.Id);

        var detail = await (await Client.GetAsync(
            $"/api/goals/{AutomatedTestSeed.ActiveGoalId}",
            TestContext.Current.CancellationToken)).ReadAsync<GoalDetailDocument>();

        // The chain breaks. This is the moment q2 could not previously express:
        // two delivered days behind it, and a window that is now a miss.
        Assert.Equal(0, detail.Goal.Streak);
        Assert.Equal(2, detail.Goal.WindowsDone);
        Assert.Equal(1, detail.Goal.WindowsMissed);
    }

    private sealed record GoalDetailDocument(GoalNumbersDocument Goal);

    private sealed record GoalNumbersDocument(Guid Id, int Streak, int WindowsDone, int WindowsMissed);

    private async Task DoubtByBothAsync(Guid proofId)
    {
        foreach (var email in new[] { AutomatedTestSeed.FriendEmail, AutomatedTestSeed.RequesterEmail })
        {
            var voter = await ClientForAsync(email);
            (await VoteAsync(voter, proofId, VoteValue.Doubt)).EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Adds a second voter to the seeded shared goal.
    /// </summary>
    /// <remarks>
    /// Written straight to the database because there is no endpoint for it —
    /// participants are chosen when a goal is created, and stage 4 did not add
    /// a way to invite somebody afterwards. A test that needs two voters should
    /// say so plainly rather than build a whole second goal to get there.
    /// </remarks>
    private async Task ShareActiveGoalWithAsync(Guid personId) =>
        await Factory.WithDatabaseAsync(async database =>
        {
            var goal = await database.Goals
                .Include(candidate => candidate.Participants)
                .SingleAsync(candidate => candidate.Id == AutomatedTestSeed.ActiveGoalId, TestContext.Current.CancellationToken);

            goal.AddParticipant(Guid.CreateVersion7(), personId);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

    /// <summary>
    /// Moves every open vote past its deadline and runs the job that decides
    /// them, which is what happens overnight while nobody is looking.
    /// </summary>
    /// <remarks>
    /// The deadline is moved rather than the clock, because the clock is fixed
    /// for the whole fixture and a test that changed it would change what
    /// "today" means for every other assertion in the same run.
    /// </remarks>
    private async Task AdvanceBeyondTheVotingDeadlineAsync()
    {
        await Factory.WithDatabaseAsync(async database =>
        {
            await database.ProofPhotos
                .Where(proof => proof.Status == ProofStatus.Voting)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        proof => proof.VotingDeadline,
                        Q2ApiFactory.Now.AddHours(-1)),
                    TestContext.Current.CancellationToken);
        });

        using var scope = Factory.Services.CreateScope();
        var worker = ActivatorUtilities.CreateInstance<GoalMaintenanceWorker>(scope.ServiceProvider);

        await worker.RunOnceAsync(TestContext.Current.CancellationToken);
    }
}
