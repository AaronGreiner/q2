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

        // Text like every enum here. Every goal from before Don'ts is a Do,
        // which is what the migration's default says.
        builder.Property(g => g.Kind)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(g => g.Limit);

        builder.Property(g => g.ProofMode)
            .HasMaxLength(32)
            .HasConversion<string>();

        /*
         * The schedule is owned rather than referenced: it has no identity of
         * its own, it is never queried without its goal, and the alternative —
         * four nullable columns nobody can see belong together — is how
         * "everyDays" ends up set on a goal that runs on weekdays.
         *
         * It still lands in the Goals table, one column per field, so a person
         * reading the database sees the whole goal in one row.
         */
        builder.OwnsOne(g => g.Schedule, schedule => MapSchedule(schedule, "Schedule"));

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
        builder.Ignore(g => g.IsDont);

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

        builder.HasMany(g => g.Changes)
            .WithOne()
            .HasForeignKey(change => change.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(g => g.PendingChange);
        builder.Ignore(g => g.LastChangeAppliedOn);

        builder.Metadata
            .FindNavigation(nameof(Goal.Participants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Instances))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Pauses))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Goal.Changes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    /// <summary>
    /// Maps a schedule owned by something other than a goal, into columns that
    /// start with <paramref name="prefix"/>. The same fields as a goal's own.
    /// </summary>
    internal static void MapSchedule<TOwner>(OwnedNavigationBuilder<TOwner, GoalSchedule> schedule, string prefix)
        where TOwner : class
    {
        schedule.Property(s => s.Kind)
            .HasColumnName($"{prefix}Kind")
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        schedule.Property(s => s.EveryDays).HasColumnName($"{prefix}EveryDays");

        schedule.Property(s => s.WeekdayList)
            .HasColumnName($"{prefix}Weekdays")
            .IsRequired()
            .HasMaxLength(32);

        schedule.Property(s => s.Times).HasColumnName($"{prefix}Times");

        schedule.Property(s => s.Period)
            .HasColumnName($"{prefix}Period")
            .HasMaxLength(16)
            .HasConversion<string>();

        // Parsed from WeekdayList on the way out; a second copy in the
        // database would be a second thing to keep in step.
        schedule.Ignore(s => s.Weekdays);
        schedule.Ignore(s => s.RequiredProofs);
        schedule.Ignore(s => s.Repeats);
    }
}

public sealed class GoalChangeConfiguration : IEntityTypeConfiguration<GoalChange>
{
    public void Configure(EntityTypeBuilder<GoalChange> builder)
    {
        builder.ToTable("GoalChanges");
        builder.HasKey(change => change.Id);

        builder.Property(change => change.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(change => change.ResolvedAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(change => change.AppliesOn);

        builder.Property(change => change.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(change => change.Objectable).IsRequired();

        // User content, the same bounds as the goal's own columns.
        builder.Property(change => change.TitleFrom).HasMaxLength(Goal.MaxTitleLength);
        builder.Property(change => change.TitleTo).HasMaxLength(Goal.MaxTitleLength);
        builder.Property(change => change.IconFrom).HasMaxLength(40);
        builder.Property(change => change.IconTo).HasMaxLength(40);
        builder.Property(change => change.DescriptionChanged).IsRequired();
        builder.Property(change => change.DescriptionFrom).HasMaxLength(Goal.MaxDescriptionLength);
        builder.Property(change => change.DescriptionTo).HasMaxLength(Goal.MaxDescriptionLength);

        builder.Property(change => change.LimitFrom);
        builder.Property(change => change.LimitTo);

        builder.Property(change => change.ProofModeFrom)
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(change => change.ProofModeTo)
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(change => change.TargetDateFrom);
        builder.Property(change => change.TargetDateTo);

        // Owned and optional, in the change's own row: null on both sides when
        // the schedule did not change.
        builder.OwnsOne(change => change.ScheduleFrom, schedule => GoalConfiguration.MapSchedule(schedule, "ScheduleFrom"));
        builder.OwnsOne(change => change.ScheduleTo, schedule => GoalConfiguration.MapSchedule(schedule, "ScheduleTo"));

        builder.Ignore(change => change.VetoCount);
        builder.Ignore(change => change.IsPending);
        builder.Ignore(change => change.ObjectionEndsAt);
        builder.Ignore(change => change.AddedCheckerIds);
        builder.Ignore(change => change.RemovedCheckerIds);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(change => change.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(change => change.Checkers)
            .WithOne()
            .HasForeignKey(checker => checker.GoalChangeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(change => change.Vetoes)
            .WithOne()
            .HasForeignKey(veto => veto.GoalChangeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalChange.Checkers))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(GoalChange.Vetoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // "Is a change waiting on this goal" is asked on every read of it,
        // like "is it paused".
        builder.HasIndex(change => new { change.GoalId, change.Status });
    }
}

public sealed class GoalChangeCheckerConfiguration : IEntityTypeConfiguration<GoalChangeChecker>
{
    public void Configure(EntityTypeBuilder<GoalChangeChecker> builder)
    {
        builder.ToTable("GoalChangeCheckers");
        builder.HasKey(checker => checker.Id);

        builder.Property(checker => checker.Added).IsRequired();

        // Erasing somebody takes them out of the edits that named them too.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(checker => checker.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(checker => new { checker.GoalChangeId, checker.PersonId }).IsUnique();
    }
}

public sealed class GoalChangeVetoConfiguration : IEntityTypeConfiguration<GoalChangeVeto>
{
    public void Configure(EntityTypeBuilder<GoalChangeVeto> builder)
    {
        builder.ToTable("GoalChangeVetoes");
        builder.HasKey(veto => veto.Id);

        builder.Property(veto => veto.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(veto => veto.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // One objection per person and change, as for a pause.
        builder.HasIndex(veto => new { veto.GoalChangeId, veto.PersonId }).IsUnique();
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

        // Null for a Do's window: what tells the two apart in the history.
        builder.Property(i => i.Limit);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Ignore(i => i.RemainingProofs);
        builder.Ignore(i => i.PendingProof);
        builder.Ignore(i => i.NextAttempt);
        builder.Ignore(i => i.AcceptsProof);
        builder.Ignore(i => i.IsDont);
        builder.Ignore(i => i.Uses);

        builder.HasMany(i => i.SpotChecks)
            .WithOne()
            .HasForeignKey(check => check.GoalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalInstance.SpotChecks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.Proofs)
            .WithOne()
            .HasForeignKey(proof => proof.GoalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalInstance.Proofs))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.UseLog)
            .WithOne()
            .HasForeignKey(use => use.GoalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GoalInstance.UseLog))!
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

public sealed class DontUseConfiguration : IEntityTypeConfiguration<DontUse>
{
    public void Configure(EntityTypeBuilder<DontUse> builder)
    {
        builder.ToTable("DontUses");
        builder.HasKey(use => use.Id);

        builder.Property(use => use.LoggedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // No foreign key to the image, like a proof's: deleting a picture
        // leaves the use, which was logged whether or not it had one.
        builder.Property(use => use.ImageId);

        builder.HasIndex(use => use.GoalInstanceId);
    }
}

public sealed class SpotCheckConfiguration : IEntityTypeConfiguration<SpotCheck>
{
    public void Configure(EntityTypeBuilder<SpotCheck> builder)
    {
        builder.ToTable("SpotChecks");
        builder.HasKey(check => check.Id);

        builder.Property(check => check.Day).IsRequired();

        builder.Property(check => check.ScheduledAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(check => check.IssuedAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(check => check.ExpiresAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(check => check.ResolvedAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(check => check.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        // No foreign key to the photograph: it lives on the same window, and
        // the check keeps its outcome whatever happens to the picture.
        builder.Property(check => check.ProofPhotoId);

        // One per window and day, which is what makes drawing twice harmless.
        builder.HasIndex(check => new { check.GoalInstanceId, check.Day }).IsUnique();

        // The worker asks "what is due" every half minute, across everybody.
        builder.HasIndex(check => new { check.Status, check.ScheduledAt });
    }
}
