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
