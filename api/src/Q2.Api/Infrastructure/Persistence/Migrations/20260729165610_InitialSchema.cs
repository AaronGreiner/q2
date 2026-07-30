using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Goals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ProgressPercent = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Goals", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GoalParticipants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalParticipants_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_GoalId_DisplayName",
            table: "GoalParticipants",
            columns: new[] { "GoalId", "DisplayName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Goals_CreatedAt",
            table: "Goals",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Goals_Status",
            table: "Goals",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GoalParticipants");

        migrationBuilder.DropTable(
            name: "Goals");
    }
}
