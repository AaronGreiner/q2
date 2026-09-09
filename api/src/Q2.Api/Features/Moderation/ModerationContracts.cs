namespace Q2.Api.Features.Moderation;

/// <summary>Request body for reporting something.</summary>
/// <remarks>
/// Nullable throughout so an empty body produces field errors rather than a
/// binding failure, like every other request in this API.
/// </remarks>
public sealed record CreateReportRequest(
    ReportTargetKind? TargetKind = null,
    Guid? TargetId = null,
    ReportReason? Reason = null,
    string? Note = null);

/// <summary>
/// What the person who reported something gets back.
/// </summary>
/// <remarks>
/// Deliberately almost nothing: that it was received, and when. There is no
/// status to poll and no outcome to report, because telling somebody what
/// happened to their report means telling them what happened to another
/// person's account.
/// </remarks>
public sealed record ReportReceiptResponse(Guid Id, DateTimeOffset CreatedAt);
