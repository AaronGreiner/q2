using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Goals;

/// <summary>
/// EF Core mapping for <see cref="GoalDraft"/>.
/// </summary>
/// <remarks>
/// The schedule columns carry the same names as on <c>Goals</c>, so somebody
/// reading both tables sees one shape twice rather than two.
/// </remarks>
public sealed class GoalDraftConfiguration : IEntityTypeConfiguration<GoalDraft>
{
    public void Configure(EntityTypeBuilder<GoalDraft> builder)
    {
        builder.ToTable("GoalDrafts");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(Goal.MaxTitleLength);

        builder.Property(d => d.Category)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Ignore(d => d.Icon);

        builder.OwnsOne(d => d.Schedule, schedule =>
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

            schedule.Ignore(s => s.Weekdays);
            schedule.Ignore(s => s.RequiredProofs);
            schedule.Ignore(s => s.Repeats);
        });

        builder.Navigation(d => d.Schedule).IsRequired();

        builder.Property(d => d.ReminderAt);

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // Leaves with its owner: an account that is deleted takes what it was
        // about to promise with it (docs/privacy.md).
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(d => d.OwnerPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // One waiting Do per person — it is the first Do, not a list.
        builder.HasIndex(d => d.OwnerPersonId).IsUnique();
    }
}
