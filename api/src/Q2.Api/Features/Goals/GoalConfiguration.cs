using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        // Stored as text: readable in the database and stable if the enum
        // members are ever reordered.
        builder.Property(g => g.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(g => g.ProgressPercent).IsRequired();

        // Instants are normalised to UTC at the storage boundary. The domain
        // keeps DateTimeOffset (it carries the offset explicitly, which is the
        // right type for an instant), but the column is a plain UTC DateTime.
        //
        // This is not cosmetic: SQLite cannot ORDER BY a DateTimeOffset at all
        // ("SQLite does not support expressions of type 'DateTimeOffset' in
        // ORDER BY clauses"), so listing goals newest-first would fail. Storing
        // UTC also maps cleanly onto PostgreSQL's `timestamp with time zone`
        // later. See docs/adr/0004-sqlite-first.md.
        builder.Property(g => g.CreatedAt)
            .IsRequired()
            .HasConversion(
                value => value.UtcDateTime,
                value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        builder.Property(g => g.TargetDate);

        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.CreatedAt);

        builder.HasMany(g => g.Participants)
            .WithOne()
            .HasForeignKey(p => p.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Goal.Participants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class GoalParticipantConfiguration : IEntityTypeConfiguration<GoalParticipant>
{
    public void Configure(EntityTypeBuilder<GoalParticipant> builder)
    {
        builder.ToTable("GoalParticipants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(Goal.MaxParticipantNameLength);

        builder.HasIndex(p => new { p.GoalId, p.DisplayName }).IsUnique();
    }
}
