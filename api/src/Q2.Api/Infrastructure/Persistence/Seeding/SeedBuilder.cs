using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>One message in a seeded conversation.</summary>
/// <param name="MinutesAgo">Relative to the moment the seed ran, so a thread never looks abandoned.</param>
/// <param name="Reaction">Which kind of kudos, or null.</param>
/// <param name="ReactedBy">Who reacted. Defaults to the signed-in person.</param>
internal sealed record SeedMessage(
    Person Sender,
    string Text,
    int MinutesAgo,
    KudosKind? Reaction = null,
    Person? ReactedBy = null);

/// <summary>
/// Builds a seeded world, handing out deterministic ids as it goes.
/// </summary>
/// <remarks>
/// Every seed is a graph of a dozen entity types, and without this each one
/// would be several hundred lines of id bookkeeping in which the interesting
/// part — what the data actually says — is impossible to find. The builder
/// owns the counters; a seed only says what exists.
///
/// Not thread-safe and not meant to be reused: one builder produces one world.
/// </remarks>
internal sealed class SeedBuilder(SeedProfile profile, SeedContext context)
{
    private readonly Dictionary<SeedEntity, int> _counters = [];

    private readonly List<Person> _people = [];
    private readonly List<AppUser> _accounts = [];
    private readonly List<Friendship> _friendships = [];
    private readonly List<Goal> _goals = [];
    private readonly List<ActivityEvent> _activity = [];
    private readonly List<Conversation> _conversations = [];
    private readonly List<UserSettings> _settings = [];
    private readonly List<Challenge> _challenges = [];

    private Person? _me;

    public SeedContext Context { get; } = context;

    /// <summary>
    /// The person this world is written from the point of view of.
    /// </summary>
    /// <remarks>
    /// Not a privileged account — every seeded person has one, and any of them
    /// can sign in. This is simply the one the documentation, the manual flow
    /// and the test suites use, and the one a seed's goals and chats belong to
    /// unless they say otherwise.
    /// </remarks>
    public Person Me => _me
        ?? throw new InvalidOperationException("This seed has not defined its primary person yet.");


    /// <summary>
    /// Adds the person this world is written around. See <see cref="Me"/>.
    /// </summary>
    /// <param name="streakDays">
    /// How many days back the check-ins go, ending today — which is exactly the
    /// streak that will be read back out.
    /// </param>
    public Person AddPrimaryPerson(
        string displayName,
        string handle,
        string initials,
        string avatarColor,
        int kudosReceived,
        int goalsCompleted,
        int streakDays,
        params BadgeKey[] badges)
    {
        if (_me is not null)
        {
            throw new InvalidOperationException("A seed may define only one primary person.");
        }

        _me = AddPerson(
            displayName,
            handle,
            initials,
            avatarColor,
            kudosReceived,
            goalsCompleted,
            streakDays,
            lastSeenMinutesAgo: 0,
            badges: badges);

        return _me;
    }

    public Person AddPerson(
        string displayName,
        string handle,
        string initials,
        string avatarColor,
        int kudosReceived = 0,
        int goalsCompleted = 0,
        int streakDays = 0,
        int? lastSeenMinutesAgo = null,
        params BadgeKey[] badges)
    {
        var person = Person.Create(
            NextId(SeedEntity.Person),
            displayName,
            handle,
            initials,
            avatarColor);

        person.SetTotals(kudosReceived, goalsCompleted);

        if (lastSeenMinutesAgo is { } minutes)
        {
            person.SetLastSeen(Context.MinutesAgo(minutes));
        }

        for (var day = 0; day < streakDays; day++)
        {
            person.CheckIn(NextId(SeedEntity.CheckIn), Context.DaysFromToday(-day));
        }

        foreach (var badge in badges)
        {
            person.AwardBadge(NextId(SeedEntity.Badge), badge, Context.DaysFromToday(-30));
        }

        _people.Add(person);

        // Everybody gets one. A person without an account could not be signed
        // in as, and "log in as the other side and check what they see" is the
        // whole reason the friendship and chat tests can exist.
        _accounts.Add(SeedAccounts.For(NextId(SeedEntity.Account), person));

        return person;
    }

    /// <summary>Two people who are already friends.</summary>
    /// <remarks>
    /// One row for the pair, so it is a friendship from both sides. Seeds also
    /// connect people to <em>each other</em>, not only to <see cref="Me"/> —
    /// suggestions are derived from friends-of-friends, and a world where
    /// nobody else knows anybody has nobody to suggest.
    /// </remarks>
    public Friendship Befriend(Person one, Person other) =>
        Add(Friendship.Create(
            NextId(SeedEntity.Friendship),
            one.Id,
            other.Id,
            FriendshipStatus.Accepted,
            Context.DaysAgo(30),
            Context.DaysAgo(30)));

    /// <summary>A request somebody sent and nobody has answered yet.</summary>
    public Friendship Request(Person from, Person to) =>
        Add(Friendship.Request(NextId(SeedEntity.Friendship), from.Id, to.Id, Context.DaysAgo(2)));

    /// <param name="history">
    /// What became of the windows before the current one, oldest first:
    /// <c>'d'</c> for delivered and <c>'m'</c> for missed. "dddmdd" is six
    /// closed windows with one miss in the middle, so the goal's streak reads
    /// as two.
    /// </param>
    /// <param name="confirmedNow">
    /// How many proofs are already in the window that is currently open. What
    /// makes "noch 2 von 3" visible without anybody tapping.
    /// </param>
    /// <param name="owner">Whose goal it is. <see cref="Me"/> unless a seed says otherwise.</param>
    public Goal AddGoal(
        string title,
        string? description,
        string icon,
        GoalSchedule schedule,
        int createdDaysAgo,
        string history = "",
        int confirmedNow = 0,
        bool isGroup = false,
        TimeOnly? reminderAt = null,
        DateOnly? targetDate = null,
        Person? owner = null,
        params Person[] participants)
    {
        var goal = Goal.Create(
            NextId(SeedEntity.Goal),
            (owner ?? Me).Id,
            title,
            description,
            icon,
            schedule,
            isGroup,
            reminderAt,
            targetDate,
            Context.DaysAgo(createdDaysAgo));

        foreach (var participant in participants)
        {
            goal.AddParticipant(NextId(SeedEntity.GoalParticipant), participant.Id);
        }

        AddWindows(goal, schedule, history, confirmedNow, targetDate);

        _goals.Add(goal);
        return goal;
    }

    /// <summary>
    /// Lays down a goal's past and its open window.
    /// </summary>
    /// <remarks>
    /// Built backwards from the window that covers today, so a seeded goal
    /// always has exactly the history it says it has — however long ago the
    /// database was last rebuilt. Walking forwards from a creation date would
    /// mean the streak on the goal card changed depending on the day somebody
    /// ran the seed.
    ///
    /// Seeds are pure, so this uses the UTC calendar rather than a person's
    /// zone: a seeded window has to be the same row on every machine.
    /// </remarks>
    private void AddWindows(Goal goal, GoalSchedule schedule, string history, int confirmedNow, DateOnly? targetDate)
    {
        var calendar = new LocalCalendar(TimeZoneInfo.Utc);
        var current = schedule.FirstWindow(Context.Today, targetDate);

        // Walk back one window per outcome, then replay them forwards so the
        // ids come out in chronological order.
        var past = new List<GoalWindow>();
        var cursor = current;

        foreach (var _ in history)
        {
            if (schedule.PreviousWindow(cursor.Start) is not { } earlier)
            {
                break;
            }

            past.Add(earlier);
            cursor = earlier;
        }

        past.Reverse();
        var outcomes = history[^past.Count..];

        for (var index = 0; index < past.Count; index++)
        {
            var window = past[index];
            var instance = goal.OpenWindow(
                NextId(SeedEntity.GoalInstance),
                window,
                calendar.StartOfDay(window.Start),
                calendar.EndOfDay(window.End));

            if (instance is null)
            {
                continue;
            }

            if (outcomes[index] == 'd')
            {
                for (var proof = 0; proof < window.RequiredProofs; proof++)
                {
                    instance.RecordProof(calendar.EndOfDay(window.End));
                }
            }
            else
            {
                instance.Miss(calendar.EndOfDay(window.End).AddTicks(1));
            }
        }

        var open = goal.OpenWindow(
            NextId(SeedEntity.GoalInstance),
            current,
            calendar.StartOfDay(current.Start),
            calendar.EndOfDay(current.End));

        for (var proof = 0; open is not null && proof < confirmedNow; proof++)
        {
            goal.SeedDeliveredProof(Context.SeededAt);
        }
    }

    /// <param name="kudosFromMe">
    /// Whether the signed-in person has already cheered this. It is what makes
    /// the "you already gave kudos" state visible without anybody tapping.
    /// </param>
    public ActivityEvent AddActivity(
        Person actor,
        ActivityKind kind,
        string? subject,
        int? amount,
        int kudosCount,
        int minutesAgo,
        bool kudosFromMe = false)
    {
        var activity = ActivityEvent.Create(
            NextId(SeedEntity.Activity),
            actor.Id,
            kind,
            subject,
            amount,
            kudosCount,
            Context.MinutesAgo(minutesAgo));

        if (kudosFromMe)
        {
            activity.GiveKudos(NextId(SeedEntity.Kudos), Me.Id);
        }

        _activity.Add(activity);
        return activity;
    }

    public Conversation AddDirectChat(Person other, Goal? goal, int unread, params SeedMessage[] messages) =>
        AddConversation(
            Conversation.CreateDirect(NextId(SeedEntity.Conversation), goal?.Id, Context.DaysAgo(14)),
            [Me, other],
            unread,
            messages);

    public Conversation AddGroupChat(
        string title,
        string icon,
        IReadOnlyList<Person> members,
        Goal? goal,
        int unread,
        params SeedMessage[] messages) =>
        AddConversation(
            Conversation.CreateGroup(NextId(SeedEntity.Conversation), title, icon, goal?.Id, Context.DaysAgo(21)),
            [Me, .. members],
            unread,
            messages);

    /// <summary>
    /// A conversation the signed-in person is <em>not</em> in.
    /// </summary>
    /// <remarks>
    /// Exists so that "opening somebody else's chat answers 404" has something
    /// to open. Nothing in the app can reach it.
    /// </remarks>
    public Conversation AddChatWithoutMe(string title, string icon, IReadOnlyList<Person> members) =>
        AddConversation(
            Conversation.CreateGroup(NextId(SeedEntity.Conversation), title, icon, null, Context.DaysAgo(21)),
            members,
            unread: 0,
            []);

    public UserSettings AddDefaultSettings()
    {
        var settings = UserSettings.CreateDefault(NextId(SeedEntity.Settings), Me.Id);
        _settings.Add(settings);
        return settings;
    }

    /// <summary>
    /// Adds a daily challenge, covering one whole day.
    /// </summary>
    /// <param name="daysAgo">0 for the one running now, 1 for yesterday's.</param>
    /// <remarks>
    /// **No contributions.** A contribution is a photograph, a seed writes no
    /// bytes (<see cref="ISeedDataSource"/>: pure, no clock, no network), and a
    /// row pointing at an image that does not exist is a broken picture on
    /// every screen that shows it. What a seeded world can honestly contain is
    /// the prompt — which is enough to see the room, the empty state and the
    /// "join in" button, and it is what the queue would have produced anyway.
    ///
    /// The day is UTC, like everything else a seed computes. A running
    /// deployment counts challenge days in its configured zone
    /// (<see cref="ChallengeQueueWorker"/>), so near a zone boundary a seeded
    /// challenge and a queued one can overlap by a few hours; the room takes
    /// the newest and carries on.
    /// </remarks>
    public Challenge AddChallenge(string prompt, int daysAgo = 0)
    {
        var day = Context.Today.AddDays(-daysAgo);

        var challenge = Challenge.Create(
            NextId(SeedEntity.Challenge),
            day,
            prompt,
            Context.DaysAgo(daysAgo),

            // Exclusive, like the queue's: the end of one day is the start of
            // the next, and no instant falls between two challenges.
            Context.DaysAgo(daysAgo - 1));

        _challenges.Add(challenge);
        return challenge;
    }

    public SeedData Build() =>
        new(_people, _accounts, _friendships, _goals, _activity, _conversations, _settings, _challenges);

    private Friendship Add(Friendship friendship)
    {
        _friendships.Add(friendship);
        return friendship;
    }

    private Conversation AddConversation(
        Conversation conversation,
        IReadOnlyList<Person> participants,
        int unread,
        IReadOnlyList<SeedMessage> messages)
    {
        foreach (var participant in participants)
        {
            conversation.AddParticipant(NextId(SeedEntity.ConversationParticipant), participant.Id);
        }

        var written = new List<ChatMessage>(messages.Count);

        foreach (var message in messages.OrderByDescending(m => m.MinutesAgo))
        {
            var created = conversation.AddMessage(
                NextId(SeedEntity.Message),
                message.Sender.Id,
                message.Text,
                Context.MinutesAgo(message.MinutesAgo));

            if (message.Reaction is { } kind)
            {
                created.ToggleReaction(NextId(SeedEntity.Reaction), (message.ReactedBy ?? Me).Id, kind);
            }

            written.Add(created);
        }

        // The read marker is placed so that exactly `unread` messages from
        // other people fall after it. Setting the count directly would have
        // meant storing a number the conversation could then contradict.
        var fromOthers = written.Where(m => m.SenderPersonId != Me.Id).OrderBy(m => m.SentAt).ToList();

        if (unread <= 0)
        {
            conversation.MarkRead(Me.Id, Context.SeededAt);
        }
        else if (fromOthers.Count > unread)
        {
            conversation.MarkRead(Me.Id, fromOthers[^(unread + 1)].SentAt);
        }

        // Fewer messages than `unread` means "never opened", which is what a
        // null read marker already says.
        _conversations.Add(conversation);
        return conversation;
    }

    private Guid NextId(SeedEntity entity)
    {
        var next = _counters.GetValueOrDefault(entity) + 1;
        _counters[entity] = next;
        return SeedIds.For(profile, entity, next);
    }
}
