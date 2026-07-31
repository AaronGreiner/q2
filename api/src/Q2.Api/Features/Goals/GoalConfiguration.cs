using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Goals;

/// <summary>
/// EF Core mapping for <see cref="Goal"/>.
/// </summary>
/// <remarks>
/// Kept provider-neutral on purpose: no SQLite-specific column types, no raw
/// SQL. Everything here works the same way against PostgreSQL later.
/// </remarks>
public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(Goal.MaxTitleLength);

        builder.Property(g => g.Description)
            .HasMaxLength(Goal.MaxDescriptionLength);

        builder.Property(g => g.Icon)
            .IsRequired()
            .HasMaxLength(40);

        // Enums are stored as text: readable in the database and stable if the
        // members are ever reordered.
        builder.Property(g => g.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(g => g.Rhythm)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(g => g.IsGroup).IsRequired();
        builder.Property(g => g.CompletedSteps).IsRequired();
        builder.Property(g => g.TotalSteps).IsRequired();

        // ProgressPercent is derived from the steps and deliberately has no
        // column: a stored copy is a second source of truth waiting to drift.
        builder.Ignore(g => g.ProgressPercent);

        builder.Property(g => g.ReminderAt);
        builder.Property(g => g.TargetDate);

        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(g => g.OwnerPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.CreatedAt);

        // Every list of goals starts from "mine", so this is the index that
        // carries the goals screen.
        builder.HasIndex(g => g.OwnerPersonId);

        builder.HasMany(g => g.Participants)
            .WithOne()
            .HasForeignKey(p => p.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Contributions)
            .WithOne()
            .HasForeignKey(c => c.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Goal.Participants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Contributions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable("GoalContributions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Date).IsRequired();

        // One row per goal per day is what makes the streak a count of days.
        builder.HasIndex(c => new { c.GoalId, c.Date }).IsUnique();
    }
}

public sealed class GoalParticipantConfiguration : IEntityTypeConfiguration<GoalParticipant>
{
    public void Configure(EntityTypeBuilder<GoalParticipant> builder)
    {
        builder.ToTable("GoalParticipants");
        builder.HasKey(p => p.Id);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(p => p.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.GoalId, p.PersonId }).IsUnique();
    }
}

public sealed class GoalTaskConfiguration : IEntityTypeConfiguration<GoalTask>
{
    public void Configure(EntityTypeBuilder<GoalTask> builder)
    {
        builder.ToTable("GoalTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(GoalTask.MaxTitleLength);

        builder.Property(t => t.Rhythm)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(t => t.WeeklyOn)
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(t => t.MeasureUnit)
            .HasMaxLength(GoalTask.MaxUnitLength);

        builder.Property(t => t.SortOrder).IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Ignore(t => t.IsMeasurable);

        // Tasks outlive nothing: deleting a goal takes its tasks with it, and a
        // standalone task simply has no goal.
        builder.HasOne<Goal>()
            .WithMany()
            .HasForeignKey(t => t.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(t => t.OwnerPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.GoalId);
        builder.HasIndex(t => t.SortOrder);

        // "What is on my list today" reads by owner before anything else.
        builder.HasIndex(t => t.OwnerPersonId);
    }
}
