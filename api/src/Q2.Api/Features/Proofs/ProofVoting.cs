namespace Q2.Api.Features.Proofs;

/// <summary>How a vote came down.</summary>
/// <remarks>
/// Two values and not three. "Abstained" is not a vote — somebody who says
/// nothing has said nothing, and <see cref="ProofVoting.ResolveAfterDeadline"/>
/// treats silence as exactly that rather than as suspicion.
/// </remarks>
public enum VoteValue
{
    /// <summary>"I believe this." Shown with the voter's name.</summary>
    Confirm,

    /// <summary>"I do not believe this." Never shown with a name.</summary>
    Doubt,
}

/// <summary>What a round of voting has decided, if anything.</summary>
public enum VotingResult
{
    /// <summary>Not enough has happened yet. The vote stays open.</summary>
    Running,

    /// <summary>Accepted. The proof counts towards its window.</summary>
    Confirmed,

    /// <summary>Too much doubt, but there is still an attempt left.</summary>
    Retry,

    /// <summary>Too much doubt and no attempt left. The window is not delivered.</summary>
    Rejected,
}

/// <summary>The outcome, and the ratio it was decided on.</summary>
/// <param name="DoubtRatio">
/// Doubts over votes cast — not over people invited. Carried out so the client
/// can show how close it was without recomputing a rule that lives here.
/// </param>
public readonly record struct VotingOutcome(VotingResult Result, double DoubtRatio);

/// <summary>
/// Whether a photograph is believed.
/// </summary>
/// <remarks>
/// This is the rule the whole product turns on, ported from the source
/// project's <c>domain/voting.ts</c>, and it is a pure function over the votes
/// cast for exactly the reason it was one there: it has to be testable on its
/// own, and it has to be the *server's* answer. The client may run the same
/// arithmetic to show a result immediately, but nothing it computes is binding
/// — a threshold evaluated in a browser is a threshold somebody can edit
/// (section 7e of the migration plan).
///
/// Four numbers carry it, and each answers a way the vote could be gamed or
/// stalled:
///
/// - <see cref="DoubtThreshold"/> — more than a third of the votes cast have to
///   be doubts. A simple majority would make a tie a rejection.
/// - <see cref="MinDoubtVotes"/> — and at least two of them. Without this floor
///   a single person could sink a proof alone in any pair, because one doubt out
///   of two votes is already half. Two independent doubters is a far more
///   reliable statement than one.
/// - <see cref="MaxAttempts"/> — a doubted proof may be delivered again once.
///   Losing a streak to a bad photograph rather than a broken promise is the
///   failure mode that would make people stop trusting the app.
/// - <see cref="VotingWindowHours"/> — a deadline, because without one a single
///   friend who never votes would freeze a daily goal permanently: the window
///   would never close, the streak would never grow, and no new window would
///   ever open behind it.
/// </remarks>
public static class ProofVoting
{
    /// <summary>The share of doubt above which a proof is disputed.</summary>
    public const double DoubtThreshold = 1.0 / 3.0;

    /// <summary>How many doubts it takes before the share means anything.</summary>
    public const int MinDoubtVotes = 2;

    /// <summary>How many times one window may be proved. The second is the retry.</summary>
    public const int MaxAttempts = 2;

    /// <summary>How long a vote stays open.</summary>
    public const int VotingWindowHours = 12;

    /// <summary>
    /// Evaluates a vote that is still running.
    /// </summary>
    /// <param name="votes">The votes actually cast — not the number invited.</param>
    /// <param name="expectedVoterCount">How many people are entitled to vote.</param>
    /// <param name="attempt">Which delivery this is, counting from one.</param>
    /// <remarks>
    /// Dispute is checked before agreement, on purpose: once enough doubt has
    /// arrived the outcome no longer depends on who else turns up, and making
    /// somebody wait for the last vote to hear it would be a worse answer, not
    /// a fairer one.
    /// </remarks>
    public static VotingOutcome Evaluate(
        IReadOnlyCollection<VoteValue> votes,
        int expectedVoterCount,
        int attempt)
    {
        ArgumentNullException.ThrowIfNull(votes);

        var (doubts, ratio) = Tally(votes);

        if (IsDisputed(doubts, ratio))
        {
            return new VotingOutcome(Disputed(attempt), ratio);
        }

        /*
         * Nobody entitled to vote means nobody to convince.
         *
         * A goal shared with no one is the case q2 has always had, and it stays
         * a self-report: waiting twelve hours to confirm a photograph that no
         * friend will ever look at would be ceremony rather than accountability.
         * It is also why the count is a parameter — the rule is "is there
         * anybody to ask", never "is this a group".
         */
        if (expectedVoterCount <= 0)
        {
            return new VotingOutcome(VotingResult.Confirmed, ratio);
        }

        // Everybody has spoken and the doubt never reached the threshold.
        return votes.Count >= expectedVoterCount
            ? new VotingOutcome(VotingResult.Confirmed, ratio)
            : new VotingOutcome(VotingResult.Running, ratio);
    }

    /// <summary>
    /// Evaluates a vote whose deadline has passed.
    /// </summary>
    /// <remarks>
    /// Only the votes cast count. Somebody who did not vote is not a doubter —
    /// silence is not mistrust — so a proof nobody objected to is confirmed,
    /// including one nobody looked at. The alternative would let an
    /// inattentive friend break a streak by doing nothing at all, which is the
    /// opposite of what the vote is for.
    /// </remarks>
    public static VotingOutcome ResolveAfterDeadline(IReadOnlyCollection<VoteValue> votes, int attempt)
    {
        ArgumentNullException.ThrowIfNull(votes);

        var (doubts, ratio) = Tally(votes);

        return IsDisputed(doubts, ratio)
            ? new VotingOutcome(Disputed(attempt), ratio)
            : new VotingOutcome(VotingResult.Confirmed, ratio);
    }

    /// <summary>True when this attempt may still be followed by another.</summary>
    public static bool HasAttemptLeft(int attempt) => attempt < MaxAttempts;

    private static (int Doubts, double Ratio) Tally(IReadOnlyCollection<VoteValue> votes)
    {
        var doubts = votes.Count(vote => vote == VoteValue.Doubt);

        return (doubts, votes.Count == 0 ? 0 : (double)doubts / votes.Count);
    }

    private static bool IsDisputed(int doubts, double ratio) =>
        doubts >= MinDoubtVotes && ratio > DoubtThreshold;

    private static VotingResult Disputed(int attempt) =>
        HasAttemptLeft(attempt) ? VotingResult.Retry : VotingResult.Rejected;
}
