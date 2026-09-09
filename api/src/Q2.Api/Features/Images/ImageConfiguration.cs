using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Images;

/// <summary>EF Core mapping for <see cref="StoredImage"/>.</summary>
public sealed class StoredImageConfiguration : IEntityTypeConfiguration<StoredImage>
{
    public void Configure(EntityTypeBuilder<StoredImage> builder)
    {
        builder.ToTable("Images");
        builder.HasKey(image => image.Id);

        builder.Property(image => image.Purpose)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(image => image.ContentType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(image => image.ByteSize).IsRequired();
        builder.Property(image => image.Width).IsRequired();
        builder.Property(image => image.Height).IsRequired();

        builder.Property(image => image.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        // Every quota check is "how much has this person used", and every
        // delete of a person's account will be the same question.
        builder.HasIndex(image => image.OwnerPersonId);
    }
}
