using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// When a pause's friends were told about it, so one planned ahead can be told
/// when it begins (#99).
/// </summary>
/// <remarks>
/// Every pause before this one was announced the moment it was asked for, so
/// that is what they get.
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class PlannedPauses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "AnnouncedAt",
            table: "GoalPauses",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql("UPDATE \"GoalPauses\" SET \"AnnouncedAt\" = \"CreatedAt\";");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"GoalPauses\" DROP COLUMN \"AnnouncedAt\";");
    }
}
