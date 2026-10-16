using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the first-run introduction (#93): when somebody finished it, and the
/// Do somebody without friends can leave waiting.
/// </summary>
/// <remarks>
/// <c>OnboardedAt</c> starts empty for every existing account, deliberately:
/// the author decided that everybody goes through the introduction once
/// (docs/adr/0039-first-run-onboarding.md). Seeds mark their people as done.
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class Onboarding : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "OnboardedAt",
            table: "People",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "GoalDrafts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Icon = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                ScheduleKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ScheduleEveryDays = table.Column<int>(type: "INTEGER", nullable: true),
                ScheduleWeekdays = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ScheduleTimes = table.Column<int>(type: "INTEGER", nullable: true),
                SchedulePeriod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                ReminderAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalDrafts", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalDrafts_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalDrafts_OwnerPersonId",
            table: "GoalDrafts",
            column: "OwnerPersonId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GoalDrafts");

        migrationBuilder.Sql("ALTER TABLE \"People\" DROP COLUMN \"OnboardedAt\";");
    }
}
