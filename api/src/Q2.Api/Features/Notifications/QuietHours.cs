namespace Q2.Api.Features.Notifications;

/// <summary>
/// The hours nothing is delivered.
/// </summary>
/// <remarks>
/// A pure function over a local time, for the same reason
/// <see cref="Q2.Api.Features.Goals.GoalRisk"/> and
/// <see cref="Q2.Api.Features.Goals.PauseRules"/> are: it decides something
/// while nobody is looking, so it has to be testable without a host and without
/// a clock.
///
/// **A notification caught by quiet hours is dropped, not held.** Holding it
/// would be a queue, and a queue is the "Neubau" this stage is explicitly not
/// — but the real reason is that the thing itself is already recorded. The
/// warning is in the feed, the challenge is on the start screen, and a
/// notification arriving at seven in the morning about a window that closed at
/// midnight is worse than no notification: it is a reminder of something that
/// can no longer be acted on.
/// </remarks>
public static class QuietHours
{
    /// <summary>When quiet hours start for a new account.</summary>
    /// <remarks>
    /// On by default, and this is not a neutral choice. A product that has to
    /// be told not to buzz at three in the morning has already buzzed at three
    /// in the morning for everybody who never opened the settings.
    ///
    /// Ten in the evening leaves the risk warning its window: it never goes out
    /// before eight
    /// (<see cref="Q2.Api.Features.Goals.GoalRisk.AlertHour"/>), so the two
    /// rules meet rather than cancel.
    /// </remarks>
    public static readonly TimeOnly DefaultFrom = new(22, 0);

    /// <summary>When they end.</summary>
    public static readonly TimeOnly DefaultTo = new(7, 0);

    /// <summary>
    /// Whether <paramref name="localTime"/> falls inside the quiet window.
    /// </summary>
    /// <param name="from">Null when this person has switched quiet hours off.</param>
    /// <remarks>
    /// Crossing midnight is the normal case rather than the edge one — 22:00 to
    /// 07:00 is what almost everybody will have — so it is the branch that gets
    /// the comment: the window is "at or after the start, **or** before the
    /// end", where a same-day window is "at or after the start **and** before
    /// the end".
    ///
    /// A window with the same start and end is treated as switched off. The
    /// alternative reading — twenty-four silent hours — is one a person could
    /// arrive at by accident and would experience as the feature being broken.
    /// </remarks>
    public static bool Covers(TimeOnly? from, TimeOnly? to, TimeOnly localTime)
    {
        if (from is not { } start || to is not { } end || start == end)
        {
            return false;
        }

        return start < end
            ? localTime >= start && localTime < end
            : localTime >= start || localTime < end;
    }
}
