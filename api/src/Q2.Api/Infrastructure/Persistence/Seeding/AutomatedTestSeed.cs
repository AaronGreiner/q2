using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The smallest world that still covers every branch the API tests care about.
/// </summary>
/// <remarks>
/// Deliberately contains <em>no</em> archived goal, so
/// <c>GET /api/goals?status=Archived</c> returns an empty list and the empty
/// state stays testable. Tests that need more may arrange extra rows
/// themselves — this seed is a floor, not a ceiling.
///
/// Every recurring task is <see cref="GoalRhythm.Daily"/> and the one-off is
/// due in three days. That is not an accident: a Weekdays or Weekly task would
/// make "how many tasks are on today's list" depend on the day the suite
/// happens to run, and a test that passes on Tuesday and fails on Saturday is
/// worse than no test.
/// </remarks>
public sealed class AutomatedTestSeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.AutomatedTest;

    public SeedProfile Profile => Owner;

    public string Description => "3 people, 3 goals, 3 tasks, 2 conversations — one of each branch.";

    /// <summary>The person every request is answered as.</summary>
    public static Guid CurrentPersonId => SeedIds.For(Owner, SeedEntity.Person, 1);

    /// <summary>An accepted friend — the actor behind the seeded feed entry.</summary>
    public static Guid FriendPersonId => SeedIds.For(Owner, SeedEntity.Person, 2);

    /// <summary>Stable id of the active seeded goal, used by GET-by-id tests.</summary>
    public static Guid ActiveGoalId => SeedIds.Goal(Owner, 1);

    /// <summary>Stable id of the completed seeded goal.</summary>
    public static Guid CompletedGoalId => SeedIds.Goal(Owner, 2);

    /// <summary>Stable id of the goal that has no target date.</summary>
    public static Guid GoalWithoutTargetDateId => SeedIds.Goal(Owner, 3);

    /// <summary>A daily task that starts the day open.</summary>
    public static Guid OpenTaskId => SeedIds.For(Owner, SeedEntity.GoalTask, 1);

    /// <summary>A daily task that starts the day already ticked off.</summary>
    public static Guid DoneTaskId => SeedIds.For(Owner, SeedEntity.GoalTask, 2);

    /// <summary>A one-off task due in three days, so it is never on today's list.</summary>
    public static Guid FutureTaskId => SeedIds.For(Owner, SeedEntity.GoalTask, 3);

    /// <summary>The pending friend request, for accept and decline tests.</summary>
    public static Guid PendingRequestId => SeedIds.For(Owner, SeedEntity.Friendship, 2);

    /// <summary>The suggestion, for the "send a request" test.</summary>
    public static Guid SuggestionId => SeedIds.For(Owner, SeedEntity.Friendship, 3);

    /// <summary>A feed entry nobody has given kudos to yet.</summary>
    public static Guid ActivityWithoutKudosId => SeedIds.For(Owner, SeedEntity.Activity, 1);

    /// <summary>The direct conversation, which starts with one unread message.</summary>
    public static Guid DirectConversationId => SeedIds.For(Owner, SeedEntity.Conversation, 1);

    /// <summary>A conversation the current person is not part of.</summary>
    public static Guid ForeignConversationId => SeedIds.For(Owner, SeedEntity.Conversation, 2);

    public SeedData Create(SeedContext context)
    {
        var build = new SeedBuilder(Owner, context);

        var me = build.AddCurrentUser(
            "Test Person One",
            "@test.one",
            "T1",
            AvatarColors.Green,
            kudosReceived: 20,
            goalsCompleted: 2,
            streakDays: 3,
            BadgeKey.StreakHero);

        var friend = build.AddPerson(
            "Test Person Two", "@test.two", "T2", AvatarColors.Indigo,
            kudosReceived: 30, goalsCompleted: 1, streakDays: 2, lastSeenMinutesAgo: 1);

        var stranger = build.AddPerson(
            "Test Person Three", "@test.three", "T3", AvatarColors.Pink, lastSeenMinutesAgo: 5000);

        build.Connect(friend, FriendshipStatus.Accepted);
        build.Connect(stranger, FriendshipStatus.Requested, mutualFriends: 2);

        var suggested = build.AddPerson("Test Person Four", "@test.four", "T4", AvatarColors.Amber);
        build.Connect(suggested, FriendshipStatus.Suggested, mutualFriends: 1);

        var active = build.AddGoal(
            "Automated test: shared active goal",
            "Synthetic test data.",
            "medal",
            GoalRhythm.Daily,
            completedSteps: 4,
            totalSteps: 10,
            createdDaysAgo: 10,
            streakDays: 2,
            reminderAt: new TimeOnly(18, 0),
            targetDate: context.DaysFromToday(20),
            participants: [friend]);

        build.AddGoal(
            "Automated test: completed goal",
            null,
            "trophy",
            GoalRhythm.Weekly,
            completedSteps: 5,
            totalSteps: 5,
            createdDaysAgo: 20,
            targetDate: context.DaysFromToday(3));

        build.AddGoal(
            "Automated test: goal without target date",
            null,
            "target",
            GoalRhythm.Daily,
            completedSteps: 0,
            totalSteps: 8,
            createdDaysAgo: 1);

        build.AddTask("Automated test: open task", GoalRhythm.Daily, active, reminderAt: new TimeOnly(7, 0));
        build.AddTask("Automated test: done task", GoalRhythm.Daily, doneToday: true);
        build.AddTask("Automated test: future task", GoalRhythm.Once, dueOn: context.DaysFromToday(3));

        build.AddActivity(friend, ActivityKind.TaskCompleted, "Automated test: a run", null, 5, minutesAgo: 10);
        build.AddActivity(friend, ActivityKind.StreakReached, null, 2, 3, minutesAgo: 60, kudosFromMe: true);
        build.AddActivity(me, ActivityKind.GoalProgress, active.Title, 40, 1, minutesAgo: 120);

        build.AddDirectChat(
            friend,
            active,
            unread: 1,
            new SeedMessage(me, "Automated test: first message", 30),
            new SeedMessage(friend, "Automated test: unread reply", 20, MessageReactions.Clap));

        build.AddChatWithoutMe("Automated test: not my group", "🔒", [stranger, suggested]);

        build.AddDefaultSettings();

        return build.Build();
    }
}
