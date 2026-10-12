using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Replaces a challenge entry's <c>CapturedInApp</c> flag with its
/// <c>Source</c>: live, screenshot, or unknown (#64).
/// </summary>
/// <remarks>
/// Every existing entry becomes <c>Unknown</c>, whatever its flag said. The
/// client sent <c>capturedInApp: true</c> for every contribution, gallery picks
/// included, so the flag cannot tell one from the other — the same reasoning,
/// and the same treatment, as the <see cref="ProofSource"/> migration gave
/// proofs (docs/adr/0036-live-or-screenshot.md).
///
/// The column is dropped with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>:
/// EF's generic operation would rebuild the table with foreign keys switched
/// off, which <c>MigrationTests</c> refuses.
/// </remarks>
public partial class ChallengeEntrySource : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "ChallengeEntries",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.Sql("ALTER TABLE \"ChallengeEntries\" DROP COLUMN \"CapturedInApp\";");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down keeps what can be kept: a live photograph was captured in the
        // app; anything else was not known to be.
        migrationBuilder.AddColumn<bool>(
            name: "CapturedInApp",
            table: "ChallengeEntries",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE \"ChallengeEntries\" SET \"CapturedInApp\" = 1 WHERE \"Source\" = 'Live';");

        migrationBuilder.Sql("ALTER TABLE \"ChallengeEntries\" DROP COLUMN \"Source\";");
    }
}
