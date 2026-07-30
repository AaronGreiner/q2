using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

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

    IReadOnlyList<Goal> CreateGoals(SeedContext context);
}

/// <summary>Shared helpers so the individual seeds stay readable.</summary>
internal static class SeedGoals
{
    /// <summary>Builds one goal, including participants, with fixed ids.</summary>
    public static Goal Build(
        SeedProfile profile,
        int index,
        string title,
        string? description,
        int progressPercent,
        DateOnly? targetDate,
        DateTimeOffset createdAt,
        params string[] participants)
    {
        var goal = Goal.Create(
            SeedIds.Goal(profile, index),
            title,
            description,
            progressPercent,
            targetDate,
            createdAt);

        for (var i = 0; i < participants.Length; i++)
        {
            goal.AddParticipant(SeedIds.Participant(profile, index, i + 1), participants[i]);
        }

        return goal;
    }

    /// <summary>Builds a goal and archives it.</summary>
    public static Goal BuildArchived(
        SeedProfile profile,
        int index,
        string title,
        string? description,
        int progressPercent,
        DateOnly? targetDate,
        DateTimeOffset createdAt,
        params string[] participants)
    {
        var goal = Build(profile, index, title, description, progressPercent, targetDate, createdAt, participants);
        goal.Archive();
        return goal;
    }
}
