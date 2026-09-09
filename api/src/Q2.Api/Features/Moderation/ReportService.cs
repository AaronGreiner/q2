using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Moderation;

/// <summary>
/// Asking for something to be looked at.
/// </summary>
/// <remarks>
/// The whole feature is three lines of logic and one rule that is easy to get
/// wrong: **you may only report what you can already see.** Without it, the
/// report endpoint would be a way to ask whether a given id exists — a
/// photograph, a contribution, an account — which is exactly the question every
/// other read in this API answers with 404.
///
/// Nothing here reads reports back. Delivery is <see cref="IReportSink"/>'s
/// job, and it happens after the row is saved: a report that reached somebody
/// but was not recorded cannot be acted on twice.
/// </remarks>
public sealed class ReportService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    BlockList blockList,
    IReportSink sink,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<ReportService> logger)
{
    /// <exception cref="DomainValidationException">A field is missing or the note is too long.</exception>
    /// <exception cref="ResourceNotFoundException">There is nothing there, or nothing you can see.</exception>
    public async Task<ReportReceiptResponse> FileAsync(
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TargetKind is not { } kind)
        {
            throw new DomainValidationException(nameof(request.TargetKind), "A report needs something to be about.");
        }

        if (request.TargetId is not { } targetId || targetId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(request.TargetId), "A report needs something to be about.");
        }

        if (request.Reason is not { } reason)
        {
            throw new DomainValidationException(nameof(request.Reason), "A report needs a reason.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);

        if (kind == ReportTargetKind.Person && targetId == me.Id)
        {
            throw new DomainValidationException(nameof(request.TargetId), "You cannot report yourself.");
        }

        if (!await CanSeeAsync(kind, targetId, me.Id, cancellationToken))
        {
            throw new ResourceNotFoundException(kind.ToString(), targetId);
        }

        /*
         * Reporting the same thing twice is one report.
         *
         * The unique index says so as well, but reaching it would mean a person
         * who tapped twice got an error for doing nothing wrong — and it would
         * ring the bell a second time for a complaint that has not changed.
         */
        var existing = await database.Reports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report => report.ReporterPersonId == me.Id
                    && report.TargetKind == kind
                    && report.TargetId == targetId,
                cancellationToken);

        if (existing is not null)
        {
            return new ReportReceiptResponse(existing.Id, existing.CreatedAt);
        }

        var now = timeProvider.GetUtcNow();

        var report = Report.Create(
            idGenerator.NewId(),
            me.Id,
            kind,
            targetId,
            reason,
            request.Note,
            now);

        database.Reports.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        // The row first, the bell second. Never the note and never the
        // reporter — see ObservabilityReportSink.
        await sink.DeliverAsync(report, cancellationToken);

        logger.LogInformation("A report was filed about a {ReportKind}", kind);

        return new ReportReceiptResponse(report.Id, report.CreatedAt);
    }

    /// <summary>
    /// Whether this person can see the thing they are reporting.
    /// </summary>
    /// <remarks>
    /// One branch per kind, so a new kind of reportable thing is a
    /// compiler-visible decision rather than something that defaults to
    /// reportable — the same shape as
    /// <see cref="Q2.Api.Features.Images.ImageService.CanRead"/>, and for the
    /// same reason.
    /// </remarks>
    private async Task<bool> CanSeeAsync(
        ReportTargetKind kind,
        Guid targetId,
        Guid meId,
        CancellationToken cancellationToken) => kind switch
        {
            // Anybody signed in can reach anybody's profile, so the only test
            // is that they exist and are not hidden from each other.
            ReportTargetKind.Person =>
                !await blockList.IsHiddenFromMeAsync(targetId, cancellationToken)
                && await database.People.AsNoTracking()
                    .AnyAsync(person => person.Id == targetId, cancellationToken),

            // A photograph's audience is the audience of its goal.
            ReportTargetKind.Proof => await database.ProofPhotos
                .AsNoTracking()
                .AnyAsync(
                    proof => proof.Id == targetId
                        && database.Goals.Any(goal =>
                            goal.Instances.Any(instance => instance.Id == proof.GoalInstanceId)
                            && (goal.OwnerPersonId == meId
                                || goal.Participants.Any(participant => participant.PersonId == meId))),
                    cancellationToken),

            // A contribution is visible to a friend who has contributed to the
            // same challenge — the reciprocity rule, asked here as well so that
            // "report" cannot see further than the room does.
            ReportTargetKind.ChallengeEntry => await database.ChallengeEntries
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == targetId
                        && entry.PersonId != meId
                        && database.ChallengeEntries.Any(mine =>
                            mine.ChallengeId == entry.ChallengeId && mine.PersonId == meId)
                        && database.Friendships.Any(friendship =>
                            friendship.Status == FriendshipStatus.Accepted
                            && ((friendship.RequesterId == meId && friendship.AddresseeId == entry.PersonId)
                                || (friendship.RequesterId == entry.PersonId && friendship.AddresseeId == meId))),
                    cancellationToken),

            _ => false,
        };
}
