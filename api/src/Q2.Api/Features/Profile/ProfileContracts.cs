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
/// The badge numbers used to ride along here, because the bottom navigation is
/// on every screen and this was a read every screen made anyway. They have a
/// read of their own now (<see cref="Q2.Api.Features.Notifications.CountsResponse"/>):
/// they arrive over the live connection whenever one of them moves, and the
/// whole profile has no business being sent again for that
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
/// </remarks>
/// <param name="Bio">A few words of their own under the name (#20), or null.</param>
/// <param name="ThisWeek">
/// The same count over the windows of their own goals that end in this
/// Monday-to-Sunday week, in their zone — with the ones still open (#27).
/// </param>
public sealed record ProfileResponse(
    PersonSummary Person,
    string? Bio,
    int Streak,
    int KudosReceived,
    int GoalsCompleted,
    IReadOnlyList<BadgeResponse> Badges,
    IReadOnlyList<ActivityResponse> RecentActivity,
    BalanceResponse Balance,
    IReadOnlyList<GoalResponse> AtRisk,
    WeekResponse ThisWeek);

/// <summary>
/// This week's windows of somebody's own goals, by how they ended (#27).
/// </summary>
/// <remarks>
/// A window belongs to the week its last day falls in: a monthly quota counts
/// in the week it is due, not in every week it runs through. States, not
/// actions — the screen shows them in grey (docs/adr/0015-qdos-design-language.md).
/// </remarks>
public sealed record WeekResponse(DateOnly From, DateOnly To, int Done, int Missed, int Relapsed, int Open);

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
/// Every property carries a default, so each is genuinely optional in the
/// contract — a request that only renames does not have to send a null picture,
/// and the generated client does not make one required. Nullable as well, so a
/// body that omits everything is an empty change rather than a binding failure.
/// </remarks>
/// <param name="Bio">
/// A few words under the name (#20). Left out, it stays; blank, it goes.
/// </param>
public sealed record UpdateProfileRequest(string? DisplayName = null, Guid? AvatarImageId = null, string? Bio = null);
