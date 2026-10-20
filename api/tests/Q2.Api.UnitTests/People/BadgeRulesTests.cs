using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.People;

/// <summary>
/// What each badge takes (#22) — one test, or a pair, per badge.
/// </summary>
public sealed class BadgeRulesTests
{
    private static readonly LocalCalendar Berlin =
        new(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));

    private static readonly DateOnly Monday = new(2026, 6, 15);

    private static IEnumerable<DateOnly> Run(DateOnly last, int days) =>
        Enumerable.Range(0, days).Select(back => last.AddDays(-back));

    [Fact]
    public void AStreakHeroHasKeptAFortnightAtSomePoint()
    {
        Assert.False(BadgeRules.IsStreakHero(Run(Monday, 13)));
        Assert.True(BadgeRules.IsStreakHero(Run(Monday, 14)));

        // A run that has since ended still counts: it happened.
        Assert.True(BadgeRules.IsStreakHero(Run(Monday.AddDays(-40), 14).Append(Monday)));
    }

    [Fact]
    public void AnEarlyBirdDeliveredBeforeSevenOnFiveDifferentDays()
    {
        DateTimeOffset At(int day, int hour, int minute = 0) =>
            Berlin.StartOfDay(Monday.AddDays(day)).AddHours(hour).AddMinutes(minute);

        var fourDays = new[] { At(0, 6), At(1, 6, 59), At(2, 5), At(3, 6), At(3, 6, 30) };
        Assert.False(BadgeRules.IsEarlyBird(fourDays, Berlin));

        // Seven on the dot is not before seven.
        Assert.False(BadgeRules.IsEarlyBird(fourDays.Append(At(4, 7)), Berlin));
        Assert.True(BadgeRules.IsEarlyBird(fourDays.Append(At(4, 6, 45)), Berlin));
    }

    [Fact]
    public void ABookwormAndAMarathonAreCountedOnOneGoalOfTheirCategory()
    {
        (GoalCategory, int)[] goals =
        [
            (GoalCategory.Reading, 9),
            (GoalCategory.Reading, 9),
            (GoalCategory.Sport, 42),
            (GoalCategory.Other, 100),
        ];

        // Two reading goals of nine are not one of ten.
        Assert.False(BadgeRules.HasKeptOnOne(goals, GoalCategory.Reading, BadgeRules.BookwormWindows));
        Assert.True(BadgeRules.HasKeptOnOne(goals, GoalCategory.Sport, BadgeRules.MarathonWindows));
        Assert.True(BadgeRules.HasKeptOnOne([(GoalCategory.Reading, 10)], GoalCategory.Reading, BadgeRules.BookwormWindows));
    }

    [Fact]
    public void AKudosGiverGaveAHundred()
    {
        Assert.False(BadgeRules.IsKudosGiver(99));
        Assert.True(BadgeRules.IsKudosGiver(100));
    }

    [Fact]
    public void AWeeklyWinnerKeptEveryWindowOfAWeekThatIsOver()
    {
        var sunday = Monday.AddDays(6);
        (DateOnly, GoalInstanceStatus)[] kept =
        [
            (Monday, GoalInstanceStatus.Done),
            (Monday.AddDays(2), GoalInstanceStatus.Done),
            (sunday, GoalInstanceStatus.Done),

            // Paused was not due.
            (Monday.AddDays(3), GoalInstanceStatus.Paused),
        ];

        // Not before the week is over: Sunday could still bring another.
        Assert.False(BadgeRules.IsWeeklyWinner(kept, sunday));
        Assert.True(BadgeRules.IsWeeklyWinner(kept, sunday.AddDays(1)));

        // One miss, or a relapse, and that week is lost.
        Assert.False(BadgeRules.IsWeeklyWinner(kept.Append((Monday.AddDays(4), GoalInstanceStatus.Missed)), sunday.AddDays(1)));
        Assert.False(BadgeRules.IsWeeklyWinner(kept.Append((Monday.AddDays(4), GoalInstanceStatus.Relapse)), sunday.AddDays(1)));

        // A week with nothing due is nobody's win.
        Assert.False(BadgeRules.IsWeeklyWinner([(Monday, GoalInstanceStatus.Paused)], sunday.AddDays(1)));
    }
}
