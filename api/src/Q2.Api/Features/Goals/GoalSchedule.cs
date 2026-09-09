using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Goals;

/// <summary>A day of the week, numbered the way Europe numbers them.</summary>
/// <remarks>
/// Not <see cref="DayOfWeek"/>, which starts on Sunday at zero. Every week in
/// this product starts on Monday — the week strip on the start screen, the
/// "3× pro Woche" window, the history grid — and a numbering where Monday is 1
/// is one less place for an off-by-one to hide.
/// </remarks>
public enum Weekday
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7,
}

/// <summary>The period a quota is counted over.</summary>
public enum QuotaPeriod
{
    Week,
    Month,
}

/// <summary>The four shapes a commitment can have.</summary>
public enum ScheduleKind
{
    /// <summary>Once, by a chosen day.</summary>
    Once,

    /// <summary>Every <c>EveryDays</c> days. 1 is "every day".</summary>
    Interval,

    /// <summary>On chosen days of the week.</summary>
    Weekdays,

    /// <summary>A number of times per week or per month, whenever you like.</summary>
    Times,
}

/// <summary>
/// One window of time a goal has to be delivered in: whole local days, and how
/// many proofs it takes.
/// </summary>
/// <remarks>
/// Local days rather than instants, because a deadline in this product is
/// always a whole day or a whole period and never a clock time — whoever
/// delivers by midnight was on time. The instants are worked out at the edge,
/// where the person's time zone is known (<see cref="Q2.Api.Infrastructure.Time.LocalCalendar"/>).
/// </remarks>
public readonly record struct GoalWindow(DateOnly Start, DateOnly End, int RequiredProofs);

/// <summary>
/// How often a goal is due.
/// </summary>
/// <remarks>
/// This replaces the old <c>GoalRhythm</c> enum, and the difference is the
/// point: a rhythm was four fixed words, and this is four shapes that carry
/// their own data. "Dreimal die Woche" cannot be expressed as an enum member,
/// and it is the commitment people actually make.
///
/// Two rules hold the whole type together:
///
/// 1. **A deadline is a whole day or a whole period, never a clock time.** The
///    clock exists only as a reminder.
/// 2. **Every commitment has exactly one representation.** "Every 7 days" and
///    "once a week" would otherwise be two ways to say one thing, and two ways
///    to say one thing is two code paths that will disagree.
///
/// An owned entity rather than four nullable columns on <see cref="Goal"/>,
/// because the combinations that make sense are not "any of them": an interval
/// has no weekdays and a quota has no interval, and this is the only place that
/// has to know it.
///
/// Ported from the private project's <c>domain/schedule.ts</c>, which is where
/// these rules were worked out; the difference is that they now run on the
/// server, where a person cannot reach them with the developer tools.
/// </remarks>
public sealed class GoalSchedule
{
    /// <summary>Two years is far beyond any interval anybody means.</summary>
    public const int MaxEveryDays = 730;

    /// <summary>More than three a day is not a commitment, it is a typo.</summary>
    public const int MaxTimesPerPeriod = 100;

    // EF Core materialisation only.
    private GoalSchedule()
    {
        WeekdayList = string.Empty;
    }

    private GoalSchedule(ScheduleKind kind, int? everyDays, string weekdayList, int? times, QuotaPeriod? period)
    {
        Kind = kind;
        EveryDays = everyDays;
        WeekdayList = weekdayList;
        Times = times;
        Period = period;
    }

    public ScheduleKind Kind { get; private set; }

    /// <summary>Set only for <see cref="ScheduleKind.Interval"/>. At least 1.</summary>
    public int? EveryDays { get; private set; }

    /// <summary>
    /// The chosen weekdays as ISO numbers, ascending, comma-separated — "1,3,5".
    /// </summary>
    /// <remarks>
    /// A string rather than a child table or a bitmask, for the same reason
    /// every other enum here is stored as text: somebody reading the database
    /// with a SQL client can see what it says. A separate table for at most
    /// seven small numbers that are never queried individually would be a join
    /// bought with nothing.
    /// </remarks>
    public string WeekdayList { get; private set; }

    /// <summary>Set only for <see cref="ScheduleKind.Times"/>.</summary>
    public int? Times { get; private set; }

    /// <summary>Set only for <see cref="ScheduleKind.Times"/>.</summary>
    public QuotaPeriod? Period { get; private set; }

    /// <summary>The weekdays, parsed. Empty for every other kind.</summary>
    public IReadOnlyList<Weekday> Weekdays => Parse(WeekdayList);

    /// <summary>How many proofs one window of this schedule takes.</summary>
    public int RequiredProofs => Kind == ScheduleKind.Times ? Times ?? 1 : 1;

    /// <summary>Whether a finished window is followed by another one.</summary>
    public bool Repeats => Kind != ScheduleKind.Once;

    public static GoalSchedule Once() => new(ScheduleKind.Once, null, string.Empty, null, null);

    /// <exception cref="DomainValidationException">The interval is out of range.</exception>
    public static GoalSchedule EveryNDays(int everyDays)
    {
        if (everyDays is < 1 or > MaxEveryDays)
        {
            throw new DomainValidationException(
                nameof(EveryDays),
                $"An interval must be between 1 and {MaxEveryDays} days.");
        }

        return new GoalSchedule(ScheduleKind.Interval, everyDays, string.Empty, null, null);
    }

    /// <exception cref="DomainValidationException">No day, or a day that is not one.</exception>
    public static GoalSchedule OnWeekdays(IEnumerable<Weekday>? weekdays)
    {
        var days = (weekdays ?? []).Distinct().OrderBy(day => (int)day).ToList();

        if (days.Count == 0)
        {
            throw new DomainValidationException(nameof(Weekdays), "Choose at least one weekday.");
        }

        if (days.Any(day => !Enum.IsDefined(day)))
        {
            throw new DomainValidationException(nameof(Weekdays), "That is not a weekday.");
        }

        return new GoalSchedule(
            ScheduleKind.Weekdays,
            null,
            string.Join(',', days.Select(day => (int)day)),
            null,
            null);
    }

    /// <exception cref="DomainValidationException">The count is out of range.</exception>
    public static GoalSchedule TimesPer(int times, QuotaPeriod period)
    {
        if (times is < 1 or > MaxTimesPerPeriod)
        {
            throw new DomainValidationException(
                nameof(Times),
                $"A quota must be between 1 and {MaxTimesPerPeriod} times.");
        }

        if (!Enum.IsDefined(period))
        {
            throw new DomainValidationException(nameof(Period), "That is not a period.");
        }

        return new GoalSchedule(ScheduleKind.Times, null, string.Empty, times, period);
    }

    /// <summary>
    /// The first window, when the goal is created.
    /// </summary>
    /// <param name="today">The creator's local day.</param>
    /// <param name="dueOn">
    /// Only meaningful for <see cref="ScheduleKind.Once"/>: the day it is due
    /// by. Ignored otherwise, because every other kind derives its own.
    /// </param>
    public GoalWindow FirstWindow(DateOnly today, DateOnly? dueOn) => Kind switch
    {
        ScheduleKind.Once => Day(dueOn ?? today),

        // Starts today. Somebody who commits to something today means today.
        ScheduleKind.Interval => Day(today),

        ScheduleKind.Weekdays => Day(NextMatchingDay(today) ?? today),
        ScheduleKind.Times => PeriodAround(today),
        _ => Day(today),
    };

    /// <summary>
    /// The window after one that has closed, or null when the goal does not
    /// repeat.
    /// </summary>
    /// <param name="previousEnd">The last day of the window that just closed.</param>
    public GoalWindow? NextWindow(DateOnly previousEnd) => Kind switch
    {
        ScheduleKind.Once => null,
        ScheduleKind.Interval => Day(previousEnd.AddDays(Math.Max(1, EveryDays ?? 1))),
        ScheduleKind.Weekdays => NextMatchingDay(previousEnd.AddDays(1)) is { } day ? Day(day) : null,
        ScheduleKind.Times => PeriodAround(previousEnd.AddDays(1)),
        _ => null,
    };

    /// <summary>
    /// The window before one, or null when there is nothing before it.
    /// </summary>
    /// <remarks>
    /// The exact inverse of <see cref="NextWindow"/>, and it exists because a
    /// history has to be laid out backwards from now: the seeds build a goal's
    /// past this way, and so does anything that wants to draw the last twelve
    /// windows of a goal that has only had three.
    /// </remarks>
    /// <param name="followingStart">The first day of the window that comes after.</param>
    public GoalWindow? PreviousWindow(DateOnly followingStart) => Kind switch
    {
        ScheduleKind.Once => null,
        ScheduleKind.Interval => Day(followingStart.AddDays(-Math.Max(1, EveryDays ?? 1))),
        ScheduleKind.Weekdays => PreviousMatchingDay(followingStart.AddDays(-1)) is { } day ? Day(day) : null,
        ScheduleKind.Times => PeriodAround(followingStart.AddDays(-1)),
        _ => null,
    };

    private GoalWindow Day(DateOnly day) => new(day, day, RequiredProofs);

    private GoalWindow PeriodAround(DateOnly day) => Period == QuotaPeriod.Month
        ? new GoalWindow(FirstOfMonth(day), LastOfMonth(day), RequiredProofs)
        : new GoalWindow(MondayOf(day), MondayOf(day).AddDays(6), RequiredProofs);

    /// <summary>The next chosen weekday on or after <paramref name="from"/>.</summary>
    private DateOnly? NextMatchingDay(DateOnly from)
    {
        var days = Weekdays;
        if (days.Count == 0)
        {
            return null;
        }

        for (var step = 0; step < 7; step++)
        {
            var candidate = from.AddDays(step);
            if (days.Contains(WeekdayOf(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The last chosen weekday on or before <paramref name="from"/>.</summary>
    private DateOnly? PreviousMatchingDay(DateOnly from)
    {
        var days = Weekdays;
        if (days.Count == 0)
        {
            return null;
        }

        for (var step = 0; step < 7; step++)
        {
            var candidate = from.AddDays(-step);
            if (days.Contains(WeekdayOf(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>ISO weekday of a date: Monday is 1.</summary>
    public static Weekday WeekdayOf(DateOnly date) =>
        (Weekday)(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);

    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-((int)WeekdayOf(date) - 1));

    public static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly LastOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static IReadOnlyList<Weekday> Parse(string? list)
    {
        if (string.IsNullOrWhiteSpace(list))
        {
            return [];
        }

        return
        [
            .. list
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var value) ? value : 0)
                .Where(value => value is >= 1 and <= 7)
                .Select(value => (Weekday)value)
                .Distinct()
                .OrderBy(day => (int)day),
        ];
    }
}
