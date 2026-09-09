namespace Q2.Api.Infrastructure.Time;

/// <summary>
/// Turns instants into local days and local days back into instants.
/// </summary>
/// <remarks>
/// Everything q2 stores is UTC, and everything q2 <em>promises</em> is local.
/// A window is due "by midnight", a warning goes out "after eight in the
/// evening", a streak counts "days" — and the moment one of those decides
/// whether somebody keeps a streak, the difference between the server's idea of
/// a day and theirs stops being a detail.
///
/// This is the only place the two are converted into each other. Nothing else
/// calls <see cref="TimeZoneInfo"/>, and nothing else takes
/// <c>DateOnly.FromDateTime(now.UtcDateTime)</c> as "today" — that expression is
/// correct only for somebody living in UTC, and it is what this type exists to
/// replace.
///
/// The zone comes from the person (<c>Person.TimeZoneId</c>), which is set to
/// the configured default when an account is created. One default is enough for
/// a German-language launch; what matters is that it is written down somewhere
/// rather than being "whatever the server happens to think".
/// </remarks>
public sealed class LocalCalendar
{
    public LocalCalendar(TimeZoneInfo zone)
    {
        Zone = zone;
    }

    public TimeZoneInfo Zone { get; }

    /// <summary>The day it is, where this person is.</summary>
    public DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    /// <summary>The local day an instant falls on.</summary>
    public DateOnly DayOf(DateTimeOffset instant) => Today(instant);

    /// <summary>
    /// The hour it is, where this person is, on a 24-hour clock.
    /// </summary>
    /// <remarks>
    /// Needed because one rule in this product is stated in hours rather than
    /// in days: the evening warning never goes out before
    /// <see cref="Q2.Api.Features.Goals.GoalRisk.AlertHour"/>. Reading the
    /// server's own hour instead would warn half of Europe at nine in the
    /// morning.
    /// </remarks>
    public int HourOf(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).Hour;

    /// <summary>
    /// The first instant of a local day.
    /// </summary>
    /// <remarks>
    /// Midnight does not exist on the night a zone springs forward, and
    /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>
    /// throws rather than guessing. Stepping forward until the clock exists is
    /// the answer that keeps a window inside its own day; there is no such
    /// problem in Europe, where the change is at 02:00, but there are zones that
    /// skip midnight itself and one of them will eventually have a user.
    /// </remarks>
    public DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        while (Zone.IsInvalidTime(local) && local.Date == day.ToDateTime(TimeOnly.MinValue).Date)
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    /// <summary>
    /// The last instant of a local day — the deadline itself.
    /// </summary>
    /// <remarks>
    /// Expressed as "the moment before the next day starts" rather than as
    /// 23:59:59.999, so a day that is 23 or 25 hours long still ends exactly
    /// where the next one begins and no second falls between two windows.
    /// </remarks>
    public DateTimeOffset EndOfDay(DateOnly day) => StartOfDay(day.AddDays(1)).AddTicks(-1);
}

/// <summary>
/// Resolves the time zone a person's days are counted in.
/// </summary>
/// <remarks>
/// Registered as a singleton and asked once per request, because
/// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> reads the system database
/// every time and a lookup per goal would be a lookup per goal.
///
/// **On Linux and macOS this works with <c>InvariantGlobalization</c> on**,
/// which the backend enables (api/Directory.Build.props): the zone rules come
/// from the operating system's tz database rather than from ICU. On Windows the
/// same setting leaves only UTC available, so a developer there gets UTC and a
/// warning rather than a crash — the deployment target is Linux
/// (docs/deployment.md).
/// </remarks>
public sealed class TimeZoneResolver
{
    /// <summary>What a person gets when nothing else is known about them.</summary>
    public const string DefaultZoneId = "Europe/Berlin";

    private readonly Dictionary<string, LocalCalendar> _cache = [];
    private readonly Lock _gate = new();
    private readonly ILogger<TimeZoneResolver> _logger;

    public TimeZoneResolver(IConfiguration configuration, ILogger<TimeZoneResolver> logger)
    {
        _logger = logger;
        ConfiguredZoneId = configuration["Q2:TimeZone"] is { Length: > 0 } configured
            ? configured
            : DefaultZoneId;
    }

    /// <summary>The zone new accounts are created in.</summary>
    public string ConfiguredZoneId { get; }

    /// <summary>The calendar for a stored zone id, falling back to the configured one.</summary>
    public LocalCalendar For(string? zoneId)
    {
        var id = string.IsNullOrWhiteSpace(zoneId) ? ConfiguredZoneId : zoneId;

        lock (_gate)
        {
            if (_cache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var calendar = new LocalCalendar(Resolve(id));
            _cache[id] = calendar;
            return calendar;
        }
    }

    private TimeZoneInfo Resolve(string id)
    {
        if (string.Equals(id, TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Not an error worth an issue: it is a platform limitation on a
            // developer machine, and the app is entirely usable in UTC.
            _logger.LogWarning(
                "Time zone {TimeZoneId} is not available on this platform; using UTC. Deadlines will be counted in UTC days.",
                id);

            return TimeZoneInfo.Utc;
        }
    }
}
