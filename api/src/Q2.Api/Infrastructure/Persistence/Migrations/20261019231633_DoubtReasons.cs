using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// What a doubter said was missing, on their vote, and the most-given reason on
/// a refusal's bell line (#111). Nothing earlier had either.
/// </summary>
/// <remarks>
/// Down drops the columns with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class DoubtReasons : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Reason",
            table: "ProofVotes",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Detail",
            table: "Notifications",
            type: "TEXT",
            maxLength: 32,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"ProofVotes\" DROP COLUMN \"Reason\";");
        migrationBuilder.Sql("ALTER TABLE \"Notifications\" DROP COLUMN \"Detail\";");
    }
}
