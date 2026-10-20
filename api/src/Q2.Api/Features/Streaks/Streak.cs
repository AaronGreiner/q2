namespace Q2.Api.Features.Streaks;

/// <summary>
/// How many days in a row something has been kept up.
/// </summary>
/// <remarks>
/// A streak is the number q2 puts in front of people most often — on the home
/// screen, on every goal card, on the profile — so there is exactly one
/// definition of it, here, rather than one per aggregate that could drift.
///
/// It is always computed from the days themselves and never stored. A stored
/// counter needs a nightly job to notice a missed day, and a job that runs
/// twice, or not at all, silently invents or destroys a streak somebody cares
/// about.
/// </remarks>
public static class Streak
{
    /// <summary>
    /// Consecutive days ending today, or ending yesterday when today has not
    /// been used yet.
    /// </summary>
    /// <remarks>
    /// Yesterday still counts, deliberately. Otherwise opening the app before
    /// breakfast would announce that a twelve-day streak is over before the day
    /// has had any chance to happen — which is the opposite of what a self-care
    /// app should do to somebody.
    /// </remarks>
    public static int Count(IEnumerable<DateOnly> days, DateOnly today)
    {
        var set = days as IReadOnlySet<DateOnly> ?? days.ToHashSet();

        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        while (set.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    /// <summary>
    /// What adding <paramref name="day"/> to <paramref name="days"/> does to
    /// the run it lands in: the longest run it touched before, and the run it
    /// makes. Null when the day was already there.
    /// </summary>
    /// <remarks>
    /// A day can join two runs into one — a late check-in for yesterday
    /// between last week and today — so "before" is the longer of the two
    /// neighbours, not their sum: a run that already reached a length never
    /// gets to reach it again, because days are only ever added. That is what
    /// makes "reached a 30-day streak" a fact that happens once per run
    /// (<see cref="Q2.Api.Features.Activity.StreakMilestones"/>).
    /// </remarks>
    public static StreakGrowth? GrowthFrom(IEnumerable<DateOnly> days, DateOnly day)
    {
        var set = days as IReadOnlySet<DateOnly> ?? days.ToHashSet();

        if (set.Contains(day))
        {
            return null;
        }

        var before = RunFrom(set, day.AddDays(-1), -1);
        var after = RunFrom(set, day.AddDays(1), 1);

        return new StreakGrowth(Math.Max(before, after), before + 1 + after);
    }

    /// <summary>The longest run of consecutive days in <paramref name="days"/>.</summary>
    public static int Longest(IEnumerable<DateOnly> days)
    {
        var set = days as IReadOnlySet<DateOnly> ?? days.ToHashSet();

        // Only a day that starts a run is walked from, so every day is looked
        // at a bounded number of times.
        return set
            .Where(day => !set.Contains(day.AddDays(-1)))
            .Select(start => RunFrom(set, start, 1))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int RunFrom(IReadOnlySet<DateOnly> set, DateOnly from, int step)
    {
        var length = 0;

        for (var cursor = from; set.Contains(cursor); cursor = cursor.AddDays(step))
        {
            length++;
        }

        return length;
    }
}

/// <summary>
/// How one new day changed the run it landed in.
/// </summary>
/// <param name="Before">The longest run the day touched, before it was there.</param>
/// <param name="After">The run it is part of now.</param>
public readonly record struct StreakGrowth(int Before, int After)
{
    /// <summary>True when this day carried the run to <paramref name="length"/> for the first time.</summary>
    public bool Reached(int length) => Before < length && length <= After;
}
