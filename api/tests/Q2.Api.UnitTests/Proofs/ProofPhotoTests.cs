using Q2.Api.Features.Proofs;

namespace Q2.Api.UnitTests.Proofs;

/// <summary>
/// Who may cast a vote on a photograph, and what a second one does.
/// </summary>
public sealed class ProofPhotoTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Owner = Guid.CreateVersion7();

    private static readonly Guid Friend = Guid.CreateVersion7();

    private static ProofPhoto Delivered() =>
        ProofPhoto.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Owner, Guid.CreateVersion7(), 1, true, Now);

    [Fact]
    public void ASecondVoteReplacesTheFirstRatherThanCountingTwice()
    {
        var proof = Delivered();

        Assert.True(proof.CastVote(Guid.CreateVersion7(), Friend, VoteValue.Confirm, Now));
        Assert.True(proof.CastVote(Guid.CreateVersion7(), Friend, VoteValue.Doubt, Now.AddMinutes(5)));

        Assert.Single(proof.Votes);
        Assert.Equal(VoteValue.Doubt, proof.VoteOf(Friend));
        Assert.Empty(proof.ConfirmedBy);
    }

    [Fact]
    public void TheUploaderCannotVouchForThemselves()
    {
        Assert.False(Delivered().CastVote(Guid.CreateVersion7(), Owner, VoteValue.Confirm, Now));
    }

    [Fact]
    public void ADecidedPhotographTakesNoMoreVotesAndNoChanges()
    {
        var proof = Delivered();
        proof.CastVote(Guid.CreateVersion7(), Friend, VoteValue.Confirm, Now);
        proof.Resolve(VotingResult.Confirmed, Now);

        Assert.False(proof.CastVote(Guid.CreateVersion7(), Friend, VoteValue.Doubt, Now));
        Assert.Equal(VoteValue.Confirm, proof.VoteOf(Friend));
    }
}
