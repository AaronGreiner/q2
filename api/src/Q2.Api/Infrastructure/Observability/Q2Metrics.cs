using Q2.Api.Features.Goals;
using Sentry;

namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// The counters q2 emits, and the only place their names are written down.
/// </summary>
/// <remarks>
/// A metric answers "how often", never "for whom" or "about what". It carries a
/// name, a count, and at most an attribute from a closed vocabulary such as a
/// rhythm — never a title, a person, an id or anything else somebody authored.
/// An id is fine in a log line, where it is looked up deliberately; a metric is
/// aggregated and read by everyone, so it stays anonymous. That is why the
/// methods below take enums and booleans rather than strings: a call site
/// cannot pass free text through them.
///
/// The names are declared here rather than at the call sites because
/// <see cref="SentryEventScrubber.ScrubMetric"/> drops every metric it does not
/// find in <see cref="Names"/>. A counter that skips this class is not sent.
/// </remarks>
public sealed class Q2Metrics(IHub hub)
{
    public const string GoalCreated = "q2.goal.created";
    public const string GoalProgress = "q2.goal.progress";
    public const string TaskCompleted = "q2.task.completed";
    public const string AccountRegistered = "q2.account.registered";
    public const string AccountSignedIn = "q2.account.signed_in";

    /// <summary>Every metric name q2 is allowed to send.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        GoalCreated,
        GoalProgress,
        TaskCompleted,
        AccountRegistered,
        AccountSignedIn,
    };

    /// <summary>A goal was created, counted by the rhythm it was given.</summary>
    public void CountGoalCreated(GoalRhythm rhythm, bool isGroup) =>
        Count(GoalCreated, ("rhythm", rhythm.ToString()), ("shared", isGroup));

    /// <summary>Somebody moved a goal one step along.</summary>
    public void CountGoalProgress() => Count(GoalProgress);

    /// <summary>A task was ticked off, or ticked back open again.</summary>
    public void CountTaskCompleted(bool done) => Count(TaskCompleted, ("done", done));

    public void CountAccountRegistered() => Count(AccountRegistered);

    public void CountAccountSignedIn() => Count(AccountSignedIn);

    private void Count(string name, params (string Key, object Value)[] attributes) =>
        hub.Metrics.EmitCounter(
            name,
            1,
            attributes.Select(a => new KeyValuePair<string, object>(a.Key, a.Value)));
}
