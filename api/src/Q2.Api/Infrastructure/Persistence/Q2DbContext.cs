using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for q2.
/// </summary>
/// <remarks>
/// Entity configuration lives next to each feature (see
/// <see cref="GoalConfiguration"/>) and is picked up by assembly scanning, so
/// adding a feature does not mean editing this file.
/// </remarks>
public sealed class Q2DbContext(DbContextOptions<Q2DbContext> options) : DbContext(options)
{
    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<GoalParticipant> GoalParticipants => Set<GoalParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Q2DbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
