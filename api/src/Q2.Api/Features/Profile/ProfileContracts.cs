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
/// <param name="Balance">
/// Everything this person has delivered and everything they have missed, over
/// all of their own goals. Unscoped, because it is their own profile — the
/// scoped version is what somebody else sees
/// (<see cref="PersonProfileResponse"/>).
/// </param>
/// <param name="AtRisk">
/// Their own windows that are close enough to failing to say so, right now.
/// Empty for most of the day: the rule only turns on in the evening.
/// </param>
/// <remarks>
/// <see cref="UnreadChats"/> and <see cref="PendingFriendRequests"/> are here
/// rather than on their own endpoint because they are what the bottom
/// navigation puts a badge on, and it is on every screen. A dedicated "counts"
/// call would be a second request on every page load to answer a question this
/// one already had the data for.
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
    IReadOnlyList<ActivityResponse> RecentActivity,
    BalanceResponse Balance,
    IReadOnlyList<GoalResponse> AtRisk);

/// <summary>
/// Changes to the signed-in person's own profile. Omitted properties keep
/// their value.
/// </summary>
/// <param name="DisplayName">
/// The name friends see. The initials beside it are re-derived from it, so
/// renaming does not leave the old ones behind.
/// </param>
/// <param name="AvatarImageId">
/// An image uploaded with <c>purpose=Avatar</c> by this person. It has to be
/// theirs and it has to be an avatar; anything else is answered 404, because
/// otherwise "set my avatar to this id" would be a way to read somebody else's
/// photograph through their own profile.
/// </param>
/// <remarks>
/// There is no way to say "remove my picture" here, and that is deliberate:
/// removing it is <c>DELETE /api/images/{id}</c>, which also stops it counting
/// against the person's storage. One path rather than two, and the one that
/// actually gets rid of the photograph.
///
/// Both properties carry a default, so each is genuinely optional in the
/// contract — a request that only renames does not have to send a null picture,
/// and the generated client does not make one required. Nullable as well, so a
/// body that omits everything is an empty change rather than a binding failure.
/// </remarks>
public sealed record UpdateProfileRequest(string? DisplayName = null, Guid? AvatarImageId = null);
