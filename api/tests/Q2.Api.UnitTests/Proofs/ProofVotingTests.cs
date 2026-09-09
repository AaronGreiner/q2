using Q2.Api.Features.Proofs;

namespace Q2.Api.UnitTests.Proofs;

/// <summary>
/// The rule the product turns on, tested where it lives.
/// </summary>
/// <remarks>
/// Written as votes and counts rather than through the endpoint on purpose: a
/// threshold is arithmetic, and arithmetic that can only be checked by
/// uploading a photograph and signing in as four people is arithmetic nobody
/// will check again.
/// </remarks>
public sealed class ProofVotingTests
{
    private static IReadOnlyCollection<VoteValue> Votes(int confirms, int doubts) =>
    [
        .. Enumerable.Repeat(VoteValue.Confirm, confirms),
        .. Enumerable.Repeat(VoteValue.Doubt, doubts),
    ];

    [Fact]
    public void StaysOpenUntilEverybodyHasSpoken()
    {
        var outcome = ProofVoting.Evaluate(Votes(confirms: 1, doubts: 0), expectedVoterCount: 3, attempt: 1);

        Assert.Equal(VotingResult.Running, outcome.Result);
    }

    [Fact]
    public void ConfirmsOnceEverybodyHasAndNobodyObjected()
    {
        var outcome = ProofVoting.Evaluate(Votes(confirms: 3, doubts: 0), expectedVoterCount: 3, attempt: 1);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
    }

    [Fact]
    public void ConfirmsImmediatelyWhenThereIsNobodyToAsk()
    {
        // A goal shared with no one stays the self-report q2 has always had.
        // Waiting twelve hours for a photograph no friend will look at would be
        // ceremony, not accountability.
        var outcome = ProofVoting.Evaluate([], expectedVoterCount: 0, attempt: 1);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
    }

    [Fact]
    public void OneDoubtIsNeverEnough()
    {
        // Half the votes, and still not a dispute: a single person must not be
        // able to sink a friend's proof alone. This is the case the ratio on
        // its own gets wrong.
        var outcome = ProofVoting.Evaluate(Votes(confirms: 1, doubts: 1), expectedVoterCount: 2, attempt: 1);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
        Assert.Equal(0.5, outcome.DoubtRatio);
    }

    [Fact]
    public void TwoDoubtsOutOfThreeDisputeIt()
    {
        var outcome = ProofVoting.Evaluate(Votes(confirms: 1, doubts: 2), expectedVoterCount: 3, attempt: 1);

        Assert.Equal(VotingResult.Retry, outcome.Result);
    }

    [Fact]
    public void ExactlyAThirdIsNotEnough()
    {
        // The threshold is "more than a third", so two doubts out of six is
        // short of it. The boundary belongs in a test because an off-by-one
        // here decides whether somebody keeps a streak.
        var outcome = ProofVoting.Evaluate(Votes(confirms: 4, doubts: 2), expectedVoterCount: 6, attempt: 1);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
    }

    [Fact]
    public void JustOverAThirdIs()
    {
        var outcome = ProofVoting.Evaluate(Votes(confirms: 3, doubts: 2), expectedVoterCount: 5, attempt: 1);

        Assert.Equal(VotingResult.Retry, outcome.Result);
    }

    [Fact]
    public void DecidesADisputeWithoutWaitingForTheRest()
    {
        // Two of five have doubted. Whatever the other three say, the share
        // cannot fall back under a third, so making somebody wait to hear it
        // would be a slower answer rather than a fairer one.
        var outcome = ProofVoting.Evaluate(Votes(confirms: 0, doubts: 2), expectedVoterCount: 5, attempt: 1);

        Assert.Equal(VotingResult.Retry, outcome.Result);
    }

    [Fact]
    public void TheSecondDisputedAttemptIsTheLast()
    {
        var first = ProofVoting.Evaluate(Votes(confirms: 0, doubts: 2), expectedVoterCount: 3, attempt: 1);
        var second = ProofVoting.Evaluate(Votes(confirms: 0, doubts: 2), expectedVoterCount: 3, attempt: 2);

        Assert.Equal(VotingResult.Retry, first.Result);
        Assert.Equal(VotingResult.Rejected, second.Result);

        Assert.True(ProofVoting.HasAttemptLeft(1));
        Assert.False(ProofVoting.HasAttemptLeft(2));
    }

    [Fact]
    public void SilenceIsNotMistrustWhenTheDeadlinePasses()
    {
        // Nobody voted at all. Confirming is the only answer that does not let
        // an inattentive friend break a streak by doing nothing.
        var outcome = ProofVoting.ResolveAfterDeadline([], attempt: 1);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
        Assert.Equal(0, outcome.DoubtRatio);
    }

    [Fact]
    public void ADeadlineDoesNotRescueADisputedProof()
    {
        var outcome = ProofVoting.ResolveAfterDeadline(Votes(confirms: 0, doubts: 2), attempt: 1);

        Assert.Equal(VotingResult.Retry, outcome.Result);
    }

    [Fact]
    public void ADeadlineWithOneDoubtConfirms()
    {
        // The floor of two doubts applies after the deadline as well; one
        // objection out of one vote is not a verdict.
        var outcome = ProofVoting.ResolveAfterDeadline(Votes(confirms: 0, doubts: 1), attempt: 2);

        Assert.Equal(VotingResult.Confirmed, outcome.Result);
    }

    [Fact]
    public void ReportsTheRatioItDecidedOn()
    {
        // Over votes cast, never over people invited: two doubts out of three
        // votes is two thirds even when ten people could have voted.
        var outcome = ProofVoting.Evaluate(Votes(confirms: 1, doubts: 2), expectedVoterCount: 10, attempt: 1);

        Assert.Equal(2.0 / 3.0, outcome.DoubtRatio, precision: 10);
    }

    [Fact]
    public void RejectsNothingWhenNobodyHasVotedYet()
    {
        var outcome = ProofVoting.Evaluate([], expectedVoterCount: 4, attempt: 2);

        Assert.Equal(VotingResult.Running, outcome.Result);
        Assert.Equal(0, outcome.DoubtRatio);
    }
}
