using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Goals;

/// <summary>Why a window is in trouble.</summary>
/// <remarks>
/// Two reasons and not one, because they are different situations and deserve
/// different sentences: running out of <em>time</em>, and running out of
/// <em>room</em>.
/// </remarks>
public enum RiskReason
{
    /// <summary>
    /// The last day of the window, and something is still outstanding.
    /// </summary>
    LastDay,

    /// <summary>
    /// Not the last day, but there are no spare days left: from here on,
    /// something has to be delivered every single day to make the quota.
    /// </summary>
    /// <remarks>
    /// Only reachable on a quota ("3× diese Woche"). A window that wants one
    /// proof has no such point — for it there is only the last day.
    /// </remarks>
    Tight,
}

/// <summary>What is missing, and how long there is left.</summary>
/// <param name="RemainingDays">Days left including today.</param>
public readonly record struct RiskAssessment(
    RiskReason Reason,
    int MissingProofs,
    int RequiredProofs,
    int RemainingDays);

/// <summary>
/// Whether a window is close enough to failing to tell anybody.
/// </summary>
/// <remarks>
/// The effective half of "do it or shame it" is not behind the deadline but in
/// front of it. Somebody who learns their friends are about to hear that it is
/// getting tight can still act; somebody who learns of it afterwards can only
/// regret it.
///
/// Ported from the source project's <c>domain/risk.ts</c>, and pure for the
/// same reason <see cref="ProofVoting"/> is: the warning has to be decided by a
/// job that runs whether or not anybody has the app open, and it has to be
/// testable without one.
///
/// Three rules keep the warning from becoming noise — and a warning that comes
/// too often is muted, after which it never works again:
///
/// 1. **Late.** Evening only. Somebody who has delivered nothing by lunchtime
///    is not failing, they are having an ordinary day.
/// 2. **Once.** At most one per window, which is why the window itself records
///    that it happened (<see cref="GoalInstance.RiskNotifiedAt"/>) rather than
///    the job keeping a list.
/// 3. **Only when something is actually missing.** A photograph already being
///    voted on counts as delivered — warning about the person who has just
///    handed in is the fastest way to teach everybody to ignore this.
/// </remarks>
public static class GoalRisk
{
    /// <summary>
    /// The local hour from which a window may be reported as at risk.
    /// </summary>
    /// <remarks>
    /// Eight in the evening is where "later today" turns into "probably not
    /// today": late enough that most of the day's deliveries are already in,
    /// early enough that there is still something somebody can do about it.
    /// </remarks>
    public const int AlertHour = 20;

    /// <summary>
    /// Assesses one window, or returns null when there is nothing worth saying.
    /// </summary>
    /// <param name="calendar">
    /// The owner's calendar. The hour and the day count are both local to the
    /// person whose window it is — warning somebody at 20:00 UTC because the
    /// server thinks it is evening would be a warning at nine in the morning
    /// for half of Europe.
    /// </param>
    public static RiskAssessment? Assess(GoalInstance instance, LocalCalendar calendar, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(calendar);

        if (instance.Status != GoalInstanceStatus.Open)
        {
            return null;
        }

        // A photograph under a running vote is delivered. On a window that
        // wants one proof this is the difference between a useful warning and
        // shouting at the person who has just handed in.
        var pending = instance.PendingProof is null ? 0 : 1;
        var missing = instance.RequiredProofs - instance.ConfirmedProofs - pending;

        if (missing <= 0)
        {
            return null;
        }

        // An expired window is no longer a risk. It is a result, and the
        // maintenance job is about to say so.
        if (now >= instance.DueAt)
        {
            return null;
        }

        if (calendar.HourOf(now) < AlertHour)
        {
            return null;
        }

        // Counted over whole local days rather than over hours: a deadline in
        // this product is always a whole day, and a daylight-saving change must
        // not move the answer by one.
        var remainingDays = instance.DueOn.DayNumber - calendar.Today(now).DayNumber + 1;

        if (remainingDays <= 1)
        {
            return new RiskAssessment(RiskReason.LastDay, missing, instance.RequiredProofs, remainingDays);
        }

        return instance.RequiredProofs > 1 && remainingDays <= missing
            ? new RiskAssessment(RiskReason.Tight, missing, instance.RequiredProofs, remainingDays)
            : null;
    }
}
