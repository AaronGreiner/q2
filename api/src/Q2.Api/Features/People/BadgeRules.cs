using Q2.Api.Features.Goals;
using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.People;

/// <summary>
/// What each badge takes, in windows, photographs and kudos (#22).
/// </summary>
/// <remarks>
/// Pure, like every rule a number on screen rests on: the facts are read by
/// <see cref="BadgeAwarder"/>, and whether they are enough is decided here,
/// where it is tested without a database. Every criterion is something a
/// person can actually do in q2 today — the ones written for goals with steps
/// ("finished a reading goal") became counts of kept windows on a goal of the
/// matching category (<see cref="GoalCategory"/>).
/// </remarks>
public static class BadgeRules
{
    /// <summary><see cref="BadgeKey.StreakHero"/>: a streak this long, at any time.</summary>
    public const int StreakHeroDays = 14;

    /// <summary><see cref="BadgeKey.EarlyBird"/>: on this many days…</summary>
    public const int EarlyBirdDays = 5;

    /// <summary>…a believed photograph delivered before this local time.</summary>
    public static readonly TimeOnly EarlyBirdBefore = new(7, 0);

    /// <summary><see cref="BadgeKey.Bookworm"/>: kept windows on one reading goal.</summary>
    public const int BookwormWindows = 10;

    /// <summary><see cref="BadgeKey.KudosGiver"/>: kudos given to other people.</summary>
    public const int KudosGiverCount = 100;

    /// <summary><see cref="BadgeKey.Marathon"/>: kept windows on one sport goal — the 42 of a marathon's kilometres.</summary>
    public const int MarathonWindows = 42;

    /// <summary>Whether the longest run of days ever kept up reaches the streak hero's.</summary>
    public static bool IsStreakHero(IEnumerable<DateOnly> checkInDays) =>
        Streak.Longest(checkInDays) >= StreakHeroDays;

    /// <summary>
    /// Whether believed photographs were delivered before seven on enough
    /// different days, on the owner's clock.
    /// </summary>
    /// <param name="believedAt">When each believed photograph of their own was delivered.</param>
    public static bool IsEarlyBird(IEnumerable<DateTimeOffset> believedAt, LocalCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return believedAt
            .Where(delivered => calendar.TimeOf(delivered) < EarlyBirdBefore)
            .Select(calendar.DayOf)
            .Distinct()
            .Count() >= EarlyBirdDays;
    }

    /// <summary>Whether one goal of <paramref name="category"/> has kept <paramref name="windows"/> windows.</summary>
    /// <param name="keptPerGoal">Each of the person's own goals, with how many of its windows were kept.</param>
    public static bool HasKeptOnOne(
        IEnumerable<(GoalCategory Category, int Kept)> keptPerGoal,
        GoalCategory category,
        int windows) =>
        keptPerGoal.Any(goal => goal.Category == category && goal.Kept >= windows);

    /// <summary>Whether enough kudos were given.</summary>
    public static bool IsKudosGiver(int given) => given >= KudosGiverCount;

    /// <summary>
    /// Whether there was a whole week, Monday to Sunday, in which every one of
    /// the person's windows due that week was kept.
    /// </summary>
    /// <remarks>
    /// Only a week that is over: until Sunday has passed, another window can
    /// still be due in it. A paused window was not due and is left out; a week
    /// with nothing due is not a week anybody won.
    /// </remarks>
    /// <param name="windows">The day each of their windows was due by, and what became of it.</param>
    /// <param name="today">The person's own day.</param>
    public static bool IsWeeklyWinner(
        IEnumerable<(DateOnly DueOn, GoalInstanceStatus Status)> windows,
        DateOnly today) =>
        windows
            .Where(window => window.Status != GoalInstanceStatus.Paused)
            .GroupBy(window => MondayOf(window.DueOn))
            .Any(week => week.Key.AddDays(6) < today
                && week.All(window => window.Status == GoalInstanceStatus.Done));

    private static DateOnly MondayOf(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
