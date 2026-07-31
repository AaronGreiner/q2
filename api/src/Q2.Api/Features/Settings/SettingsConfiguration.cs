using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Settings;

/// <summary>EF Core mapping for <see cref="UserSettings"/>.</summary>
public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Theme)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(s => s.Language)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(s => s.NotifyReminders).IsRequired();
        builder.Property(s => s.NotifyKudos).IsRequired();
        builder.Property(s => s.NotifyMessages).IsRequired();
        builder.Property(s => s.NotifyWeeklyReview).IsRequired();

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(s => s.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.PersonId).IsUnique();
    }
}
