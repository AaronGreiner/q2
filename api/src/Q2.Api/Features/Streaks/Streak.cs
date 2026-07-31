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
    /// Monday-to-Sunday activity for the week containing
    /// <paramref name="today"/>. Always seven entries, always in that order.
    /// </summary>
    /// <remarks>
    /// Monday first because that is what the week looks like where q2 is used;
    /// <see cref="DayOfWeek"/> starts on Sunday, which is why the offset is not
    /// simply the enum value.
    /// </remarks>
    public static IReadOnlyList<bool> Week(IEnumerable<DateOnly> days, DateOnly today)
    {
        var set = days as IReadOnlySet<DateOnly> ?? days.ToHashSet();

        var offset = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-offset);

        return [.. Enumerable.Range(0, 7).Select(i => set.Contains(monday.AddDays(i)))];
    }
}
