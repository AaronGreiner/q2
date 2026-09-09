using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.People;

/// <summary>
/// EF Core mapping for <see cref="Person"/> and everything hanging off it.
/// </summary>
/// <remarks>
/// Kept provider-neutral on purpose: no SQLite-specific column types, no raw
/// SQL. Everything here works the same way against PostgreSQL later.
/// </remarks>
public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(Person.MaxDisplayNameLength);

        builder.Property(p => p.Handle)
            .IsRequired()
            .HasMaxLength(Person.MaxHandleLength);

        builder.Property(p => p.Initials)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(p => p.AvatarColor)
            .IsRequired()
            .HasMaxLength(9);

        builder.Property(p => p.KudosReceived).IsRequired();
        builder.Property(p => p.GoalsCompleted).IsRequired();

        builder.Property(p => p.LastSeenAt)
            .HasConversion(InstantConversion.Optional);

        // Long enough for any IANA identifier ("America/Argentina/Buenos_Aires"
        // is the longest at 32).
        builder.Property(p => p.TimeZoneId)
            .HasMaxLength(64);

        builder.Property(p => p.InviteCode)
            .HasMaxLength(Person.MaxInviteCodeLength);

        /*
         * Unique, with no filter needed.
         *
         * Uniqueness is what a redemption depends on: the lookup is "whose code
         * is this", and a second row with the same code would give that
         * question two answers at the exact moment somebody is being handed a
         * friend. The many people without a code do not collide, because SQL
         * treats NULLs as distinct in a unique index — a filtered index would
         * be raw SQL in a file that is deliberately provider-neutral, buying
         * nothing.
         */
        builder.HasIndex(p => p.InviteCode).IsUnique();

        /*
         * No foreign key to Images, on purpose.
         *
         * The reference is cleared by ImageService.DeleteAsync in the same
         * transaction that removes the image, so the constraint would never
         * fire — and a cascade would be a second place where deleting a
         * photograph changes somebody's profile, one that no test would see.
         */

        // Unique because the handle is how one person finds another on the
        // friends screen, and two people answering to "@lena.schmidt" would
        // make that search a coin toss.
        builder.HasIndex(p => p.Handle).IsUnique();

        builder.HasMany(p => p.CheckIns)
            .WithOne()
            .HasForeignKey(c => c.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Badges)
            .WithOne()
            .HasForeignKey(b => b.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Person.CheckIns))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Person.Badges))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.ToTable("DailyCheckIns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Date).IsRequired();

        // One check-in per person per day is what makes a streak a count of
        // days rather than of taps.
        builder.HasIndex(c => new { c.PersonId, c.Date }).IsUnique();
    }
}

public sealed class PersonBadgeConfiguration : IEntityTypeConfiguration<PersonBadge>
{
    public void Configure(EntityTypeBuilder<PersonBadge> builder)
    {
        builder.ToTable("PersonBadges");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Badge)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(b => b.EarnedOn).IsRequired();

        builder.HasIndex(b => new { b.PersonId, b.Badge }).IsUnique();
    }
}

public sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("Blocks");
        builder.HasKey(block => block.Id);

        builder.Property(block => block.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(block => block.BlockerPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(block => block.BlockedPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per ordered pair. Two people blocking each other is two rows,
        // and that is correct: each of them can lift only their own.
        builder.HasIndex(block => new { block.BlockerPersonId, block.BlockedPersonId }).IsUnique();

        // "Who is hidden from me" reads both columns on every request that can
        // put a person on a screen, so the second direction gets its own index.
        builder.HasIndex(block => block.BlockedPersonId);
    }
}

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(f => f.RequestedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(f => f.RespondedAt)
            .HasConversion(InstantConversion.Optional);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per ordered pair. The mirrored duplicate — B asking A while
        // A's request to B is still open — is not something a unique index can
        // express, so FriendsService looks both ways before it inserts and
        // turns the second ask into an acceptance.
        builder.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();

        // The friends screen reads "requests waiting for me" on every load.
        builder.HasIndex(f => f.AddresseeId);
    }
}
