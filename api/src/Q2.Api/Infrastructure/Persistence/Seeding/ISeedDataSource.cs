using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// A whole world's worth of synthetic data, in the order it has to be written.
/// </summary>
/// <remarks>
/// One record rather than one collection per seed method: q2 is a graph — a
/// chat points at a goal, a goal at people, an activity at whoever did it — and
/// a seed that could return half of it would be a seed that could leave a
/// dangling reference behind.
/// </remarks>
public sealed record SeedData(
    IReadOnlyList<Person> People,
    IReadOnlyList<AppUser> Accounts,
    IReadOnlyList<Friendship> Friendships,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<GoalTask> Tasks,
    IReadOnlyList<ActivityEvent> Activity,
    IReadOnlyList<Conversation> Conversations,
    IReadOnlyList<UserSettings> Settings)
{
    public static SeedData Empty { get; } = new([], [], [], [], [], [], [], []);
}

/// <summary>
/// One profile's worth of synthetic data.
/// </summary>
/// <remarks>
/// Implementations must be pure: same <see cref="SeedContext"/> in, same rows
/// out. No clock, no random, no network, no real personal data.
/// </remarks>
public interface ISeedDataSource
{
    SeedProfile Profile { get; }

    /// <summary>Short description shown when the seed runs.</summary>
    string Description { get; }

    SeedData Create(SeedContext context);
}
