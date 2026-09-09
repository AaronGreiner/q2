using Sentry;

namespace Q2.Api.Features.Moderation;

/// <summary>
/// Where a report actually goes.
/// </summary>
/// <remarks>
/// The seam that makes the report button honest. Writing the row is easy;
/// [privacy.md](../../../../docs/privacy.md) is blunt about the hard part — a
/// report that lands in a table nobody looks at is worse than no report button,
/// because it promises somebody that something will happen.
///
/// An interface with one implementation, the same posture as
/// <see cref="Q2.Api.Features.Images.IImageStore"/>: the simplest thing that
/// carries the product, behind a seam that makes the real answer — a mailbox, a
/// queue, a moderation console — a registration and a class rather than a
/// search through the features.
/// </remarks>
public interface IReportSink
{
    /// <summary>
    /// Tells whoever is responsible that a report exists.
    /// </summary>
    /// <remarks>
    /// Called after the row is saved, never instead of it. A report that
    /// reached a recipient but was not recorded cannot be acted on twice.
    /// </remarks>
    Task DeliverAsync(Report report, CancellationToken cancellationToken);
}

/// <summary>
/// Rings the one bell this deployment already answers.
/// </summary>
/// <remarks>
/// Sentry is running in every environment with somebody watching it
/// ([0005](../../../../docs/adr/0005-observability-and-sentry.md)), which makes
/// it the only channel q2 has today that a person actually reads. Using it for
/// a moderation report is a deliberate stretch of what an error tracker is for,
/// and it is still better than the alternative of a silent table.
///
/// **The alert is the doorbell; the database is the letter.** What goes to
/// Sentry is the report's id, the kind of thing reported, the reason chosen
/// from a closed list, and the target's id. What does not go is the note —
/// free text somebody typed — and the reporter, who is nobody's business
/// (<see cref="Report.ReporterPersonId"/>). Whoever answers the bell reads the
/// row.
/// </remarks>
public sealed class ObservabilityReportSink(IHub hub, ILogger<ObservabilityReportSink> logger) : IReportSink
{
    public Task DeliverAsync(Report report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Warning rather than Error: nothing has gone wrong technically, and an
        // issue that pages somebody at three in the morning for a spam report
        // is an issue they will mute.
        hub.CaptureEvent(new SentryEvent
        {
            Level = SentryLevel.Warning,
            Message = "A report was filed and is waiting to be looked at",
        }, scope =>
        {
            scope.SetTag("q2.report.kind", report.TargetKind.ToString());
            scope.SetTag("q2.report.reason", report.Reason.ToString());

            // Ids only. They are looked up deliberately by somebody who has
            // decided to act, which is exactly the difference between an id in
            // an alert and a name in one.
            scope.SetExtra("reportId", report.Id);
            scope.SetExtra("targetId", report.TargetId);
            scope.SetExtra("hasNote", report.Note is not null);
        });

        logger.LogWarning(
            "A {ReportKind} was reported as {ReportReason}; report {ReportId}",
            report.TargetKind,
            report.Reason,
            report.Id);

        return Task.CompletedTask;
    }
}
