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

        /*
         * A complex type rather than an owned entity or a table of its own: the
         * notification preferences are a value — replaced whole, compared by
         * what they contain — and they live in the same row as the rest of the
         * person's preferences.
         *
         * The column names are written out because two of them predate the value
         * type and are kept (NotifyMessages, NotifyChallenge, the quiet hours),
         * so a database migrated before it still has everybody's choices under
         * the names it has always had.
         */
        builder.ComplexProperty(s => s.Notifications, notifications =>
        {
            notifications.Property(n => n.Messages).HasColumnName("NotifyMessages");
            notifications.Property(n => n.Friendships).HasColumnName("NotifyFriendships");
            notifications.Property(n => n.VotesDue).HasColumnName("NotifyVotesDue");
            notifications.Property(n => n.ProofResults).HasColumnName("NotifyProofResults");
            notifications.Property(n => n.Reactions).HasColumnName("NotifyReactions");
            notifications.Property(n => n.GoalUpdates).HasColumnName("NotifyGoalUpdates");
            notifications.Property(n => n.FriendsAtRisk).HasColumnName("NotifyFriendsAtRisk");
            notifications.Property(n => n.Challenge).HasColumnName("NotifyChallenge");
            notifications.Property(n => n.QuietHoursFrom).HasColumnName("QuietHoursFrom");
            notifications.Property(n => n.QuietHoursTo).HasColumnName("QuietHoursTo");
        });

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(s => s.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.PersonId).IsUnique();
    }
}
