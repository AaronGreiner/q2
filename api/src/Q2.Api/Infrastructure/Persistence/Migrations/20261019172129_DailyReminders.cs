using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// The switch for a Do's daily reminder, and the day a window last reminded
/// its owner (#82).
/// </summary>
/// <remarks>
/// The switch is on for everybody who already has settings, as every switch
/// is by default (<c>NotificationPreferences</c>): somebody who set up a
/// reminder has not said they do not want it.
///
/// Down drops the columns with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class DailyReminders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "NotifyReminders",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "RemindedOn",
            table: "GoalInstances",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"UserSettings\" DROP COLUMN \"NotifyReminders\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalInstances\" DROP COLUMN \"RemindedOn\";");
    }
}
