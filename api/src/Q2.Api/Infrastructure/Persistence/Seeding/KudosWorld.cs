using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The demonstration world: one person, five friends, four goals, a week's
/// worth of chat.
/// </summary>
/// <remarks>
/// Shared by the Development and ManualTesting profiles, because "what a
/// working q2 looks like" should not have two slightly different answers
/// depending on which database you opened. ManualTesting adds the awkward cases
/// on top of it rather than restating the pleasant ones.
///
/// Every name here is invented. No real person, handle, message or photograph
/// is in this file, and none may be added (docs/privacy.md).
/// </remarks>
internal static class KudosWorld
{
    public static void Compose(SeedBuilder build)
    {
        var context = build.Context;

        var me = build.AddPrimaryPerson(
            "Mara Klein",
            "@mara.k",
            "MK",
            AvatarColors.Green,
            kudosReceived: 248,
            goalsCompleted: 37,
            streakDays: 12,
            BadgeKey.StreakHero,
            BadgeKey.EarlyBird,
            BadgeKey.Bookworm,
            BadgeKey.KudosGiver);

        var jonas = build.AddPerson(
            "Jonas Weber", "@jonas.w", "JW", AvatarColors.Indigo,
            kudosReceived: 210, goalsCompleted: 14, streakDays: 12, lastSeenMinutesAgo: 1);

        var lena = build.AddPerson(
            "Lena Schulz", "@lena.s", "LS", AvatarColors.Pink,
            kudosReceived: 320, goalsCompleted: 22, streakDays: 7, lastSeenMinutesAgo: 5);

        var tom = build.AddPerson(
            "Tom Bergmann", "@coach.tom", "TB", AvatarColors.Amber,
            kudosReceived: 150, goalsCompleted: 31, streakDays: 4, lastSeenMinutesAgo: 2);

        var sarah = build.AddPerson(
            "Sarah Reich", "@sarah.r", "SR", AvatarColors.Cyan,
            kudosReceived: 96, goalsCompleted: 9, streakDays: 9, lastSeenMinutesAgo: 240);

        var david = build.AddPerson(
            "David Kurz", "@david.k", "DK", AvatarColors.Violet,
            kudosReceived: 74, goalsCompleted: 5, streakDays: 2, lastSeenMinutesAgo: 120);

        var max = build.AddPerson("Max Brenner", "@max.b", "MB", AvatarColors.Teal, lastSeenMinutesAgo: 600);
        var nora = build.AddPerson("Nora Sommer", "@nora.s", "NS", AvatarColors.Orange, lastSeenMinutesAgo: 900);
        var lukas = build.AddPerson("Lukas Feld", "@lukas.f", "LF", AvatarColors.Blue, lastSeenMinutesAgo: 300);
        var emma = build.AddPerson("Emma Roth", "@emma.r", "ER", AvatarColors.Red, lastSeenMinutesAgo: 45);

        build.Befriend(me, jonas);
        build.Befriend(me, lena);
        build.Befriend(me, tom);
        build.Befriend(me, sarah);
        build.Befriend(me, david);

        // Two people waiting for an answer, and one waiting on me — the friends
        // screen has a section for each, and an empty one shows nothing.
        build.Request(max, me);
        build.Request(nora, me);
        build.Request(me, emma);

        // Friendships that have nothing to do with me. They are what makes
        // Lukas a suggestion: three of my friends know him and I do not, which
        // is the whole of what "Vorschläge" means now that it is derived rather
        // than stored.
        build.Befriend(jonas, lukas);
        build.Befriend(lena, lukas);
        build.Befriend(tom, lukas);
        build.Befriend(sarah, nora);
        build.Befriend(david, max);
        build.Befriend(jonas, lena);

        // Three runs a week — the commitment this product exists for, and the
        // one shape the old rhythm enum could not express at all.
        var halfMarathon = build.AddGoal(
            "Halbmarathon im Mai",
            "Drei Läufe pro Woche, langsam steigern.",
            "medal",
            GoalSchedule.TimesPer(3, QuotaPeriod.Week),
            createdDaysAgo: 40,
            history: "dddddd",
            confirmedNow: 1,
            reminderAt: new TimeOnly(18, 0),
            participants: [jonas, lena]);

        var reading = build.AddGoal(
            "Jeden Tag lesen",
            null,
            "book-open",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 25,
            history: "dddddddddddddddddddd",
            confirmedNow: 1,
            reminderAt: new TimeOnly(21, 0));

        var earlyBirds = build.AddGoal(
            "Frühaufsteher-Challenge",
            "Vor sieben aufstehen — gemeinsam fällt es leichter.",
            "sunrise",
            GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday]),
            createdDaysAgo: 14,
            history: "ddddddd",
            isGroup: true,
            reminderAt: new TimeOnly(6, 0),
            participants: [lena, tom, sarah]);

        // A chain that broke four days ago: what a missed window looks like on
        // a card, and the reason the streak reads 3 rather than 8.
        var water = build.AddGoal(
            "2 L Wasser am Tag",
            null,
            "droplet",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 9,
            history: "dddmddd");

        build.AddGoal(
            "Meditation an Wochentagen",
            null,
            "hand-heart",
            GoalSchedule.OnWeekdays([Weekday.Monday, Weekday.Wednesday, Weekday.Friday]),
            createdDaysAgo: 30,
            history: "dddd",
            reminderAt: new TimeOnly(8, 0));

        build.AddGoal(
            "Wocheneinkauf",
            null,
            "calendar",
            GoalSchedule.Once(),
            createdDaysAgo: 1,
            targetDate: context.DaysFromToday(2));

        build.AddActivity(jonas, ActivityKind.TaskCompleted, "Joggen 5 km", null, kudosCount: 8, minutesAgo: 12);
        build.AddActivity(lena, ActivityKind.StreakReached, null, 7, kudosCount: 13, minutesAgo: 40, kudosFromMe: true);
        build.AddActivity(lena, ActivityKind.GoalProgress, earlyBirds.Title, 1, kudosCount: 5, minutesAgo: 62);
        build.AddActivity(tom, ActivityKind.GoalCreated, "Klettern lernen", null, kudosCount: 3, minutesAgo: 190);
        build.AddActivity(sarah, ActivityKind.TaskCompleted, "Vor 7 aufstehen", null, kudosCount: 6, minutesAgo: 320);
        build.AddActivity(david, ActivityKind.StreakReached, null, 2, kudosCount: 1, minutesAgo: 700);

        // The signed-in person's own history, which is what the profile screen
        // shows and the feed deliberately leaves out.
        build.AddActivity(me, ActivityKind.TaskCompleted, "30 Seiten lesen", null, kudosCount: 4, minutesAgo: 90);
        build.AddActivity(me, ActivityKind.StreakReached, null, 12, kudosCount: 11, minutesAgo: 400);
        build.AddActivity(me, ActivityKind.GoalProgress, reading.Title, 1, kudosCount: 2, minutesAgo: 1500);

        build.AddDirectChat(
            jonas,
            halfMarathon,
            unread: 2,
            new SeedMessage(jonas, "Na, schon wach? 😄", 96),
            new SeedMessage(jonas, "Stark, dass du gestern die 5 km durchgezogen hast! 👏", 95, KudosKind.Strong),
            new SeedMessage(me, "Danke! War hart, aber hat sich gelohnt 💪", 92),
            new SeedMessage(jonas, "Sehen wir uns morgen beim Lauf? 🏃", 88));

        build.AddGroupChat(
            "Frühaufsteher",
            "sunrise",
            [lena, tom, sarah],
            earlyBirds,
            unread: 3,
            new SeedMessage(lena, "Guten Morgen! Wer ist heute um 6 dabei? ☀️", 220),
            new SeedMessage(tom, "Ich! Schon auf den Beinen 🏃‍♂️", 214),
            new SeedMessage(me, "Bin dabei, bis gleich!", 210, KudosKind.Fire, lena),
            new SeedMessage(sarah, "Zehn Minuten später bei mir, aber ich komme 🙂", 180),
            new SeedMessage(lena, "Acht von zehn Tagen — das Team steht 💚", 140));

        build.AddDirectChat(
            lena,
            null,
            unread: 0,
            new SeedMessage(me, "Mega Streak, weiter so! 🔥", 1500),
            new SeedMessage(lena, "Danke für die Motivation 💚", 1440, KudosKind.Applause));

        build.AddGroupChat(
            "Lesekreis",
            "book-open",
            [sarah, david],
            reading,
            unread: 0,
            new SeedMessage(sarah, "Kapitel 7 ist der Hammer 📖", 2200),
            new SeedMessage(david, "Bin fast durch, keine Spoiler!", 2100));

        build.AddDirectChat(
            tom,
            null,
            unread: 0,
            new SeedMessage(tom, "Denk an die Pausen zwischen den Einheiten 👍", 4300));

        // Today's prompt, with nobody in the room yet: a seed writes no
        // photographs, so what a developer sees is the state a real morning
        // starts in.
        build.AddChallenge("Zeig deinen Arbeitsplatz, so wie er gerade aussieht.");

        build.AddDefaultSettings();
    }
}
