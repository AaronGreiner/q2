using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Activity;

/// <summary>EF Core mapping for the activity feed.</summary>
public sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(e => e.Subject)
            .HasMaxLength(ActivityEvent.MaxSubjectLength);

        builder.Property(e => e.KudosCount).IsRequired();

        builder.Property(e => e.OccurredAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(e => e.ActorPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.OccurredAt);

        // Withdrawing an entry looks it up by what produced it.
        builder.HasIndex(e => e.SourceId);

        builder.HasMany(e => e.Kudos)
            .WithOne()
            .HasForeignKey(k => k.ActivityEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ActivityEvent.Kudos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ActivityKudosConfiguration : IEntityTypeConfiguration<ActivityKudos>
{
    public void Configure(EntityTypeBuilder<ActivityKudos> builder)
    {
        builder.ToTable("ActivityKudos");
        builder.HasKey(k => k.Id);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(k => k.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // One kudos per person per activity — tapping twice takes it back, it
        // does not stack.
        builder.HasIndex(k => new { k.ActivityEventId, k.PersonId }).IsUnique();
    }
}
