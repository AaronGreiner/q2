using Q2.Api.Features.Activity;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Profile;

/// <summary>A badge and whether it has been earned.</summary>
/// <remarks>
/// Unearned badges are returned too. The profile shows the whole set, greyed
/// out — a badge you cannot see is not something to aim for.
/// </remarks>
public sealed record BadgeResponse(BadgeKey Key, bool IsEarned, DateOnly? EarnedOn);

/// <summary>
/// Who you are and how you are doing — the home screen's header and the whole
/// profile screen, in one read.
/// </summary>
/// <param name="WeekActivity">
/// Monday to Sunday of the current week, seven entries. Always in that order,
/// so the client never has to work out which end of the array is Monday.
/// </param>
/// <param name="UnreadChats">
/// How many conversations have something unread in them.
/// </param>
/// <param name="PendingFriendRequests">
/// How many people are waiting for an answer.
/// </param>
/// <remarks>
/// The last two are here rather than on their own endpoint because they are
/// what the bottom navigation puts a badge on, and it is on every screen. A
/// dedicated "counts" call would be a second request on every page load to
/// answer a question this one already had the data for.
/// </remarks>
public sealed record ProfileResponse(
    PersonSummary Person,
    int Streak,
    int KudosReceived,
    int GoalsCompleted,
    IReadOnlyList<bool> WeekActivity,
    DaySummaryResponse Today,
    int UnreadChats,
    int PendingFriendRequests,
    IReadOnlyList<BadgeResponse> Badges,
    IReadOnlyList<ActivityResponse> RecentActivity);
