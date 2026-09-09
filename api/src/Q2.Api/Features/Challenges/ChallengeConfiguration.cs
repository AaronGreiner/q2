using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Challenges;

/// <summary>EF Core mapping for the daily challenge and its contributions.</summary>
public sealed class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.ToTable("Challenges");
        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Day).IsRequired();

        builder.Property(challenge => challenge.Prompt)
            .IsRequired()
            .HasMaxLength(Challenge.MaxPromptLength);

        builder.Property(challenge => challenge.PublishedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(challenge => challenge.ExpiresAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(challenge => challenge.AnnouncedAt)
            .HasConversion(InstantConversion.Optional);

        builder.HasMany(challenge => challenge.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Challenge.Entries))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // One prompt per day, enforced by the database as well as by the
        // planner. This is the index that would catch a second path into the
        // queue — a manual insert, two hosts running the job at once — before
        // anybody saw two challenges on one morning.
        builder.HasIndex(challenge => challenge.Day).IsUnique();

        // "Which one is running?" is asked on every room read and on every
        // pass of the queue worker, and it is this pair every time.
        builder.HasIndex(challenge => new { challenge.PublishedAt, challenge.ExpiresAt });
    }
}

public sealed class ChallengeEntryConfiguration : IEntityTypeConfiguration<ChallengeEntry>
{
    public void Configure(EntityTypeBuilder<ChallengeEntry> builder)
    {
        builder.ToTable("ChallengeEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.CapturedInApp).IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(entry => entry.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(entry => entry.Reactions)
            .WithOne()
            .HasForeignKey(reaction => reaction.ChallengeEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(ChallengeEntry.Reactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Exactly one contribution per person and challenge. A second picture
        // replaces the first (Challenge.Contribute); this is what makes that
        // true even if a second path to contributing ever appears.
        builder.HasIndex(entry => new { entry.ChallengeId, entry.PersonId }).IsUnique();

        // The archive reads "everything of mine, newest first" and nothing else.
        builder.HasIndex(entry => new { entry.PersonId, entry.CreatedAt });
    }
}

public sealed class ChallengeReactionConfiguration : IEntityTypeConfiguration<ChallengeReaction>
{
    public void Configure(EntityTypeBuilder<ChallengeReaction> builder)
    {
        builder.ToTable("ChallengeReactions");
        builder.HasKey(reaction => reaction.Id);

        builder.Property(reaction => reaction.Kind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.HasIndex(reaction => new { reaction.ChallengeEntryId, reaction.PersonId }).IsUnique();
    }
}
