using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds spot checks on Don'ts (#63) and the switch that lets somebody be
/// interrupted for them.
/// </summary>
/// <remarks>
/// The switch defaults to on for every existing account, like every other
/// switch: a check nobody hears about is a check that is always missed
/// (docs/adr/0038-spot-checks.md).
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class SpotChecks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "NotifySpotChecks",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            name: "SpotChecks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                ProofPhotoId = table.Column<Guid>(type: "TEXT", nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpotChecks", x => x.Id);
                table.ForeignKey(
                    name: "FK_SpotChecks_GoalInstances_GoalInstanceId",
                    column: x => x.GoalInstanceId,
                    principalTable: "GoalInstances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SpotChecks_GoalInstanceId_Day",
            table: "SpotChecks",
            columns: new[] { "GoalInstanceId", "Day" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SpotChecks_Status_ScheduledAt",
            table: "SpotChecks",
            columns: new[] { "Status", "ScheduledAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SpotChecks");

        migrationBuilder.Sql("ALTER TABLE \"UserSettings\" DROP COLUMN \"NotifySpotChecks\";");
    }
}
