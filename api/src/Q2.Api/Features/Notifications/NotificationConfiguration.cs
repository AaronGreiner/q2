using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Notifications;

/// <summary>EF Core mapping for a push subscription.</summary>
public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Endpoint)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxEndpointLength);

        builder.Property(subscription => subscription.PublicKey)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxKeyLength);

        builder.Property(subscription => subscription.AuthSecret)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxKeyLength);

        builder.Property(subscription => subscription.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(subscription => subscription.LastDeliveredAt)
            .HasConversion(InstantConversion.Optional);

        builder.Property(subscription => subscription.ConsecutiveFailures).IsRequired();

        // Goes with the account. A push endpoint is the most identifying thing
        // this feature touches, and an erasure that left one behind would leave
        // a working handle to a device belonging to somebody who asked to be
        // gone (docs/adr/0022-blocking-reporting-and-erasure.md).
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(subscription => subscription.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * One row per endpoint, not per person.
         *
         * The same account on a phone and a laptop is two subscriptions and
         * both should ring; the same *browser* subscribing twice is one row
         * updated. This index is what makes the second sentence true, and it is
         * also what a shared device depends on — when the previous person signs
         * out and the next one subscribes, the browser hands back the same
         * endpoint and it has to change hands rather than collide.
         */
        builder.HasIndex(subscription => subscription.Endpoint).IsUnique();

        builder.HasIndex(subscription => subscription.PersonId);
    }
}

/// <summary>EF Core mapping for a line in the bell.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Kind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(line => line.Target)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(line => line.Subject)
            .HasMaxLength(Notification.MaxSubjectLength);

        builder.Property(line => line.OccurredAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // Goes with the recipient: their bell is theirs and nobody else's.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(line => line.RecipientPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * And with the actor.
         *
         * Somebody who deleted their account is gone, and a line in a friend's
         * bell naming them would be the shape of them left behind — the thing
         * erasure exists to prevent (docs/adr/0022-blocking-reporting-and-erasure.md).
         * A cascade rather than a clean-up step, so there is nothing for the
         * erasure path to forget.
         */
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(line => line.ActorPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // The bell reads one person's lines newest first; the retention pass
        // reads everybody's by age.
        builder.HasIndex(line => new { line.RecipientPersonId, line.OccurredAt });
        builder.HasIndex(line => line.OccurredAt);
        builder.HasIndex(line => line.ActorPersonId);
    }
}
