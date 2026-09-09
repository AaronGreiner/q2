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
            GoalSchedule.Once(),
            createdDaysAgo: 90,
            confirmedNow: 1,
            targetDate: context.DaysFromToday(-4));

        build.AddGoal(
            "Jeden Sonntag Meal Prep",
            "Mehrere Fenster hintereinander verpasst — die gerissene Kette.",
            "calendar",
            GoalSchedule.OnWeekdays([Weekday.Sunday]),
            createdDaysAgo: 120,
            history: "ddmmm");

        var archived = build.AddGoal(
            "Kalt duschen",
            "Beiseitegelegt, aber nicht gelöscht — der archivierte Zustand.",
            "droplet",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 150,
            history: "dd");
        archived.Close(completed: false, build.Context.DaysAgo(2));

        var completed = build.AddGoal(
            "Dry January",
            "Durchgezogen und abgeschlossen — der andere Ausgang.",
            "medal",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 90,
            history: "dddd");
        completed.Close(completed: true, build.Context.DaysAgo(1));

        // A title long enough to wrap on a 390-pixel screen, which is where a
        // card layout usually gives up.
        build.AddGoal(
            "Eine ziemlich lange Zielbezeichnung, die auf einem schmalen Telefon sicher umbricht",
            null,
            "target",
            GoalSchedule.EveryNDays(1),
            createdDaysAgo: 3);

        // Every second day, so the schedule label has to say a number.
        build.AddGoal(
            "Krafttraining alle zwei Tage",
            null,
            "flame",
            GoalSchedule.EveryNDays(2),
            createdDaysAgo: 30,
            history: "dddmd");

        // A monthly quota: the other period, and the longest window there is.
        build.AddGoal(
            "Zweimal im Monat wandern",
            null,
            "sunrise",
            GoalSchedule.TimesPer(2, QuotaPeriod.Month),
            createdDaysAgo: 120,
            history: "dd",
            confirmedNow: 1);

        return build.Build();
    }
}
