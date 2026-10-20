using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// The local time a Do's day ends at instead of midnight — "bis 07:00" (#98).
/// </summary>
/// <remarks>
/// Null for every goal there is: they all end at midnight, as they always did.
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class GoalDueBy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<TimeOnly>(
            name: "DueBy",
            table: "Goals",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"DueBy\";");
    }
}
