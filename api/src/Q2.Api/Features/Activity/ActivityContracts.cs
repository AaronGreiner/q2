using Q2.Api.Features.People;

namespace Q2.Api.Features.Activity;

/// <summary>
/// One line in the friends' feed.
/// </summary>
/// <remarks>
/// The sentence is not sent, its parts are. "Jonas hat „Joggen 5 km"
/// abgeschlossen" is assembled by the client from
/// <see cref="Kind"/>, <see cref="Subject"/> and <see cref="Amount"/>, because
/// the app ships in German and English and a server-composed sentence could
/// only ever be one of them.
/// </remarks>
/// <param name="HasMyKudos">Whether the person asking has already given kudos.</param>
public sealed record ActivityResponse(
    Guid Id,
    PersonSummary Actor,
    ActivityKind Kind,
    string? Subject,
    int? Amount,
    int KudosCount,
    bool HasMyKudos,
    DateTimeOffset OccurredAt)
{
    public static ActivityResponse From(ActivityEvent activity, Person actor, Guid viewerId, DateTimeOffset now) => new(
        activity.Id,
        PersonSummary.From(actor, now),
        activity.Kind,
        activity.Subject,
        activity.Amount,
        activity.KudosCount,
        activity.HasKudosFrom(viewerId),
        activity.OccurredAt);
}

/// <summary>One row of the weekly leaderboard.</summary>
/// <param name="Rank">1-based, assigned server-side so ties break the same way everywhere.</param>
/// <param name="IsMe">Lets the client highlight the row without comparing ids itself.</param>
public sealed record LeaderboardEntryResponse(int Rank, PersonSummary Person, int Kudos, bool IsMe);
