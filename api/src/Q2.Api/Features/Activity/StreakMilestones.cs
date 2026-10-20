using Q2.Api.Features.Streaks;

namespace Q2.Api.Features.Activity;

/// <summary>
/// The streak lengths friends hear about in the feed.
/// </summary>
/// <remarks>
/// Few, and far apart, on purpose: a line for every day would be noise, and a
/// line that comes rarely is one worth giving kudos to (#90). Each is reached
/// at most once per run, because a run only ever grows
/// (<see cref="StreakGrowth.Reached"/>); a run that breaks and starts again
/// reaches them again, and that is a new achievement.
/// </remarks>
public static class StreakMilestones
{
    public static readonly IReadOnlyList<int> Days = [7, 30, 100, 365];

    /// <summary>
    /// The milestone a check-in carried a run to, or null. When one day
    /// carried it past several — two runs joined by a late day — only the
    /// highest is said: one line for one day.
    /// </summary>
    public static int? ReachedBy(StreakGrowth growth) =>
        Days.Where(growth.Reached).Select(days => (int?)days).LastOrDefault();
}
