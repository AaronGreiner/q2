using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
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

        builder.Property(g => g.IsGroup).IsRequired();

        /*
         * The schedule is owned rather than referenced: it has no identity of
         * its own, it is never queried without its goal, and the alternative —
         * four nullable columns nobody can see belong together — is how
         * "everyDays" ends up set on a goal that runs on weekdays.
         *
         * It still lands in the Goals table, one column per field, so a person
         * reading the database sees the whole goal in one row.
         */
        builder.OwnsOne(g => g.Schedule, schedule =>
        {
            schedule.Property(s => s.Kind)
                .HasColumnName("ScheduleKind")
                .IsRequired()
                .HasMaxLength(32)
                .HasConversion<string>();

            schedule.Property(s => s.EveryDays).HasColumnName("ScheduleEveryDays");

            schedule.Property(s => s.WeekdayList)
                .HasColumnName("ScheduleWeekdays")
                .IsRequired()
                .HasMaxLength(32);

            schedule.Property(s => s.Times).HasColumnName("ScheduleTimes");

            schedule.Property(s => s.Period)
                .HasColumnName("SchedulePeriod")
                .HasMaxLength(16)
                .HasConversion<string>();

            // Parsed from WeekdayList on the way out; a second copy in the
            // database would be a second thing to keep in step.
            schedule.Ignore(s => s.Weekdays);
            schedule.Ignore(s => s.RequiredProofs);
            schedule.Ignore(s => s.Repeats);
        });

        builder.Navigation(g => g.Schedule).IsRequired();

        // Derived from the windows, and deliberately without a column each: a
        // stored streak needs a nightly job to notice a missed day, and a job
        // that runs twice invents one.
        builder.Ignore(g => g.Streak);
        builder.Ignore(g => g.Balance);
        builder.Ignore(g => g.CurrentInstance);
        builder.Ignore(g => g.LatestInstance);
        builder.Ignore(g => g.DeliveredDays);
        builder.Ignore(g => g.IsClosed);

        builder.Property(g => g.ReminderAt);
        builder.Property(g => g.TargetDate);

        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(g => g.ClosedAt)
            .HasConversion(InstantConversion.Optional);

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

        builder.HasMany(g => g.Instances)
            .WithOne()
            .HasForeignKey(i => i.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Pauses)
            .WithOne()
            .HasForeignKey(pause => pause.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Goal.Participants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Instances))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Pauses))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class GoalPauseConfiguration : IEntityTypeConfiguration<GoalPause>
{
    public void Configure(EntityTypeBuilder<GoalPause> builder)
    {
        builder.ToTable("GoalPauses");
        builder.HasKey(pause => pause.Id);

        builder.Property(pause => pause.Reason)
            .IsRequired()
            .HasMaxLength(PauseRules.MaxReasonLength);

        builder.Property(pause => pause.StartsOn).IsRequired();
        builder.Property(pause => pause.EndsOn).IsRequired();

        builder.Property(pause => pause.StartsAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(pause => pause.EndsAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(pause => pause.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(pause => pause.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Ignore(pause => pause.VetoCount);
        builder.Ignore(pause => pause.Days);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(pause => pause.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // No foreign key to the window on purpose. The pause outlives it: a
        // goal deleted from the archive takes both, but a window that somehow
        // went without its pause would leave an objection pointing at nothing.
        builder.Property(pause => pause.GoalInstanceId);

        builder.HasMany(pause => pause.Vetoes)
            .WithOne()
            .HasForeignKey(veto => veto.GoalPauseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalPause.Vetoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // "Is this goal paused right now" is asked on every read of every goal.
        builder.HasIndex(pause => new { pause.GoalId, pause.Status });
    }
}

public sealed class PauseVetoConfiguration : IEntityTypeConfiguration<PauseVeto>
{
    public void Configure(EntityTypeBuilder<PauseVeto> builder)
    {
        builder.ToTable("PauseVetoes");
        builder.HasKey(veto => veto.Id);

        builder.Property(veto => veto.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(veto => veto.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // One objection per person and pause. Taking one back removes the row,
        // so the uniqueness is what makes "toggle" mean toggle.
        builder.HasIndex(veto => new { veto.GoalPauseId, veto.PersonId }).IsUnique();
    }
}

public sealed class GoalInstanceConfiguration : IEntityTypeConfiguration<GoalInstance>
{
    public void Configure(EntityTypeBuilder<GoalInstance> builder)
    {
        builder.ToTable("GoalInstances");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.StartsOn).IsRequired();
        builder.Property(i => i.DueOn).IsRequired();

        builder.Property(i => i.StartsAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(i => i.DueAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(i => i.ResolvedAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(i => i.RiskNotifiedAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(i => i.RequiredProofs).IsRequired();
        builder.Property(i => i.ConfirmedProofs).IsRequired();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Ignore(i => i.RemainingProofs);
        builder.Ignore(i => i.PendingProof);
        builder.Ignore(i => i.NextAttempt);
        builder.Ignore(i => i.AcceptsProof);

        builder.HasMany(i => i.Proofs)
            .WithOne()
            .HasForeignKey(proof => proof.GoalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalInstance.Proofs))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // One window per goal per period. This is what makes the maintenance
        // job safe to run twice: the second insert cannot happen.
        builder.HasIndex(i => new { i.GoalId, i.StartsOn, i.DueOn }).IsUnique();

        // The job asks "what is open and overdue" across every goal there is,
        // which is the one query in the app that is not scoped to one person.
        builder.HasIndex(i => new { i.Status, i.DueAt });
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
