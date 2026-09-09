using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Application logic for goals: reads, writes, and the mapping between the
/// domain model and the API contract.
/// </summary>
/// <remarks>
/// It talks to <see cref="Q2DbContext"/> directly. EF Core's DbContext already
/// is a unit of work plus repository, so wrapping it in another repository
/// layer would add indirection without adding a single testable behaviour
/// (see docs/adr/0003-backend-architecture.md).
///
/// **Every read brings the windows up to date first.** A deadline that passed
/// overnight has to read as "verpasst" the moment somebody looks, not whenever
/// the background job next comes round, and the next window has to exist or
/// there is nothing to deliver into. <see cref="GoalMaintenance"/> is idempotent
/// and writes nothing when nothing has changed, so this costs a save only on
/// the first read after a deadline.
/// </remarks>
public sealed class GoalService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    FriendsService friends,
    ActivityRecorder activity,
    ImageService images,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    TimeZoneResolver timeZones,
    Q2Metrics metrics,
    ILogger<GoalService> logger)
{
    public async Task<IReadOnlyList<GoalResponse>> ListAsync(GoalStatus? status, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var goals = await MineAsync(me.Id, cancellationToken);
        await AdvanceAsync(goals, me, now, cancellationToken);

        var visible = status is { } wanted
            ? goals.Where(goal => goal.Status == wanted).ToList()
            : goals;

        var people = await LoadParticipantsAsync(visible, cancellationToken);
        var calendars = await LoadOwnerCalendarsAsync(visible, me, cancellationToken);

        return [.. visible.Select(goal => GoalResponse.From(goal, people, me.Id, now, calendars[goal.OwnerPersonId]))];
    }

    /// <summary>
    /// The goals whose current window covers today — what is actually on
    /// somebody's plate.
    /// </summary>
    /// <remarks>
    /// "Covers today" rather than "is due today", because a window can be a
    /// whole week: three runs by Sunday is something you can do on Tuesday, and
    /// a list that only showed it on Sunday would be a list of things it is
    /// already too late to start.
    /// </remarks>
    public async Task<IReadOnlyList<GoalResponse>> ListDueTodayAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = timeZones.For(me.TimeZoneId).Today(now);

        var goals = await MineAsync(me.Id, cancellationToken);
        await AdvanceAsync(goals, me, now, cancellationToken);

        var due = goals
            .Where(goal => goal.CurrentInstance is { } instance && Covers(instance, today))
            .ToList();

        var people = await LoadParticipantsAsync(due, cancellationToken);
        var calendars = await LoadOwnerCalendarsAsync(due, me, cancellationToken);

        return [.. due.Select(goal => GoalResponse.From(goal, people, me.Id, now, calendars[goal.OwnerPersonId]))];
    }

    /// <summary>
    /// The signed-in person's own windows that are close enough to failing to
    /// say so.
    /// </summary>
    /// <remarks>
    /// Their own only. A friend's risk reaches them through the feed, where a
    /// warning has been through the once-per-window rule; putting somebody
    /// else's near-miss on this list would be the same information without any
    /// of the restraint that makes it bearable.
    ///
    /// Empty for most of the day — the rule only turns on in the evening — and
    /// that is why the screen shows nothing at all rather than an empty
    /// heading.
    /// </remarks>
    public async Task<IReadOnlyList<GoalResponse>> ListAtRiskAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        var goals = await MineAsync(me.Id, cancellationToken);
        await AdvanceAsync(goals, me, now, cancellationToken);

        var mine = goals
            .Where(goal => goal.OwnerPersonId == me.Id
                && goal.CurrentInstance is { } open
                && GoalRisk.Assess(open, calendar, now) is not null)
            .ToList();

        var people = await LoadParticipantsAsync(mine, cancellationToken);

        return [.. mine.Select(goal => GoalResponse.From(goal, people, me.Id, now, calendar))];
    }

    /// <summary>How much of today is delivered, as the home screen's ring.</summary>
    public async Task<DaySummaryResponse> SummariseTodayAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = timeZones.For(me.TimeZoneId).Today(now);

        var goals = await MineAsync(me.Id, cancellationToken);
        await AdvanceAsync(goals, me, now, cancellationToken);

        // Every window that covers today, whatever became of it: one delivered
        // this morning still counts towards the day it was delivered in.
        var windows = goals
            .SelectMany(goal => goal.Instances)
            .Where(instance => Covers(instance, today))
            .ToList();

        return DaySummaryResponse.From(
            windows.Count(instance => instance.Status == GoalInstanceStatus.Done),
            windows.Count);
    }

    /// <exception cref="ResourceNotFoundException">
    /// No such goal, or not one this person may see. The same answer for both:
    /// confirming that a goal exists but belongs to somebody else already says
    /// something about somebody else.
    /// </exception>
    public async Task<GoalDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var goal = await LoadAsync(id, cancellationToken);

        if (goal is null || !goal.IsVisibleTo(me.Id))
        {
            throw new ResourceNotFoundException("Goal", id);
        }

        var now = timeProvider.GetUtcNow();
        var owner = await OwnerOf(goal, me, cancellationToken);
        await AdvanceAsync([goal], owner, now, cancellationToken);

        var today = timeZones.For(me.TimeZoneId).Today(now);
        var people = await LoadParticipantsAsync([goal], cancellationToken);

        // Each team member's own streak, which is the number the detail screen
        // shows next to them — not the goal's.
        var memberIds = goal.Participants.Select(p => p.PersonId).ToList();
        var memberDays = await database.DailyCheckIns
            .AsNoTracking()
            .Where(c => memberIds.Contains(c.PersonId))
            .ToListAsync(cancellationToken);

        var team = goal.Participants
            .Where(p => people.ContainsKey(p.PersonId))
            .Select(p => new GoalTeamMemberResponse(
                PersonSummary.From(people[p.PersonId], now),
                Streak.Count(memberDays.Where(c => c.PersonId == p.PersonId).Select(c => c.Date), today)))
            .OrderBy(m => m.Person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Resolved windows only: the open one is already on the goal itself,
        // and a history grid that included it would show today as a failure
        // every morning.
        var history = goal.Instances
            .Where(instance => instance.Status != GoalInstanceStatus.Open)
            .OrderByDescending(instance => instance.DueOn)
            .Select(GoalInstanceResponse.From)
            .ToList();

        return new GoalDetailResponse(
            GoalResponse.From(goal, people, me.Id, now, timeZones.For(owner.TimeZoneId)),
            team,
            history);
    }

    /// <exception cref="DomainValidationException">The request violates a domain rule.</exception>
    public async Task<GoalResponse> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var me = await currentPerson.GetAsync(cancellationToken);
        var schedule = CreateGoalRequestValidator.ToSchedule(request.Schedule);

        var goal = Goal.Create(
            idGenerator.NewId(),
            me.Id,
            request.Title ?? string.Empty,
            request.Description,
            request.Icon,
            schedule,
            request.IsGroup ?? false,
            request.ReminderAt,
            request.TargetDate,
            now);

        // Only friends can be added to a goal. Without that check, a goal is a
        // way to put your name into a stranger's app.
        var friendIds = (await friends.FriendIdsAsync(me.Id, cancellationToken)).ToHashSet();

        foreach (var personId in request.ParticipantIds ?? [])
        {
            if (!friendIds.Contains(personId))
            {
                throw new DomainValidationException(
                    nameof(request.ParticipantIds),
                    "A goal can only be shared with your friends.");
            }

            goal.AddParticipant(idGenerator.NewId(), personId);
        }

        // The first window opens now, through the same code that opens every
        // later one — so "created today" and "rolled over overnight" cannot
        // disagree about where a window starts.
        GoalMaintenance.Advance(goal, timeZones.For(me.TimeZoneId), now, idGenerator);

        database.Goals.Add(goal);
        activity.Publish(me.Id, ActivityKind.GoalCreated, goal.Title, null, now, goal.Id);

        await database.SaveChangesAsync(cancellationToken);

        // The title is user content and may be personal — log the id only.
        logger.LogInformation(
            "Created goal {GoalId} on a {ScheduleKind} schedule with {ParticipantCount} participant(s)",
            goal.Id,
            goal.Schedule.Kind,
            goal.Participants.Count);

        // How often, and of which kind. Never which goal, and never whose.
        metrics.CountGoalCreated(goal.Schedule.Kind, goal.IsGroup);

        return await DescribeAsync(goal, me.Id, timeZones.For(me.TimeZoneId), now, cancellationToken);
    }

    /// <summary>
    /// The goals that have stopped, most recently stopped first.
    /// </summary>
    /// <remarks>
    /// Its own read rather than <c>?status=</c> twice, because the archive is
    /// one list of two statuses and it is sorted by when things ended rather
    /// than by when they began. A goal in here is not advanced: nothing is
    /// going to happen to it again.
    /// </remarks>
    public async Task<IReadOnlyList<GoalResponse>> ListArchiveAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var closed = (await MineAsync(me.Id, cancellationToken))
            .Where(goal => goal.IsClosed)
            .OrderByDescending(goal => goal.ClosedAt ?? goal.CreatedAt)
            .ThenBy(goal => goal.Id)
            .ToList();

        var people = await LoadParticipantsAsync(closed, cancellationToken);
        var calendars = await LoadOwnerCalendarsAsync(closed, me, cancellationToken);

        return [.. closed.Select(goal => GoalResponse.From(goal, people, me.Id, now, calendars[goal.OwnerPersonId]))];
    }

    /// <summary>
    /// Sets a goal aside for a few days, with a reason its friends can read.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">
    /// A pause is already running, the allowance is used up, a photograph is
    /// being voted on, or the goal has stopped.
    /// </exception>
    public async Task<GoalResponse> PauseAsync(
        Guid id,
        RequestPauseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        // Up to date first, so a pause cannot be granted for a window whose
        // deadline passed overnight — that window is a miss, not a pause.
        GoalMaintenance.Advance(goal, calendar, now, idGenerator, me);

        var pause = goal.RequestPause(
            idGenerator.NewId(),
            request.Reason,
            request.Days ?? 0,
            calendar.Today(now),
            calendar.EndOfDay,
            now);

        database.GoalPauses.Add(pause);
        await database.SaveChangesAsync(cancellationToken);

        // The reason is user content and may say why somebody is ill. The id
        // and the length are the whole of what is safe to log (docs/privacy.md).
        logger.LogInformation("Goal {GoalId} was paused for {Days} day(s)", goal.Id, pause.Days);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>Ends the running pause early, on its owner's say-so.</summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">Nothing was paused.</exception>
    public async Task<GoalResponse> EndPauseAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        if (!goal.EndPause(calendar.Today(now), now))
        {
            throw new DomainValidationException("Pause", "This goal is not paused.");
        }

        // The days already covered stay covered, today included, so this opens
        // the next window rather than reviving the one that was set aside.
        GoalMaintenance.Advance(goal, calendar, now, idGenerator, me);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Adds or takes back the reader's objection to a running pause.
    /// </summary>
    /// <remarks>
    /// Only the people invited to the goal, and never its owner — the same
    /// division as the vote on a photograph, and the reason both are refused
    /// with a 404 rather than a 403: a goal somebody is not on should not be
    /// confirmed to exist by the way this fails.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">Not a goal this person may object on.</exception>
    /// <exception cref="DomainValidationException">Nothing is paused.</exception>
    public async Task<GoalResponse> VetoPauseAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var goal = await LoadAsync(id, cancellationToken);

        if (goal is null || !goal.CanVote(me.Id))
        {
            throw new ResourceNotFoundException("Goal", id);
        }

        var now = timeProvider.GetUtcNow();
        var owner = await OwnerOf(goal, me, cancellationToken);
        var calendar = timeZones.For(owner.TimeZoneId);

        if (goal.VetoPause(idGenerator.NewId(), me.Id, now) is null)
        {
            throw new DomainValidationException("Pause", "This goal is not paused.");
        }

        // An overturned pause hands the window back with its original deadline.
        // If that has passed, this is the run that makes it a miss.
        GoalMaintenance.Advance(goal, calendar, now, idGenerator, owner);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Stops a goal for good and moves it to the archive.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">It had already stopped.</exception>
    public async Task<GoalResponse> CloseAsync(
        Guid id,
        CloseGoalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        // Up to date first, so what is closed is the real state: a window whose
        // deadline passed last night is a miss, and stopping now must not
        // quietly turn it into a pause.
        GoalMaintenance.Advance(goal, calendar, now, idGenerator, me);

        if (!goal.Close(request.Completed ?? false, now))
        {
            throw new DomainValidationException("Goal", "This goal has already stopped.");
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Goal {GoalId} was closed as {Status}", goal.Id, goal.Status);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Deletes a stopped goal outright — history, photographs and chat, for
    /// everybody on it.
    /// </summary>
    /// <remarks>
    /// The second and last step, and deliberately not the first: stopping and
    /// deleting are two decisions, and only a goal that has already stopped can
    /// be deleted. It is also the only way photographs ever really go, which is
    /// why the chat goes with them — leaving the conversation standing would
    /// leave every proof message in it pointing at a picture that no longer
    /// exists.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">It is still running.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);

        if (!goal.IsClosed)
        {
            throw new DomainValidationException(
                "Goal",
                "Stop this goal before deleting it.");
        }

        var imageIds = goal.Instances
            .SelectMany(instance => instance.Proofs)
            .Select(proof => proof.ImageId)
            .Distinct()
            .ToList();

        var removed = await images.RemoveOwnedAsync(imageIds, me.Id, cancellationToken);

        // The conversation is pointed at the goal rather than owned by it, so
        // the database would only null the reference. Everything said about a
        // goal goes when the goal does.
        var conversations = await database.Conversations
            .Include(conversation => conversation.Messages)
            .Include(conversation => conversation.Participants)
            .Where(conversation => conversation.GoalId == goal.Id)
            .ToListAsync(cancellationToken);

        database.Conversations.RemoveRange(conversations);

        // The feed carries the title of what was created and how it went. A
        // deleted goal leaves no sentence about itself behind.
        var events = await database.ActivityEvents
            .Where(entry => entry.SourceId == goal.Id)
            .ToListAsync(cancellationToken);

        database.ActivityEvents.RemoveRange(events);
        database.Goals.Remove(goal);

        await database.SaveChangesAsync(cancellationToken);

        // Bytes last, once the rows are safely gone.
        await images.DeleteBytesAsync(removed, cancellationToken);

        logger.LogInformation(
            "Goal {GoalId} was deleted with {ImageCount} photograph(s)",
            goal.Id,
            removed.Count);
    }

    /// <summary>Mine, and the ones I have been let in on. Tracked, because reads advance them.</summary>
    private async Task<List<Goal>> MineAsync(Guid meId, CancellationToken cancellationToken) =>
        await database.Goals
            .Include(g => g.Participants)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.Proofs)
            .Include(g => g.Pauses)
            .ThenInclude(pause => pause.Vetoes)
            .Where(g => g.OwnerPersonId == meId || g.Participants.Any(p => p.PersonId == meId))
            .OrderByDescending(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Brings a set of goals up to date and saves if anything moved.
    /// </summary>
    /// <remarks>
    /// The calendar is the <em>owner's</em>, never the reader's. A goal's
    /// deadline belongs to the person who committed to it; a friend in another
    /// country looking at it must not see a different day.
    /// </remarks>
    private async Task AdvanceAsync(
        IReadOnlyCollection<Goal> goals,
        Person owner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mine = goals.Where(goal => goal.OwnerPersonId == owner.Id).ToList();
        if (mine.Count == 0)
        {
            return;
        }

        var calendar = timeZones.For(owner.TimeZoneId);
        var changed = false;

        foreach (var goal in mine)
        {
            changed |= GoalMaintenance.Advance(goal, calendar, now, idGenerator);
        }

        if (changed)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The goal's owner, which is usually the person asking and occasionally a
    /// friend of theirs.
    /// </summary>
    private async Task<Person> OwnerOf(Goal goal, Person me, CancellationToken cancellationToken) =>
        goal.OwnerPersonId == me.Id
            ? me
            : await database.People.SingleAsync(p => p.Id == goal.OwnerPersonId, cancellationToken);

    private static bool Covers(GoalInstance instance, DateOnly day) =>
        instance.StartsOn <= day && day <= instance.DueOn;

    /// <summary>
    /// A calendar per goal, belonging to whoever owns it.
    /// </summary>
    /// <remarks>
    /// Needed because "is this window at risk" turns on a local hour, and the
    /// hour that counts is the owner's — a friend reading the same list from
    /// another country must see the same answer they do. For a goal somebody
    /// else owns this costs one lookup, which is why the reader's own person is
    /// passed in rather than fetched again.
    /// </remarks>
    private async Task<Dictionary<Guid, LocalCalendar>> LoadOwnerCalendarsAsync(
        IReadOnlyCollection<Goal> goals,
        Person me,
        CancellationToken cancellationToken)
    {
        var others = goals
            .Select(goal => goal.OwnerPersonId)
            .Where(id => id != me.Id)
            .Distinct()
            .ToList();

        var zones = others.Count == 0
            ? []
            : await database.People
                .AsNoTracking()
                .Where(person => others.Contains(person.Id))
                .ToDictionaryAsync(person => person.Id, person => person.TimeZoneId, cancellationToken);

        zones[me.Id] = me.TimeZoneId;

        return zones.ToDictionary(entry => entry.Key, entry => timeZones.For(entry.Value));
    }

    /// <summary>One goal with everything its rules need, tracked for writing.</summary>
    private async Task<Goal?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Goals
            .Include(g => g.Participants)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.Proofs)
            .Include(g => g.Pauses)
            .ThenInclude(pause => pause.Vetoes)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    /// <summary>
    /// One goal this person owns, or a 404.
    /// </summary>
    /// <remarks>
    /// Not "you may not": a goal somebody is not on should not be confirmed to
    /// exist by the way this fails, and a goal they are merely invited to is
    /// not theirs to pause, stop or delete.
    /// </remarks>
    private async Task<Goal> RequireOwnAsync(Guid id, Guid meId, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) is { } goal && goal.OwnerPersonId == meId
            ? goal
            : throw new ResourceNotFoundException("Goal", id);

    /// <summary>The response for one goal, after it has been written to.</summary>
    private async Task<GoalResponse> DescribeAsync(
        Goal goal,
        Guid viewerId,
        LocalCalendar ownerCalendar,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        GoalResponse.From(
            goal,
            await LoadParticipantsAsync([goal], cancellationToken),
            viewerId,
            now,
            ownerCalendar);

    /// <summary>
    /// Loads every person mentioned by these goals in one query, rather than
    /// letting the mapper pull them in one at a time.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Person>> LoadParticipantsAsync(
        IReadOnlyCollection<Goal> goals,
        CancellationToken cancellationToken)
    {
        var ids = goals.SelectMany(g => g.Participants).Select(p => p.PersonId).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Person>();
        }

        return await database.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
