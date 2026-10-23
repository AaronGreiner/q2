using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Q2.Api.Features.Images;

/// <summary>
/// The columns of <see cref="MediaParts"/>, mapped the same way on every table
/// that carries a picture from q2's camera besides its main one (#152).
/// </summary>
/// <remarks>
/// Plain ids, like the main picture's: the bytes and their row are reached
/// through the image feature, and a deleted picture leaves the row that
/// pointed at it. The kinds are text, so the database stays readable and
/// reordering <see cref="MediaKind"/> cannot change what a row means.
/// </remarks>
internal static class MediaPartsMapping
{
    public static void HasMediaParts<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        // Every row from before #152 is a single photograph, which is what the
        // migration fills in for them.
        builder.Property<MediaKind>("Media")
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property<Guid?>("PosterImageId");
        builder.Property<Guid?>("InsetImageId");

        builder.Property<MediaKind?>("InsetMedia")
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property<Guid?>("InsetPosterImageId");
    }
}
