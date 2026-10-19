using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
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
    GoalLauncher launcher,
    ImageService images,
    Notifier notifier,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    TimeZoneResolver timeZones,
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

        var people = await LoadPeopleAsync(visible, cancellationToken);
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

        // A Don't that relapsed this period stays on the list, decided: the
        // period is still today's, and logging is still possible.
        var due = goals
            .Where(goal => (goal.CurrentInstance ?? goal.RelapsedInstanceAt(now)) is { } instance && Covers(instance, today))
            .ToList();

        var people = await LoadPeopleAsync(due, cancellationToken);
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

        var people = await LoadPeopleAsync(mine, cancellationToken);

        return [.. mine.Select(goal => GoalResponse.From(goal, people, me.Id, now, calendar))];
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
        var people = await LoadPeopleAsync([goal], cancellationToken);

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

        // Everybody on the goal is in its conversation, so the link is theirs
        // to follow — including for a goal from before conversations, which
        // has none until the maintenance pass opens it.
        var conversationId = await database.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.GoalId == goal.Id)
            .Select(conversation => (Guid?)conversation.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return new GoalDetailResponse(
            GoalResponse.From(goal, people, me.Id, now, timeZones.For(owner.TimeZoneId)),
            team,
            history,
            conversationId);
    }

    /// <exception cref="DomainValidationException">The request violates a domain rule.</exception>
    public async Task<GoalResponse> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var me = await currentPerson.GetAsync(cancellationToken);
        var kind = request.Kind ?? GoalKind.Do;
        var schedule = CreateGoalRequestValidator.ToSchedule(request.Schedule, kind);
        var calendar = timeZones.For(me.TimeZoneId);

        if (CreateGoalRequestValidator.ValidateTargetDate(request.TargetDate, schedule, calendar.Today(now)) is { } problem)
        {
            throw new DomainValidationException(nameof(request.TargetDate), problem.Message, problem.Code);
        }

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
            now,
            kind,
            request.Limit,
            request.ProofMode);

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

        // A goal nobody else is on is a promise nobody checks — its photographs
        // would confirm themselves. The validator says so for an empty list;
        // this is the same rule for a list that was only its own owner.
        if (goal.Participants.Count == 0)
        {
            throw new DomainValidationException(
                nameof(request.ParticipantIds),
                "Invite at least one friend to check this goal.");
        }

        // Its first window, its conversation, the feed and the invitations —
        // the same for a goal made here as for a waiting Do that starts.
        await launcher.LaunchAsync(goal, calendar, now, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await notifier.FlushAsync(cancellationToken);

        // The title is user content and may be personal — log the id only.
        logger.LogInformation(
            "Created goal {GoalId} on a {ScheduleKind} schedule with {ParticipantCount} participant(s)",
            goal.Id,
            goal.Schedule.Kind,
            goal.Participants.Count);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
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

        var people = await LoadPeopleAsync(closed, cancellationToken);
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
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        var pause = goal.RequestPause(
            idGenerator.NewId(),
            request.Reason,
            request.Days ?? 0,
            calendar.Today(now),
            calendar.EndOfDay,
            now);

        database.GoalPauses.Add(pause);

        /*
         * The people it is announced to are the people who may object, so they
         * are told — how long, and never why. The reason may say somebody is
         * ill; it belongs on the goal's own screen, one tap away, for the
         * people allowed to read it, and not on a lock screen.
         */
        await notifier.StageAsync(
            new NotificationEvent(NotificationKind.GoalPaused, me.Id, NotificationTarget.Goal, goal.Id, goal.Title, pause.Days),
            goal.Participants.Select(participant => participant.PersonId),
            cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await notifier.FlushAsync(cancellationToken);

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
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(goal.Participants.Select(participant => participant.PersonId), LiveArea.Goals, goal.Id);
        await notifier.FlushAsync(cancellationToken);

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

        if (goal.VetoPause(idGenerator.NewId(), me.Id, now) is not { } pause)
        {
            throw new DomainValidationException("Pause", "This goal is not paused.");
        }

        // An overturned pause hands the window back with its original deadline.
        // If that has passed, this is the run that makes it a miss.
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, owner, cancellationToken);

        /*
         * A single objection is silent; the owner hears only the consequence.
         *
         * Objections are anonymous, and a notification per objection would be
         * pressure while nothing is decided — "somebody doubts you" is not
         * something to buzz a phone with. Once enough of them have lifted the
         * pause, the window is running again, and that is worth knowing. No
         * actor, for the same reason a verdict has none.
         */
        if (pause.Status == PauseStatus.Overturned)
        {
            await notifier.StageAsync(
                new NotificationEvent(NotificationKind.PauseLifted, ActorPersonId: null, NotificationTarget.Goal, goal.Id, goal.Title),
                [goal.OwnerPersonId],
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(goal.Participants.Select(participant => participant.PersonId), LiveArea.Goals, goal.Id);
        await notifier.FlushAsync(cancellationToken);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Edits one of your own running goals: what applies now applies, what
    /// waits for the next window is recorded, and the people who check it are
    /// told what they need to know (docs/adr/0044-editing-a-running-goal.md).
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">
    /// It has stopped, a newcomer is not a friend, or something asked for is
    /// not allowed for this goal.
    /// </exception>
    public async Task<GoalResponse> EditAsync(Guid id, EditGoalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        // Up to date first: an edit is judged against the window that is
        // really running, and a change that was due overnight has applied.
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        var plan = await PlanEditAsync(goal, request, me, calendar, now, cancellationToken);
        var change = goal.Edit(idGenerator.NewId(), plan, idGenerator.NewId, calendar.EndOfDay, now);

        if (change is not null)
        {
            database.GoalChanges.Add(change);
        }

        if (plan.CheckersAdded.Count > 0)
        {
            await JoinConversationAsync(goal, plan.CheckersAdded, now, cancellationToken);

            // Somebody put on a running goal is one of the people who decide
            // it from now on, the same news as being put on a new one.
            await notifier.StageAsync(
                new NotificationEvent(NotificationKind.GoalInvitation, me.Id, NotificationTarget.Goal, goal.Id, goal.Title),
                plan.CheckersAdded,
                cancellationToken);
        }

        var checkers = goal.Participants.Select(participant => participant.PersonId).ToList();

        /*
         * An easier change is announced to the people who can object to it —
         * that it exists, and never what it is: the terms belong on the goal
         * and in its conversation, not on a lock screen. A stricter one needs
         * no telling beyond the line in the conversation. Somebody added in the
         * same edit has just been told about the whole goal.
         */
        if (change is { IsPending: true, Objectable: true })
        {
            await notifier.StageAsync(
                new NotificationEvent(NotificationKind.GoalChangeProposed, me.Id, NotificationTarget.Goal, goal.Id, goal.Title),
                checkers.Except(plan.CheckersAdded),
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        // Catching up may have applied an earlier edit that removed somebody.
        await RemoveFormerCheckersAsync([goal], cancellationToken);

        // The goal's screen, and its conversation: its name and its lines.
        notifier.Touch(checkers.Append(me.Id), LiveArea.Goals, goal.Id);
        notifier.Touch(checkers.Append(me.Id), LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

        // What kind of edit, and nothing of what it says (docs/privacy.md).
        logger.LogInformation(
            "Goal {GoalId} was edited: {AppliedNow} applied now, {Waiting} waiting, objectable {Objectable}",
            goal.Id,
            plan.HasImmediate,
            plan.HasNewDeferred,
            plan.HasNewDeferred && plan.Objectable);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// What an edit would do, without doing it: what applies at once, what
    /// waits and from when, and whether the checkers could object.
    /// </summary>
    /// <remarks>
    /// The edit sheet's last step, so the owner reads the consequences before
    /// committing to them — worked out by the same <see cref="Goal.PlanEdit"/>
    /// that saving goes through, rather than by a copy of its rules in the
    /// client.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">The same refusals as <see cref="EditAsync"/>.</exception>
    public async Task<GoalEditPreviewResponse> PreviewEditAsync(
        Guid id,
        EditGoalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        if (await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken))
        {
            await database.SaveChangesAsync(cancellationToken);
            await notifier.FlushAsync(cancellationToken);
        }

        var plan = await PlanEditAsync(goal, request, me, calendar, now, cancellationToken);

        if (!plan.HasImmediate && !plan.HasNewDeferred)
        {
            return new GoalEditPreviewResponse(null, plan.WithdrawsPending);
        }

        var ids = goal.Participants.Select(participant => participant.PersonId).Concat(plan.CheckersAdded).ToList();
        var people = await database.People
            .AsNoTracking()
            .Where(person => ids.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, cancellationToken);

        var expectedOn = plan.HasNewDeferred
            ? GoalMaintenance.WhenChangeApplies(goal, calendar, plan.Objectable, now)
            : null;

        return new GoalEditPreviewResponse(
            GoalChangeResponse.Preview(plan, goal, people, now, expectedOn),
            plan.WithdrawsPending);
    }

    /// <summary>
    /// Adds or takes back the reader's objection to the change waiting on a
    /// goal they check.
    /// </summary>
    /// <remarks>
    /// The same people and the same 404 as an objection to a pause: the
    /// checkers, never the owner, and a goal somebody is not on is not
    /// confirmed to exist by the way this fails.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">Not a goal this person may object on.</exception>
    /// <exception cref="DomainValidationException">Nothing is waiting that can be objected to.</exception>
    public async Task<GoalResponse> VetoChangeAsync(Guid id, CancellationToken cancellationToken)
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

        // Up to date first: a change that applied with this morning's window
        // can no longer be objected to.
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, owner, cancellationToken);

        if (goal.VetoChange(idGenerator.NewId(), me.Id, now) is not { } change)
        {
            throw new DomainValidationException("Change", "Nothing is waiting on this goal that can be objected to.");
        }

        // As for a pause: a single objection is silent, and the owner hears
        // only the consequence, from nobody in particular.
        if (change.Status == GoalChangeStatus.Overturned)
        {
            await notifier.StageAsync(
                new NotificationEvent(NotificationKind.GoalChangeLifted, ActorPersonId: null, NotificationTarget.Goal, goal.Id, goal.Title),
                [goal.OwnerPersonId],
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        await RemoveFormerCheckersAsync([goal], cancellationToken);

        var everybody = goal.Participants.Select(participant => participant.PersonId).Append(goal.OwnerPersonId).ToList();
        notifier.Touch(everybody, LiveArea.Goals, goal.Id);
        notifier.Touch(everybody, LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>Takes back the change waiting on one of your own goals.</summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">Nothing was waiting.</exception>
    public async Task<GoalResponse> WithdrawChangeAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        if (!goal.WithdrawChange(now))
        {
            throw new DomainValidationException("Change", "Nothing is waiting on this goal.");
        }

        await database.SaveChangesAsync(cancellationToken);
        await RemoveFormerCheckersAsync([goal], cancellationToken);

        var everybody = goal.Participants.Select(participant => participant.PersonId).Append(me.Id).ToList();
        notifier.Touch(everybody, LiveArea.Goals, goal.Id);
        notifier.Touch(everybody, LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

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
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        if (!goal.Close(request.Completed ?? false, now))
        {
            throw new DomainValidationException("Goal", "This goal has already stopped.");
        }

        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(goal.Participants.Select(participant => participant.PersonId), LiveArea.Goals, goal.Id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("Goal {GoalId} was closed as {Status}", goal.Id, goal.Status);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Logs one use of your own Don't into the window running now — "Heute 1
    /// Bier" — optionally with a photograph.
    /// </summary>
    /// <remarks>
    /// Nobody is notified and nobody votes: the owner said it themselves. It
    /// shows as a neutral line in the goal's conversation, derived by
    /// <see cref="GoalTimeline"/>, so everybody on the goal is touched for
    /// their open screens to follow. The use that breaks the limit makes the
    /// window a relapse on the spot (docs/adr/0037-donts.md).
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">
    /// Not a Don't, stopped, paused, or between windows; or the photograph is
    /// not theirs.
    /// </exception>
    public async Task<GoalResponse> LogUseAsync(Guid id, LogUseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        if (!goal.IsDont)
        {
            throw new DomainValidationException("Goal", "Only a Don't has uses to log.");
        }

        // Theirs, and uploaded like a proof, which is what lets the goal's
        // audience read it (ImageService.CanRead).
        if (request.ImageId is { } imageId)
        {
            await images.RequireOwnedAsync(imageId, me.Id, ImagePurpose.Proof, cancellationToken);
        }

        // Up to date first, so a use cannot land in a window whose period
        // ended while the screen was open.
        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        var use = goal.LogUse(idGenerator.NewId(), request.ImageId, now)
            ?? throw new DomainValidationException("Use", "There is no running period to log this into.");

        database.Add(use);
        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(
            goal.Participants.Select(participant => participant.PersonId).Append(me.Id),
            LiveArea.Goals,
            goal.Id);
        notifier.Touch(goal.Participants.Select(participant => participant.PersonId).Append(me.Id), LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

        // Never what was logged: a use of a Don't is about as personal as
        // anything in this app.
        logger.LogInformation("A use was logged on goal {GoalId}", goal.Id);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Takes back the use of your own Don't you have just logged (#110).
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">
    /// Not the newest use of a period still running, too long ago, or the use
    /// that made the period a relapse.
    /// </exception>
    public async Task<GoalResponse> UndoUseAsync(Guid id, Guid useId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        var use = goal.UndoUse(useId, now)
            ?? throw new DomainValidationException("Use", "This use can no longer be taken back.");

        database.Remove(use);
        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(
            goal.Participants.Select(participant => participant.PersonId).Append(me.Id),
            LiveArea.Goals,
            goal.Id);
        notifier.Touch(goal.Participants.Select(participant => participant.PersonId).Append(me.Id), LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("A use was taken back on goal {GoalId}", goal.Id);

        return await DescribeAsync(goal, me.Id, calendar, now, cancellationToken);
    }

    /// <summary>
    /// Asks a spot check on one of your own spot-check Don'ts right now.
    /// Diagnostics only: mapped outside Staging and Production, so E2E and
    /// manual testing can see a check without waiting for its random moment.
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">Not a running spot-check Don't, or today's check is already decided.</exception>
    public async Task AskSpotCheckNowAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, me, cancellationToken);

        if (!goal.ScheduleSpotCheckNow(idGenerator.NewId(), calendar.Today(now), now))
        {
            throw new DomainValidationException("SpotCheck", "There is no spot check to ask on this goal today.");
        }

        await SpotChecks.IssueAsync(notifier, goal, now, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await notifier.FlushAsync(cancellationToken);
    }

    /// <summary>Moves or removes the daily reminder of one of your own running goals.</summary>
    /// <exception cref="ResourceNotFoundException">Not this person's goal.</exception>
    /// <exception cref="DomainValidationException">It has stopped.</exception>
    public async Task<GoalResponse> SetReminderAsync(
        Guid id,
        SetReminderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var goal = await RequireOwnAsync(id, me.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendar = timeZones.For(me.TimeZoneId);

        if (!goal.SetReminder(request.ReminderAt))
        {
            throw new DomainValidationException("Goal", "This goal has already stopped.");
        }

        await database.SaveChangesAsync(cancellationToken);

        // Only the owner's own devices: nobody else is shown the reminder.
        notifier.Touch([me.Id], LiveArea.Goals, goal.Id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("The reminder of goal {GoalId} was changed", goal.Id);

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

        // The conversation belongs to the goal, but the database would only
        // null the reference (ConversationConfiguration). Everything said about
        // a goal goes when the goal does.
        var conversations = await database.Conversations
            .Include(conversation => conversation.Messages)
            .Include(conversation => conversation.Participants)
            .Where(conversation => conversation.GoalId == goal.Id)
            .ToListAsync(cancellationToken);

        // Everybody's photographs in it, not only the owner's: nobody can reach
        // them once the thread is gone.
        var chatPhotos = await images.RemoveChatPhotosAsync(
            conversations.SelectMany(conversation => conversation.Messages).Select(message => message.ImageId),
            cancellationToken);

        database.Conversations.RemoveRange(conversations);

        // The feed carries the title of what was created and how it went. A
        // deleted goal leaves no sentence about itself behind.
        var events = await database.ActivityEvents
            .Where(entry => entry.SourceId == goal.Id)
            .ToListAsync(cancellationToken);

        database.ActivityEvents.RemoveRange(events);

        // Nor does it leave a line in anybody's bell: an invitation, a verdict,
        // a warning about it — all of them quote its title.
        var lines = await database.Notifications
            .Where(line => line.Target == NotificationTarget.Goal && line.TargetId == goal.Id)
            .ToListAsync(cancellationToken);

        database.Notifications.RemoveRange(lines);

        // Held before the goal goes, because its participants go with it.
        var participantIds = goal.Participants.Select(participant => participant.PersonId).ToList();
        database.Goals.Remove(goal);

        await database.SaveChangesAsync(cancellationToken);

        // Bytes last, once the rows are safely gone.
        await images.DeleteBytesAsync([.. removed, .. chatPhotos], cancellationToken);

        notifier.Touch(participantIds, LiveArea.Goals, goal.Id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation(
            "Goal {GoalId} was deleted with {ImageCount} photograph(s)",
            goal.Id,
            removed.Count);
    }

    /// <summary>
    /// Turns the request into the goal's own terms and plans it against the
    /// goal, refusing a newcomer who is not a friend.
    /// </summary>
    /// <remarks>
    /// Only somebody being added has to be a friend. A checker already on the
    /// goal stays until the owner removes them, friends or not — the same
    /// rule as at creation, applied to the people it is about.
    /// </remarks>
    private async Task<GoalEditPlan> PlanEditAsync(
        Goal goal,
        EditGoalRequest request,
        Person me,
        LocalCalendar calendar,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.ParticipantIds is { } wanted)
        {
            var newcomers = wanted
                .Where(personId => personId != me.Id && goal.Participants.All(participant => participant.PersonId != personId))
                .ToList();

            if (newcomers.Count > 0)
            {
                var friendIds = (await friends.FriendIdsAsync(me.Id, cancellationToken)).ToHashSet();

                if (newcomers.Any(personId => !friendIds.Contains(personId)))
                {
                    throw new DomainValidationException(
                        nameof(request.ParticipantIds),
                        "A goal can only be shared with your friends.",
                        new(FieldErrorCodes.Invalid));
                }
            }
        }

        var edit = new GoalEdit(
            request.Title,
            request.Description,
            request.Icon,
            EditGoalRequestValidator.ToSchedule(request.Schedule, goal.Kind),
            request.TargetDate,
            request.Limit,
            request.ProofMode,
            request.ParticipantIds);

        return goal.PlanEdit(edit, calendar.Today(now), now);
    }

    /// <summary>
    /// Puts checkers added by an edit into the goal's conversation — or opens
    /// it, for a goal from before every goal had one.
    /// </summary>
    private async Task JoinConversationAsync(
        Goal goal,
        IReadOnlyList<Guid> added,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var conversation = await database.Conversations
            .Include(candidate => candidate.Participants)
            .SingleOrDefaultAsync(candidate => candidate.GoalId == goal.Id, cancellationToken);

        if (conversation is null)
        {
            database.Conversations.Add(GoalConversations.Open(goal, idGenerator.NewId(), idGenerator.NewId, now));
            return;
        }

        // Read up to now: the thread so far is history to scroll back
        // through, not a badge of a hundred unread lines.
        foreach (var personId in added)
        {
            conversation.AddParticipant(idGenerator.NewId(), personId, now);
        }

        notifier.Touch(added, LiveArea.Chats);
    }

    /// <summary>
    /// Takes checkers an applied edit removed out of these goals'
    /// conversations, and saves when anybody was.
    /// </summary>
    private async Task RemoveFormerCheckersAsync(IReadOnlyCollection<Goal> goals, CancellationToken cancellationToken)
    {
        var former = await GoalConversations.RemoveFormerCheckersAsync(
            database,
            [.. goals.Select(goal => goal.Id)],
            cancellationToken);

        if (former.Count == 0)
        {
            return;
        }

        await database.SaveChangesAsync(cancellationToken);
        notifier.Touch(former, LiveArea.Chats);
        notifier.Touch(former, LiveArea.Goals);
    }

    /// <summary>Mine, and the ones I have been let in on. Tracked, because reads advance them.</summary>
    private async Task<List<Goal>> MineAsync(Guid meId, CancellationToken cancellationToken) =>
        await database.Goals
            .Include(g => g.Participants)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Votes)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.UseLog)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.SpotChecks)
            .Include(g => g.Pauses)
            .ThenInclude(pause => pause.Vetoes)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Checkers)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Vetoes)
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
            changed |= await ProofVerdicts.AdvanceAsync(notifier, goal, calendar, now, idGenerator, owner, cancellationToken);
        }

        if (changed)
        {
            await database.SaveChangesAsync(cancellationToken);

            // A change that applied may have removed a checker, who leaves
            // the conversation with it.
            await RemoveFormerCheckersAsync(mine, cancellationToken);
            await notifier.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The goal's owner, which is usually the person asking and occasionally a
    /// friend of theirs.
    /// </summary>
    private async Task<Person> OwnerOf(Goal goal, Person me, CancellationToken cancellationToken) =>
        goal.OwnerPersonId == me.Id
            ? me
            : await database.People.Include(person => person.CheckIns)
                .SingleAsync(p => p.Id == goal.OwnerPersonId, cancellationToken);

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
            .ThenInclude(proof => proof.Votes)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.UseLog)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.SpotChecks)
            .Include(g => g.Pauses)
            .ThenInclude(pause => pause.Vetoes)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Checkers)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Vetoes)
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
            await LoadPeopleAsync([goal], cancellationToken),
            viewerId,
            now,
            ownerCalendar);

    /// <summary>
    /// Loads every person mentioned by these goals — their owners and their
    /// participants — in one query, rather than letting the mapper pull them in
    /// one at a time.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Person>> LoadPeopleAsync(
        IReadOnlyCollection<Goal> goals,
        CancellationToken cancellationToken)
    {
        var ids = goals
            .SelectMany(g => g.Participants.Select(p => p.PersonId).Append(g.OwnerPersonId))
            .Distinct()
            .ToList();

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
