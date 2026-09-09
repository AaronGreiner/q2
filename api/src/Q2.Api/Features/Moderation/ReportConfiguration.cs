using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Moderation;

/// <summary>EF Core mapping for a report.</summary>
public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.TargetKind)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(report => report.Reason)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(report => report.Note).HasMaxLength(Report.MaxNoteLength);

        builder.Property(report => report.CreatedAt)
            .IsRequired()
            .HasConversion(InstantConversion.Required);

        /*
         * The reporter cascades; the target does not exist as a relationship at
         * all.
         *
         * Deleting your account takes the reports you filed with it — they are
         * yours. What must not happen is the reverse: a reported photograph, or
         * a reported account, being removed and taking the record of why it was
         * removed along with it. That is why TargetId has no foreign key (see
         * Report), and it is the one place in this model where the absence of
         * one is the point rather than an oversight.
         */
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(report => report.ReporterPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Whoever answers the bell reads by target: "what else has been said
        // about this one?" is the first question after the second report.
        builder.HasIndex(report => new { report.TargetKind, report.TargetId });

        // One report per person and target: reporting the same picture twice is
        // not twice as urgent, and without this a tap-happy client would turn
        // one complaint into ten alerts.
        builder.HasIndex(report => new { report.ReporterPersonId, report.TargetKind, report.TargetId })
            .IsUnique();
    }
}
