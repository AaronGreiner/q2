using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>
/// Drawing a spot check's moment, and announcing one when it comes
/// (docs/adr/0038-spot-checks.md).
/// </summary>
public static class SpotChecks
{
    /// <summary>
    /// The moment for a check on the owner's local <paramref name="day"/>, or
    /// null when no allowed minute is left.
    /// </summary>
    /// <remarks>
    /// Every whole minute from <see cref="SpotCheckRules.EarliestHour"/> to
    /// <see cref="SpotCheckRules.LatestHour"/> that is not in the owner's quiet
    /// hours, has not passed yet, and leaves the full three minutes before the
    /// latest hour. One of them is picked by <paramref name="seed"/>, which is
    /// the check's own id: random wherever ids are (a GUID v7 carries 74 random
    /// bits), and reproducible in a test or a seed that hands ids out in
    /// order. No clock or random source is read here.
    ///
    /// Drawn on the server and never sent anywhere before it comes, so the
    /// owner cannot plan for it — which is the whole point.
    /// </remarks>
    public static DateTimeOffset? MomentFor(
        DateOnly day,
        LocalCalendar calendar,
        TimeOnly? quietFrom,
        TimeOnly? quietTo,
        DateTimeOffset now,
        Guid seed)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var latest = (SpotCheckRules.LatestHour * 60) - (int)SpotCheckRules.AnswerTime.TotalMinutes;
        var candidates = new List<DateTimeOffset>();

        for (var minute = SpotCheckRules.EarliestHour * 60; minute <= latest; minute++)
        {
            var time = new TimeOnly(minute / 60, minute % 60);

            if (QuietHours.Covers(quietFrom, quietTo, time) || calendar.At(day, time) is not { } at || at <= now)
            {
                continue;
            }

            candidates.Add(at);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var pick = (int)(BitConverter.ToUInt32(seed.ToByteArray(), 12) % (uint)candidates.Count);
        return candidates[pick];
    }

    /// <summary>
    /// Draws today's check for a spot-check Don't that has none yet. Returns
    /// true when one was drawn.
    /// </summary>
    public static bool Draw(
        Goal goal,
        LocalCalendar calendar,
        NotificationPreferences preferences,
        DateTimeOffset now,
        IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(ids);

        if (!goal.TakesSpotChecks || goal.CurrentInstance is not { } window)
        {
            return false;
        }

        var today = calendar.Today(now);

        if (window.SpotChecks.Any(check => check.Day == today))
        {
            return false;
        }

        var id = ids.NewId();

        return MomentFor(today, calendar, preferences.QuietHoursFrom, preferences.QuietHoursTo, now, id) is { } at
            && goal.ScheduleSpotCheck(id, today, at, now) is not null;
    }

    /// <summary>
    /// Opens the checks whose moment has come and stages the owner's
    /// notification for each. Returns true when any was opened.
    /// </summary>
    /// <remarks>
    /// Called wherever a goal is brought up to date with a notifier at hand —
    /// the background pass, and the owner's own reads — so the check is asked
    /// the moment anything looks, and announced exactly once: a check that is
    /// open is not opened again.
    /// </remarks>
    public static async Task<bool> IssueAsync(
        Notifier notifier,
        Goal goal,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(goal);

        var issued = goal.IssueDueSpotChecks(now);

        foreach (var _ in issued)
        {
            await notifier.StageAsync(
                new NotificationEvent(NotificationKind.SpotCheckDue, null, NotificationTarget.Goal, goal.Id, goal.Title),
                [goal.OwnerPersonId],
                cancellationToken);
        }

        return issued.Count > 0;
    }
}
