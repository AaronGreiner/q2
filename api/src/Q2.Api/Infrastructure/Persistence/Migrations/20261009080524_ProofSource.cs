using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Replaces a proof's <c>CapturedInApp</c> flag with its <c>Source</c>: live,
/// screenshot, or unknown (#61).
/// </summary>
/// <remarks>
/// Every existing proof becomes <c>Unknown</c>, whatever its flag said. The
/// client sent <c>capturedInApp: true</c> for every delivery, gallery picks
/// included, so the flag cannot tell one from the other and mapping it to
/// <c>Live</c> would label screenshots as live photographs after the fact
/// (docs/adr/0036-live-or-screenshot.md).
///
/// The column is dropped with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>,
/// as in <c>AccountsAndTwoSidedFriendships</c>: EF's generic operation would
/// rebuild the table with foreign keys switched off, which
/// <c>MigrationTests</c> refuses.
/// </remarks>
public partial class ProofSource : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "ProofPhotos",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.Sql("ALTER TABLE \"ProofPhotos\" DROP COLUMN \"CapturedInApp\";");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down keeps what can be kept: a live photograph was captured in the
        // app; anything else was not known to be.
        migrationBuilder.AddColumn<bool>(
            name: "CapturedInApp",
            table: "ProofPhotos",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE \"ProofPhotos\" SET \"CapturedInApp\" = 1 WHERE \"Source\" = 'Live';");

        migrationBuilder.Sql("ALTER TABLE \"ProofPhotos\" DROP COLUMN \"Source\";");
    }
}
