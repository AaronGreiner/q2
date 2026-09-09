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

    /// <summary>The person the tests sign in as by default.</summary>
    public static Guid CurrentPersonId => SeedIds.For(Owner, SeedEntity.Person, 1);

    /// <summary>Their email address, which is what the sign-in helper posts.</summary>
    public const string CurrentPersonEmail = "test.one@" + SeedAccounts.EmailDomain;

    /// <summary>The friend's address, for tests that check the other side of a thing.</summary>
    public const string FriendEmail = "test.two@" + SeedAccounts.EmailDomain;

    /// <summary>The address of the person who has asked to be friends.</summary>
    public const string RequesterEmail = "test.three@" + SeedAccounts.EmailDomain;

    /// <summary>An accepted friend — the actor behind the seeded feed entry.</summary>
    public static Guid FriendPersonId => SeedIds.For(Owner, SeedEntity.Person, 2);

    /// <summary>Stable id of the active seeded goal, used by GET-by-id tests.</summary>
    public static Guid ActiveGoalId => SeedIds.Goal(Owner, 1);

    /// <summary>Stable id of the completed seeded goal.</summary>
    public static Guid CompletedGoalId => SeedIds.Goal(Owner, 2);

    /// <summary>Stable id of the goal that has no target date.</summary>
    public static Guid GoalWithoutTargetDateId => SeedIds.Goal(Owner, 3);

    /// <summary>A goal that is due three times a week, with one proof already in.</summary>
    public static Guid QuotaGoalId => SeedIds.Goal(Owner, 4);

    /// <summary>Who has asked to be friends — the accept and decline tests.</summary>
    public static Guid RequestingPersonId => SeedIds.For(Owner, SeedEntity.Person, 3);

    /// <summary>Somebody not connected at all — the "send a request" test.</summary>
    public static Guid UnconnectedPersonId => SeedIds.For(Owner, SeedEntity.Person, 4);

    /// <summary>A feed entry nobody has given kudos to yet.</summary>
    public static Guid ActivityWithoutKudosId => SeedIds.For(Owner, SeedEntity.Activity, 1);

    /// <summary>The direct conversation, which starts with one unread message.</summary>
    public static Guid DirectConversationId => SeedIds.For(Owner, SeedEntity.Conversation, 1);

    /// <summary>A conversation the current person is not part of.</summary>
    public static Guid ForeignConversationId => SeedIds.For(Owner, SeedEntity.Conversation, 2);

    /// <summary>
    /// The prompt of the challenge that is running. The queue worker does not
    /// run in this environment, so a seeded row is what makes the room exist.
    /// </summary>
    public const string ChallengePrompt = "Automated test: today's prompt";

    public SeedData Create(SeedContext context)
    {
        var build = new SeedBuilder(Owner, context);

        var me = build.AddPrimaryPerson(
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

        var unconnected = build.AddPerson("Test Person Four", "@test.four", "T4", AvatarColors.Amber);

        build.Befriend(me, friend);
        build.Request(stranger, me);

        // Not connected to me, but known to my friend — which is what makes
        // them the one suggestion this world offers.
        build.Befriend(friend, unconnected);

        // Two delivered days behind it, so its streak reads as two and its
        // current window is open with nothing in it yet.
        var active = build.AddGoal(
            "Automated test: shared active goal",
            "Synthetic test data.",
            "medal",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 10,
            history: "dd",
            reminderAt: new TimeOnly(18, 0),
            participants: [friend]);

        // A one-off that has been delivered: the only way a goal reaches
        // Completed.
        build.AddGoal(
            "Automated test: completed goal",
            null,
            "trophy",
            GoalSchedule.Once(),
            createdDaysAgo: 20,
            confirmedNow: 1,
            targetDate: context.DaysFromToday(3));

        build.AddGoal(
            "Automated test: goal without target date",
            null,
            "target",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 1);

        // "Dreimal die Woche", one proof in — the window that reads
        // "noch 2 von 3".
        build.AddGoal(
            "Automated test: three times a week",
            null,
            "flame",
            GoalSchedule.TimesPer(3, QuotaPeriod.Week),
            createdDaysAgo: 30,
            history: "dm",
            confirmedNow: 1);

        build.AddActivity(friend, ActivityKind.TaskCompleted, "Automated test: a run", null, 5, minutesAgo: 10);
        build.AddActivity(friend, ActivityKind.StreakReached, null, 2, 3, minutesAgo: 60, kudosFromMe: true);
        build.AddActivity(me, ActivityKind.GoalProgress, active.Title, 2, 1, minutesAgo: 120);

        build.AddDirectChat(
            friend,
            active,
            unread: 1,
            new SeedMessage(me, "Automated test: first message", 30),
            new SeedMessage(friend, "Automated test: unread reply", 20, KudosKind.Applause));

        build.AddChatWithoutMe("Automated test: not my group", "target", [stranger, unconnected]);

        // One running challenge and no contributions: a seed writes no image
        // bytes, so the room starts empty and a test that wants somebody in it
        // uploads a picture like a client would.
        build.AddChallenge(ChallengePrompt);

        build.AddDefaultSettings();

        return build.Build();
    }
}
