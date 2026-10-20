using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// The switch for the line a badge earned puts in its owner's bell (#139).
/// </summary>
/// <remarks>
/// On for everybody who already has settings, as every switch is by default
/// (<c>NotificationPreferences</c>).
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class BadgeAnnouncements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "NotifyBadges",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"UserSettings\" DROP COLUMN \"NotifyBadges\";");
    }
}
