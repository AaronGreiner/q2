using Q2.Api.Features.People;

namespace Q2.Api.Features.Activity;

/// <summary>
/// One line in the friends' feed.
/// </summary>
/// <remarks>
/// The sentence is not sent, its parts are. "Jonas hat bei „Jeden Morgen
/// joggen" geliefert" is assembled by the client from
/// <see cref="Kind"/>, <see cref="Subject"/> and <see cref="Amount"/>, because
/// the app ships in German and English and a server-composed sentence could
/// only ever be one of them.
/// </remarks>
/// <param name="HasMyKudos">Whether the person asking has already given kudos.</param>
/// <param name="GoalId">
/// The goal the line is about, so tapping it opens that goal — but only when
/// the person asking may open it: its owner, or somebody it is shared with.
/// A friend's goal you are not on is still a line in your feed, and its id
/// would only lead to "not found"; that line opens the friend instead (#103).
/// </param>
public sealed record ActivityResponse(
    Guid Id,
    PersonSummary Actor,
    ActivityKind Kind,
    string? Subject,
    int? Amount,
    int KudosCount,
    bool HasMyKudos,
    DateTimeOffset OccurredAt,
    Guid? GoalId)
{
    /// <param name="visibleGoalIds">The goals among the lines' sources the viewer may read.</param>
    public static ActivityResponse From(
        ActivityEvent activity,
        Person actor,
        Guid viewerId,
        DateTimeOffset now,
        IReadOnlySet<Guid> visibleGoalIds) => new(
        activity.Id,
        PersonSummary.From(actor, now),
        activity.Kind,
        activity.Subject,
        activity.Amount,
        activity.KudosCount,
        activity.HasKudosFrom(viewerId),
        activity.OccurredAt,
        activity.SourceId is { } source && visibleGoalIds.Contains(source) ? source : null);
}

