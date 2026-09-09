using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Proofs;

/// <summary>EF Core mapping for a photograph and the votes on it.</summary>
public sealed class ProofPhotoConfiguration : IEntityTypeConfiguration<ProofPhoto>
{
    public void Configure(EntityTypeBuilder<ProofPhoto> builder)
    {
        builder.ToTable("ProofPhotos");
        builder.HasKey(proof => proof.Id);

        builder.Property(proof => proof.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(proof => proof.Attempt).IsRequired();
        builder.Property(proof => proof.CapturedInApp).IsRequired();

        builder.Property(proof => proof.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(proof => proof.VotingDeadline)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        builder.Property(proof => proof.ResolvedAt)
            .HasConversion(InstantConversion.Optional);

        builder.HasMany(proof => proof.Votes)
            .WithOne()
            .HasForeignKey(vote => vote.ProofPhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(proof => proof.Reactions)
            .WithOne()
            .HasForeignKey(reaction => reaction.ProofPhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(ProofPhoto.Votes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ProofPhoto.Reactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // The maintenance job asks "which votes have run out" on every pass,
        // and the feed asks "which are still open". Both read this pair.
        builder.HasIndex(proof => new { proof.Status, proof.VotingDeadline });

        builder.HasIndex(proof => proof.GoalInstanceId);
    }
}

public sealed class ProofVoteConfiguration : IEntityTypeConfiguration<ProofVote>
{
    public void Configure(EntityTypeBuilder<ProofVote> builder)
    {
        builder.ToTable("ProofVotes");
        builder.HasKey(vote => vote.Id);

        builder.Property(vote => vote.Value)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(vote => vote.CastAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // One say per person, enforced by the database as well as by the model:
        // this is the index that would catch a second path to voting before it
        // reached anybody's streak.
        builder.HasIndex(vote => new { vote.ProofPhotoId, vote.VoterPersonId }).IsUnique();
    }
}

public sealed class ProofReactionConfiguration : IEntityTypeConfiguration<ProofReaction>
{
    public void Configure(EntityTypeBuilder<ProofReaction> builder)
    {
        builder.ToTable("ProofReactions");
        builder.HasKey(reaction => reaction.Id);

        builder.Property(reaction => reaction.Kind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.HasIndex(reaction => new { reaction.ProofPhotoId, reaction.PersonId }).IsUnique();
    }
}
