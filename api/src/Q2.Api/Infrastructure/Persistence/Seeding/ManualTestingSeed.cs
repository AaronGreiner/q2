using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The data set a human explores. Rebuilt from scratch on every
/// <c>bun run test:manual:start</c>, so it can safely include awkward cases.
/// </summary>
/// <remarks>
/// Covers, in order: a shared goal with many participants, an overdue goal, a
/// goal at 0%, one at 99%, one at 100%, goals with and without a target date,
/// an archived goal, a maximum-length title, a long description, and text with
/// non-ASCII characters.
///
/// The <em>empty</em> state is deliberately not represented by a row: reach it
/// with <c>bun run db:reset --env ManualTesting</c> (reset without seeding), or
/// by filtering the goal list down to a status that has no matches.
///
/// All names are obviously fictional. No real personal data belongs here.
/// </remarks>
public sealed class ManualTestingSeed : ISeedDataSource
{
    private const SeedProfile Owner = SeedProfile.ManualTesting;

    public SeedProfile Profile => Owner;

    public string Description => "10 goals covering shared, overdue, archived, boundary and long-text cases.";

    public IReadOnlyList<Goal> CreateGoals(SeedContext context) =>
    [
        SeedGoals.Build(
            Owner,
            1,
            "Run a 10k together",
            "A shared goal with several participants — the main case for the goal card.",
            progressPercent: 62,
            targetDate: context.DaysFromToday(45),
            createdAt: context.DaysAgo(30),
            "Robin Sample",
            "Kim Example",
            "Alex Placeholder",
            "Sam Fixture",
            "Jules Testcase"),

        SeedGoals.Build(
            Owner,
            2,
            "Drink 2 litres of water a day",
            "Overdue on purpose: the target date is in the past and the goal is still active.",
            progressPercent: 35,
            targetDate: context.DaysFromToday(-9),
            createdAt: context.DaysAgo(60)),

        SeedGoals.Build(
            Owner,
            3,
            "Learn 20 words of sign language",
            "Nothing done yet — shows the 0% progress bar.",
            progressPercent: 0,
            targetDate: context.DaysFromToday(90),
            createdAt: context.DaysAgo(4)),

        SeedGoals.Build(
            Owner,
            4,
            "Meditate on 30 days",
            "One percent short of done — checks rounding and the 'almost there' styling.",
            progressPercent: 99,
            targetDate: context.DaysFromToday(2),
            createdAt: context.DaysAgo(28),
            "Kim Example"),

        SeedGoals.Build(
            Owner,
            5,
            "Cook at home every weekday",
            "Fully complete: 100% progress and status Completed.",
            progressPercent: 100,
            targetDate: context.DaysFromToday(-1),
            createdAt: context.DaysAgo(21),
            "Robin Sample",
            "Alex Placeholder"),

        SeedGoals.Build(
            Owner,
            6,
            "Keep a gratitude note",
            "No target date at all — the card must not render an empty date row.",
            progressPercent: 20,
            targetDate: null,
            createdAt: context.DaysAgo(7)),

        SeedGoals.BuildArchived(
            Owner,
            7,
            "Train for a half marathon",
            "Archived while unfinished: still visible in history, no longer counted as active.",
            progressPercent: 55,
            targetDate: context.DaysFromToday(-30),
            createdAt: context.DaysAgo(120),
            "Sam Fixture"),

        SeedGoals.Build(
            Owner,
            8,
            // Exactly Goal.MaxTitleLength (120) characters — the layout boundary.
            // ManualTestingSeedTests asserts this, so the comment cannot drift.
            "A deliberately very long goal title that reaches the maximum allowed length of exactly one hundred and twenty characters",
            "Boundary case for the title: any longer and the API rejects it.",
            progressPercent: 10,
            targetDate: context.DaysFromToday(14),
            createdAt: context.DaysAgo(3)),

        SeedGoals.Build(
            Owner,
            9,
            "Write a long-form journal",
            LongDescription,
            progressPercent: 48,
            targetDate: context.DaysFromToday(60),
            createdAt: context.DaysAgo(15),
            "Jules Testcase"),

        SeedGoals.Build(
            Owner,
            10,
            "Üben, täglich 10 Minuten zu lesen 📚",
            "Non-ASCII title and description — umlauts, an emoji and a “typographic quote”.",
            progressPercent: 73,
            targetDate: context.DaysFromToday(21),
            createdAt: context.DaysAgo(9),
            "Mika Beispiel"),
    ];

    private const string LongDescription =
        "A description long enough to exercise wrapping, truncation and the detail view. "
        + "It repeats a harmless sentence so the layout has something to work with, without "
        + "containing anything that looks like real personal information. "
        + "It repeats a harmless sentence so the layout has something to work with, without "
        + "containing anything that looks like real personal information. "
        + "It repeats a harmless sentence so the layout has something to work with.";
}
