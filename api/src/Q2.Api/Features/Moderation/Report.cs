using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Moderation;

/// <summary>What somebody is reporting.</summary>
/// <remarks>
/// Persisted as text so the database stays readable and reordering the members
/// cannot change what a row means.
/// </remarks>
public enum ReportTargetKind
{
    /// <summary>A person, for what they are doing rather than for one picture.</summary>
    Person,

    /// <summary>One photograph delivered against a goal.</summary>
    Proof,

    /// <summary>One contribution to the daily challenge.</summary>
    ChallengeEntry,
}

/// <summary>Why it is being reported.</summary>
/// <remarks>
/// A closed list rather than free text alone, because the note is optional and
/// somebody in a hurry has to be able to say something useful in one tap. The
/// wording of each is in the message catalogue; these are the categories,
/// carried over from the source project's <c>ReportReason</c>.
/// </remarks>
public enum ReportReason
{
    /// <summary>The picture does not show what it claims to.</summary>
    Faked,

    /// <summary>Content that does not belong in the app at all.</summary>
    Inappropriate,

    /// <summary>Aimed at somebody. The one reason that is about a person rather than a picture.</summary>
    Harassment,

    Spam,

    Other,
}

/// <summary>
/// Somebody has asked for something to be looked at.
/// </summary>
/// <remarks>
/// **The target is an id and a kind, with no foreign key**, and that is the
/// opposite of the decision taken for
/// <see cref="Q2.Api.Features.Challenges.ChallengeReaction"/> — deliberately,
/// because the requirement is the reverse. A reaction must die with the thing
/// it is attached to; a report must <em>survive</em> it. The whole point of
/// reporting a photograph is that the photograph may be removed afterwards, and
/// a cascade would delete the record of why. A report is an operational fact
/// about something that happened, not a relationship whose integrity the
/// database is protecting.
///
/// It also means a report keeps its meaning when the reported account is
/// deleted, which is the case that matters most.
///
/// Nothing in the app ever reads these rows back. That is not an oversight, it
/// is the reason <see cref="IReportSink"/> exists: a report that lands only in
/// a table nobody looks at is worse than no report button at all
/// (docs/privacy.md).
/// </remarks>
public sealed class Report
{
    /// <summary>How much somebody may write. Long enough for a sentence, short enough to read.</summary>
    public const int MaxNoteLength = 500;

    // EF Core materialisation only.
    private Report()
    {
    }

    private Report(
        Guid id,
        Guid reporterPersonId,
        ReportTargetKind targetKind,
        Guid targetId,
        ReportReason reason,
        string? note,
        DateTimeOffset createdAt)
    {
        Id = id;
        ReporterPersonId = reporterPersonId;
        TargetKind = targetKind;
        TargetId = targetId;
        Reason = reason;
        Note = note;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Who reported it.
    /// </summary>
    /// <remarks>
    /// Recorded, and never shown to the person reported. Reporting is not a
    /// public act — if it were attributable it would become another way to have
    /// an argument, and the people most in need of it would stop using it.
    /// </remarks>
    public Guid ReporterPersonId { get; private set; }

    public ReportTargetKind TargetKind { get; private set; }

    /// <summary>What was reported. No foreign key — see the type's remarks.</summary>
    public Guid TargetId { get; private set; }

    public ReportReason Reason { get; private set; }

    /// <summary>What they wrote, if anything. Optional on purpose.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <exception cref="DomainValidationException">There is no target, or the note is too long.</exception>
    public static Report Create(
        Guid id,
        Guid reporterPersonId,
        ReportTargetKind targetKind,
        Guid targetId,
        ReportReason reason,
        string? note,
        DateTimeOffset createdAt)
    {
        if (targetId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(TargetId), "A report needs something to be about.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > MaxNoteLength })
        {
            throw new DomainValidationException(
                nameof(Note),
                $"A note is at most {MaxNoteLength} characters.");
        }

        return new Report(id, reporterPersonId, targetKind, targetId, reason, trimmed, createdAt);
    }
}
