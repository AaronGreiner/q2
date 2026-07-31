using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>One message in a seeded conversation.</summary>
/// <param name="MinutesAgo">Relative to the moment the seed ran, so a thread never looks abandoned.</param>
/// <param name="Reaction">One of <see cref="MessageReactions"/>, or null.</param>
/// <param name="ReactedBy">Who reacted. Defaults to the signed-in person.</param>
internal sealed record SeedMessage(
    Person Sender,
    string Text,
    int MinutesAgo,
    string? Reaction = null,
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
    private readonly List<Friendship> _friendships = [];
    private readonly List<Goal> _goals = [];
    private readonly List<GoalTask> _tasks = [];
    private readonly List<ActivityEvent> _activity = [];
    private readonly List<Conversation> _conversations = [];
    private readonly List<UserSettings> _settings = [];

    private Person? _me;

    public SeedContext Context { get; } = context;

    /// <summary>The signed-in person. Set by <see cref="AddCurrentUser"/>.</summary>
    public Person Me => _me
        ?? throw new InvalidOperationException("This seed has not defined a current user yet.");

    /// <summary>
    /// Adds the one person this deployment treats as signed in.
    /// </summary>
    /// <param name="streakDays">
    /// How many days back the check-ins go, ending today — which is exactly the
    /// streak that will be read back out.
    /// </param>
    public Person AddCurrentUser(
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
            throw new InvalidOperationException("A seed may define only one current user.");
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
            isCurrentUser: true,
            badges);

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
        bool isCurrentUser = false,
        params BadgeKey[] badges)
    {
        var person = Person.Create(
            NextId(SeedEntity.Person),
            displayName,
            handle,
            initials,
            avatarColor,
            isCurrentUser);

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
        return person;
    }

    public Friendship Connect(Person person, FriendshipStatus status, int mutualFriends = 0)
    {
        var friendship = Friendship.Create(NextId(SeedEntity.Friendship), person.Id, status, mutualFriends);
        _friendships.Add(friendship);
        return friendship;
    }

    /// <param name="streakDays">
    /// How many days back the contributions go, ending today — which is the
    /// streak the goal card will show.
    /// </param>
    public Goal AddGoal(
        string title,
        string? description,
        string icon,
        GoalRhythm rhythm,
        int completedSteps,
        int totalSteps,
        int createdDaysAgo,
        int streakDays = 0,
        bool isGroup = false,
        TimeOnly? reminderAt = null,
        DateOnly? targetDate = null,
        params Person[] participants)
    {
        var goal = Goal.Create(
            NextId(SeedEntity.Goal),
            title,
            description,
            icon,
            rhythm,
            isGroup,
            completedSteps,
            totalSteps,
            reminderAt,
            targetDate,
            Context.DaysAgo(createdDaysAgo));

        foreach (var participant in participants)
        {
            goal.AddParticipant(NextId(SeedEntity.GoalParticipant), participant.Id);
        }

        for (var day = 0; day < streakDays; day++)
        {
            goal.RecordContribution(NextId(SeedEntity.GoalContribution), Context.DaysFromToday(-day));
        }

        _goals.Add(goal);
        return goal;
    }

    public GoalTask AddTask(
        string title,
        GoalRhythm rhythm,
        Goal? goal = null,
        TimeOnly? reminderAt = null,
        bool doneToday = false,
        double? measuredValue = null,
        double? targetValue = null,
        string? measureUnit = null,
        DayOfWeek? weeklyOn = null,
        DateOnly? dueOn = null)
    {
        var task = GoalTask.Create(
            NextId(SeedEntity.GoalTask),
            goal?.Id,
            title,
            rhythm,
            reminderAt,
            weeklyOn,
            dueOn,
            measuredValue,
            targetValue,
            measureUnit,
            _tasks.Count,
            Context.DaysAgo(7));

        if (doneToday)
        {
            task.Toggle(Context.Today);
        }

        _tasks.Add(task);
        return task;
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
        string emoji,
        IReadOnlyList<Person> members,
        Goal? goal,
        int unread,
        params SeedMessage[] messages) =>
        AddConversation(
            Conversation.CreateGroup(NextId(SeedEntity.Conversation), title, emoji, goal?.Id, Context.DaysAgo(21)),
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
    public Conversation AddChatWithoutMe(string title, string emoji, IReadOnlyList<Person> members) =>
        AddConversation(
            Conversation.CreateGroup(NextId(SeedEntity.Conversation), title, emoji, null, Context.DaysAgo(21)),
            members,
            unread: 0,
            []);

    public UserSettings AddDefaultSettings()
    {
        var settings = UserSettings.CreateDefault(NextId(SeedEntity.Settings), Me.Id);
        _settings.Add(settings);
        return settings;
    }

    public SeedData Build() =>
        new(_people, _friendships, _goals, _tasks, _activity, _conversations, _settings);

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

            if (message.Reaction is { } emoji)
            {
                created.ToggleReaction(NextId(SeedEntity.Reaction), (message.ReactedBy ?? Me).Id, emoji);
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
