using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fixture for the Playwright suite.
/// </summary>
/// <remarks>
/// Titles are unique, prefixed with <c>E2E</c> and never a substring of one
/// another, so a locator can match on text without becoming ambiguous once a
/// test creates its own rows. Ids are stable across runs
/// (<c>e2e00000-0000-4000-8000-000000000001</c> and up).
///
/// Like the automated-test seed it contains no archived goal, which keeps
/// "filter to a status with no results" available as an empty-state check —
/// and, for the same reason as there, every recurring task is
/// <see cref="GoalRhythm.Daily"/> so the day of the week the suite runs on
/// cannot change what is on the list.
/// </remarks>
public sealed class E2ESeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.E2E;

    public SeedProfile Profile => Owner;

    public string Description => "5 people, 4 goals, 3 tasks and 2 conversations with stable ids for Playwright.";

    /// <summary>Shared active goal — the row E2E tests open and assert on.</summary>
    public static Guid SharedGoalId => SeedIds.Goal(Owner, 1);

    /// <summary>The conversation E2E opens, sends into and reacts in.</summary>
    public static Guid SharedConversationId => SeedIds.For(Owner, SeedEntity.Conversation, 1);

    public const string CurrentPersonName = "E2E Mara";

    /// <summary>
    /// The address Playwright signs in with. Its global setup posts this and
    /// <see cref="SeedAccounts.Password"/> once, then every spec reuses the
    /// session it got back.
    /// </summary>
    public const string CurrentPersonEmail = "e2e.mara@" + SeedAccounts.EmailDomain;

    /// <summary>The friend's address, for the specs that check the other side.</summary>
    public const string FriendEmail = "e2e.jonas@" + SeedAccounts.EmailDomain;

    public const string FriendName = "E2E Jonas";

    public const string RequestingPersonName = "E2E Max";

    public const string SuggestedPersonName = "E2E Emma";

    public const string SharedGoalTitle = "E2E shared goal with participants";

    public const string CompletedGoalTitle = "E2E completed goal";

    public const string ZeroProgressGoalTitle = "E2E goal without any progress";

    public const string OverdueGoalTitle = "E2E overdue goal";

    public const string OpenTaskTitle = "E2E open task for today";

    public const string DoneTaskTitle = "E2E task already done";

    public const string GroupChatTitle = "E2E group chat";

    public SeedData Create(SeedContext context)
    {
        var build = new SeedBuilder(Owner, context);

        var me = build.AddPrimaryPerson(
            CurrentPersonName,
            "@e2e.mara",
            "EM",
            AvatarColors.Green,
            kudosReceived: 120,
            goalsCompleted: 7,
            streakDays: 5,
            BadgeKey.StreakHero,
            BadgeKey.Bookworm);

        var jonas = build.AddPerson(
            FriendName, "@e2e.jonas", "EJ", AvatarColors.Indigo,
            kudosReceived: 200, goalsCompleted: 3, streakDays: 4, lastSeenMinutesAgo: 1);

        var lena = build.AddPerson(
            "E2E Lena", "@e2e.lena", "EL", AvatarColors.Pink,
            kudosReceived: 60, goalsCompleted: 1, streakDays: 1, lastSeenMinutesAgo: 400);

        var max = build.AddPerson(RequestingPersonName, "@e2e.max", "EX", AvatarColors.Teal);
        var emma = build.AddPerson(SuggestedPersonName, "@e2e.emma", "EE", AvatarColors.Red);

        build.Befriend(me, jonas);
        build.Befriend(me, lena);
        build.Request(max, me);

        // Not connected to me, but to both of my friends: the one suggestion.
        build.Befriend(jonas, emma);
        build.Befriend(lena, emma);

        var shared = build.AddGoal(
            SharedGoalTitle,
            "Synthetic E2E fixture.",
            "medal",
            GoalRhythm.Daily,
            completedSteps: 5,
            totalSteps: 10,
            createdDaysAgo: 10,
            streakDays: 3,
            reminderAt: new TimeOnly(18, 0),
            targetDate: context.DaysFromToday(30),
            participants: [jonas, lena]);

        build.AddGoal(
            CompletedGoalTitle,
            null,
            "trophy",
            GoalRhythm.Weekly,
            completedSteps: 4,
            totalSteps: 4,
            createdDaysAgo: 20,
            targetDate: context.DaysFromToday(5));

        build.AddGoal(
            ZeroProgressGoalTitle,
            null,
            "target",
            GoalRhythm.Daily,
            completedSteps: 0,
            totalSteps: 12,
            createdDaysAgo: 2);

        build.AddGoal(
            OverdueGoalTitle,
            null,
            "calendar",
            GoalRhythm.Weekly,
            completedSteps: 3,
            totalSteps: 12,
            createdDaysAgo: 40,
            targetDate: context.DaysFromToday(-7));

        build.AddTask(OpenTaskTitle, GoalRhythm.Daily, shared, reminderAt: new TimeOnly(7, 0));
        build.AddTask(DoneTaskTitle, GoalRhythm.Daily, doneToday: true);
        build.AddTask("E2E task due later this week", GoalRhythm.Once, dueOn: context.DaysFromToday(3));

        build.AddActivity(jonas, ActivityKind.TaskCompleted, "E2E morning run", null, 7, minutesAgo: 15);
        build.AddActivity(lena, ActivityKind.StreakReached, null, 4, 2, minutesAgo: 90);
        build.AddActivity(me, ActivityKind.GoalProgress, shared.Title, 50, 1, minutesAgo: 300);

        build.AddDirectChat(
            jonas,
            shared,
            unread: 1,
            new SeedMessage(me, "E2E first message", 40),
            new SeedMessage(jonas, "E2E unread reply", 25));

        build.AddGroupChat(
            GroupChatTitle,
            "🌅",
            [jonas, lena],
            null,
            unread: 0,
            new SeedMessage(lena, "E2E group hello", 600));

        build.AddDefaultSettings();

        return build.Build();
    }
}
