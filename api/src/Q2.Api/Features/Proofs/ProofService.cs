using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Proofs;

/// <summary>
/// Delivering a photograph, voting on somebody else's, and the feed of the
/// ones still waiting for you.
/// </summary>
/// <remarks>
/// The service that makes q2 a different product. Everything here enforces one
/// of three rules that are not display decisions and must never move to a
/// client (section 7e of the migration plan):
///
/// 1. **Only the owner delivers.** The person who made the promise does not get
///    to mark their own homework, and cannot vote on their own photograph.
/// 2. **Doubt is anonymous.** The names of doubters are not withheld from the
///    response — they are never selected at all.
/// 3. **The thresholds and the deadline are evaluated here.** A client may run
///    <see cref="ProofVoting"/> to show a result immediately; nothing it
///    computes decides anything.
/// </remarks>
public sealed class ProofService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    ImageService images,
    ActivityRecorder activity,
    TimeProvider timeProvider,
    TimeZoneResolver timeZones,
    IIdGenerator idGenerator,
    Q2Metrics metrics,
    ILogger<ProofService> logger)
{
    /// <summary>How many cards the swipe feed hands over at once.</summary>
    public const int FeedLimit = 30;

    /// <summary>
    /// Delivers a photograph into a goal's open window.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">
    /// No such goal, it is not this person's, or the image is not theirs.
    /// </exception>
    /// <exception cref="DomainValidationException">The window is not taking a photograph.</exception>
    public async Task<ProofResponse> SubmitAsync(
        Guid goalId,
        SubmitProofRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await LoadGoalAsync(goalId, cancellationToken);

        if (goal is null || goal.OwnerPersonId != me.Id)
        {
            // Not "you may not": a goal somebody is not on should not be
            // confirmed to exist by the way this fails.
            throw new ResourceNotFoundException("Goal", goalId);
        }

        if (request.ImageId is not { } imageId)
        {
            throw new DomainValidationException(nameof(request.ImageId), "A proof needs a photograph.");
        }

        // Theirs, and uploaded as a proof. Without this check, "prove it with
        // this id" would be a way to read any image in the database.
        await images.RequireOwnedAsync(imageId, me.Id, ImagePurpose.Proof, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        // Brought up to date first, so a photograph cannot land in a window
        // whose deadline passed while the screen was open.
        GoalMaintenance.Advance(goal, calendar, now, idGenerator);

        var proof = goal.SubmitProof(idGenerator.NewId(), imageId, request.CapturedInApp, now)
            ?? throw new DomainValidationException(
                "Proof",
                "This window is not taking a photograph right now.");

        database.ProofPhotos.Add(proof);

        // Evaluated at once, because a goal nobody shares has nobody to ask and
        // should not wait twelve hours to hear it.
        Settle(goal, proof, ProofVoting.Evaluate(proof.CastValues, goal.VoterCount, proof.Attempt), me, calendar, now);

        GoalMaintenance.Advance(goal, calendar, now, idGenerator);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("A proof was delivered on goal {GoalId}", goal.Id);
        metrics.CountGoalProgress();

        return await DescribeAsync(goal, proof, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Records one person's verdict, and closes the vote when that settles it.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">
    /// No such photograph, or it is not this person's to look at.
    /// </exception>
    /// <exception cref="DomainValidationException">They have already voted, or it is closed.</exception>
    public async Task<ProofResponse> VoteAsync(
        Guid proofId,
        CastVoteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Value is not { } value)
        {
            throw new DomainValidationException(nameof(request.Value), "A vote needs a value.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);
        var (goal, proof) = await LoadProofAsync(proofId, cancellationToken);

        // Everything that is not theirs to see answers the same way: 404. A
        // 403 here would confirm that somebody's photograph exists.
        if (!goal.CanVote(me.Id))
        {
            throw new ResourceNotFoundException("Proof", proofId);
        }

        var now = timeProvider.GetUtcNow();

        if (!proof.CastVote(idGenerator.NewId(), me.Id, value, now))
        {
            throw new DomainValidationException("Vote", "You have already had your say on this one.");
        }

        /*
         * With their days, because a confirmed vote checks one in.
         *
         * Person.CheckIn on somebody whose check-ins were never loaded cannot
         * see that today is already there, so it inserts a duplicate and hits
         * the unique index — a 500 on the friend's vote, for a day that had
         * already been counted. CurrentPerson loads them for exactly this
         * reason; this is the same person read from the other end.
         */
        var owner = await database.People
            .Include(person => person.CheckIns)
            .SingleAsync(person => person.Id == goal.OwnerPersonId, cancellationToken);
        var calendar = timeZones.For(owner.TimeZoneId);

        Settle(goal, proof, ProofVoting.Evaluate(proof.CastValues, goal.VoterCount, proof.Attempt), owner, calendar, now);

        GoalMaintenance.Advance(goal, calendar, now, idGenerator);
        await database.SaveChangesAsync(cancellationToken);

        // No title, no names, no verdict: how many, and nothing about whose.
        logger.LogInformation("A vote was cast on proof {ProofId}", proof.Id);

        return await DescribeAsync(goal, proof, me.Id, now, cancellationToken);
    }

    /// <summary>Adds or takes back a reaction on a photograph.</summary>
    public async Task<ProofResponse> ReactAsync(
        Guid proofId,
        ReactToProofRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind is not { } kind)
        {
            throw new DomainValidationException(nameof(request.Kind), "A reaction needs a kind.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);
        var (goal, proof) = await LoadProofAsync(proofId, cancellationToken);

        if (!goal.IsVisibleTo(me.Id))
        {
            throw new ResourceNotFoundException("Proof", proofId);
        }

        /*
         * No reacting to a finished failure.
         *
         * All three kinds are approving, so a reaction can never be an insult
         * on its own — but a rejected photograph is somebody's worst moment in
         * this app, and offering a surface for "fire" on it is the nearest
         * thing to kicking somebody who is down. It is a property of the state,
         * not a setting.
         */
        if (proof.Status == ProofStatus.Rejected)
        {
            throw new DomainValidationException("Reaction", "This one is closed.");
        }

        proof.ToggleReaction(idGenerator.NewId(), me.Id, kind);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(goal, proof, me.Id, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// The photographs waiting for this person's vote, newest first.
    /// </summary>
    /// <remarks>
    /// Scoped in the query rather than filtered afterwards: a card only reaches
    /// this list if the viewer is a participant on the goal, is not its owner,
    /// and has not voted yet. Written as one `Where` so there is no filter for
    /// a later change to forget.
    /// </remarks>
    public async Task<IReadOnlyList<FeedProofResponse>> FeedAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var waiting = await database.ProofPhotos
            .AsNoTracking()
            .Include(proof => proof.Votes)
            .Include(proof => proof.Reactions)
            .Where(proof =>
                proof.Status == ProofStatus.Voting
                && proof.VotingDeadline > now
                && proof.UploaderPersonId != me.Id
                && !proof.Votes.Any(vote => vote.VoterPersonId == me.Id)
                && database.Goals.Any(goal =>
                    goal.Instances.Any(instance => instance.Id == proof.GoalInstanceId)
                    && goal.Participants.Any(participant => participant.PersonId == me.Id)))
            .OrderByDescending(proof => proof.CreatedAt)
            .ThenBy(proof => proof.Id)
            .Take(FeedLimit)
            .ToListAsync(cancellationToken);

        if (waiting.Count == 0)
        {
            return [];
        }

        var instanceIds = waiting.Select(proof => proof.GoalInstanceId).ToList();

        var goals = await database.Goals
            .AsNoTracking()
            // The participants, without which `CanVote` sees an empty goal and
            // tells everybody that somebody else's photograph is their own.
            .Include(goal => goal.Participants)
            .Include(goal => goal.Instances)
            .Where(goal => goal.Instances.Any(instance => instanceIds.Contains(instance.Id)))
            .ToListAsync(cancellationToken);

        var people = await LoadPeopleAsync(waiting, goals, cancellationToken);

        return
        [
            .. waiting
                .Select(proof => (Proof: proof, Goal: GoalOf(goals, proof)))
                .Where(pair => pair.Goal is not null)
                .Select(pair => new FeedProofResponse(
                    Describe(pair.Goal!, pair.Proof, me.Id, people, now),
                    pair.Goal!.Title,
                    pair.Goal!.Icon)),
        ];
    }

    /// <summary>One photograph, for somebody allowed to see it.</summary>
    public async Task<ProofResponse> GetAsync(Guid proofId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var (goal, proof) = await LoadProofAsync(proofId, cancellationToken);

        if (!goal.IsVisibleTo(me.Id))
        {
            throw new ResourceNotFoundException("Proof", proofId);
        }

        return await DescribeAsync(goal, proof, me.Id, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Applies an outcome and publishes what follows from it.
    /// </summary>
    /// <remarks>
    /// The single place a verdict becomes a consequence, so "a confirmed proof
    /// counts a day towards the streak, a rejected one does not" is stated once
    /// rather than in each caller.
    /// </remarks>
    private void Settle(
        Goal goal,
        ProofPhoto proof,
        VotingOutcome outcome,
        Person owner,
        LocalCalendar calendar,
        DateTimeOffset now)
    {
        if (outcome.Result == VotingResult.Running || !goal.ApplyProofOutcome(proof, outcome.Result, now))
        {
            return;
        }

        if (outcome.Result != VotingResult.Confirmed)
        {
            // Nothing is published for a doubted photograph. A miss is somebody
            // else's to notice from the balance, not an announcement in the
            // feed — the warning in stage 5 is the considered version of that.
            return;
        }

        // A day only counts once, and only for a proof somebody believed.
        owner.CheckIn(idGenerator.NewId(), calendar.DayOf(now));

        var instance = goal.Instances.FirstOrDefault(candidate => candidate.Id == proof.GoalInstanceId);

        activity.Publish(
            owner.Id,
            ActivityKind.TaskCompleted,
            goal.Title,
            instance?.ConfirmedProofs,
            now,
            goal.Id);
    }

    /// <remarks>
    /// The pauses come too, and they are not optional: this goal is handed to
    /// <see cref="GoalMaintenance.Advance"/>, which asks whether one is running
    /// before it opens or misses anything. An unloaded collection reads as "no
    /// pause" and would quietly fail a window somebody was excused from.
    /// </remarks>
    private async Task<Goal?> LoadGoalAsync(Guid goalId, CancellationToken cancellationToken) =>
        await database.Goals
            .Include(goal => goal.Participants)
            .Include(goal => goal.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Votes)
            .Include(goal => goal.Pauses)
            .SingleOrDefaultAsync(goal => goal.Id == goalId, cancellationToken);

    private async Task<(Goal Goal, ProofPhoto Proof)> LoadProofAsync(
        Guid proofId,
        CancellationToken cancellationToken)
    {
        var instanceId = await database.ProofPhotos
            .AsNoTracking()
            .Where(proof => proof.Id == proofId)
            .Select(proof => (Guid?)proof.GoalInstanceId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException("Proof", proofId);

        var goal = await database.Goals
            .Include(candidate => candidate.Participants)
            .Include(candidate => candidate.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Votes)
            .Include(candidate => candidate.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Reactions)

            // As in LoadGoalAsync: the outcome of this vote goes through
            // GoalMaintenance, which has to be able to see a running pause.
            .Include(candidate => candidate.Pauses)
            .SingleOrDefaultAsync(
                candidate => candidate.Instances.Any(instance => instance.Id == instanceId),
                cancellationToken)
            ?? throw new ResourceNotFoundException("Proof", proofId);

        var proof = goal.Instances
            .SelectMany(instance => instance.Proofs)
            .SingleOrDefault(candidate => candidate.Id == proofId)
            ?? throw new ResourceNotFoundException("Proof", proofId);

        return (goal, proof);
    }

    private static Goal? GoalOf(IEnumerable<Goal> goals, ProofPhoto proof) =>
        goals.FirstOrDefault(goal => goal.Instances.Any(instance => instance.Id == proof.GoalInstanceId));

    private async Task<ProofResponse> DescribeAsync(
        Goal goal,
        ProofPhoto proof,
        Guid viewerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var people = await LoadPeopleAsync([proof], [goal], cancellationToken);
        return Describe(goal, proof, viewerId, people, now);
    }

    private async Task<Dictionary<Guid, Person>> LoadPeopleAsync(
        IEnumerable<ProofPhoto> proofs,
        IEnumerable<Goal> goals,
        CancellationToken cancellationToken)
    {
        /*
         * Uploaders, and the people who *confirmed*. Not the doubters.
         *
         * This is where anonymity is actually enforced: their ids are never
         * loaded, so no later change to the mapping can leak a name that was
         * never fetched. Owners come along because the uploader always is one.
         */
        var ids = proofs
            .SelectMany(proof => proof.ConfirmedBy.Append(proof.UploaderPersonId))
            .Concat(goals.Select(goal => goal.OwnerPersonId))
            .Distinct()
            .ToList();

        return await database.People
            .AsNoTracking()
            .Where(person => ids.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);
    }

    private static ProofResponse Describe(
        Goal goal,
        ProofPhoto proof,
        Guid viewerId,
        IReadOnlyDictionary<Guid, Person> people,
        DateTimeOffset now)
    {
        var confirmedBy = proof.ConfirmedBy
            .Where(people.ContainsKey)
            .Select(id => PersonSummary.From(people[id], now))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var votes = new VoteSummaryResponse(
            proof.ConfirmCount,
            proof.DoubtCount,
            confirmedBy,
            proof.VoteOf(viewerId),
            proof.Status == ProofStatus.Voting && goal.CanVote(viewerId) && !proof.HasVoted(viewerId));

        var reactions = proof.Reactions
            .GroupBy(reaction => reaction.Kind)
            .OrderBy(group => group.Key)
            .Select(group => new ReactionSummaryResponse(
                group.Key,
                group.Count(),
                group.Any(reaction => reaction.PersonId == viewerId)))
            .ToList();

        return new ProofResponse(
            proof.Id,
            goal.Id,
            proof.GoalInstanceId,
            PersonSummary.From(people[proof.UploaderPersonId], now),
            proof.ImageId,
            proof.Status,
            proof.Attempt,
            Math.Max(0, ProofVoting.MaxAttempts - proof.Attempt),
            proof.CapturedInApp,
            proof.CreatedAt,
            proof.VotingDeadline,
            votes,
            reactions);
    }
}
