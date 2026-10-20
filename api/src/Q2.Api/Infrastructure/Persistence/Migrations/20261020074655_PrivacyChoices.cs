using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Who can find a person through the search, and whether their activity shows
/// (#106).
/// </summary>
/// <remarks>
/// Everybody keeps what they had: findable by any part of their name, and
/// their activity shown — the choice made in the session for these defaults.
///
/// Down drops the columns with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class PrivacyChoices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Findability",
            table: "People",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Everyone");

        migrationBuilder.AddColumn<bool>(
            name: "ShowsActivity",
            table: "People",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"People\" DROP COLUMN \"Findability\";");
        migrationBuilder.Sql("ALTER TABLE \"People\" DROP COLUMN \"ShowsActivity\";");
    }
}
