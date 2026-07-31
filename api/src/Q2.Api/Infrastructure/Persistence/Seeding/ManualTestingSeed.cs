using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The demonstration world plus the cases that are easy to forget: a goal
/// already finished, one long overdue, one archived, and a conversation with
/// nothing in it yet.
/// </summary>
/// <remarks>
/// This is the profile to look at a change in. The happy path is shared with
/// Development so the two cannot drift; what is added here is everything that
/// makes a layout fall over.
/// </remarks>
public sealed class ManualTestingSeed : ISeedDataSource
{
    public SeedProfile Profile => SeedProfile.ManualTesting;

    public string Description => "The demonstration world plus completed, overdue, archived and empty states.";

    public SeedData Create(SeedContext context)
    {
        var build = new SeedBuilder(Profile, context);
        KudosWorld.Compose(build);

        build.AddGoal(
            "Zehn Kilometer am Stück",
            "Schon erreicht — der abgeschlossene Zustand.",
            "trophy",
            GoalRhythm.Weekly,
            completedSteps: 12,
            totalSteps: 12,
            createdDaysAgo: 90,
            streakDays: 3,
            targetDate: context.DaysFromToday(-4));

        build.AddGoal(
            "Jeden Sonntag Meal Prep",
            "Zieldatum ist vorbei und das Ziel läuft noch — der überfällige Zustand.",
            "calendar",
            GoalRhythm.Weekly,
            completedSteps: 3,
            totalSteps: 12,
            createdDaysAgo: 120,
            targetDate: context.DaysFromToday(-21));

        var archived = build.AddGoal(
            "Kalt duschen",
            "Beiseitegelegt, aber nicht gelöscht — der archivierte Zustand.",
            "droplet",
            GoalRhythm.Daily,
            completedSteps: 2,
            totalSteps: 30,
            createdDaysAgo: 150);
        archived.Archive();

        // A title long enough to wrap on a 390-pixel screen, which is where a
        // card layout usually gives up.
        build.AddTask(
            "Eine ziemlich lange Aufgabenbezeichnung, die auf einem schmalen Telefon sicher umbricht",
            GoalRhythm.Daily);

        return build.Build();
    }
}
