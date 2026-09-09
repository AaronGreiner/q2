using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PausesAndArchive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ClosedAt",
            table: "Goals",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "GoalPauses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 280, nullable: false),
                GoalInstanceId = table.Column<Guid>(type: "TEXT", nullable: true),
                StartsOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EndsOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalPauses", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalPauses_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GoalPauses_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PauseVetoes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalPauseId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PauseVetoes", x => x.Id);
                table.ForeignKey(
                    name: "FK_PauseVetoes_GoalPauses_GoalPauseId",
                    column: x => x.GoalPauseId,
                    principalTable: "GoalPauses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PauseVetoes_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalPauses_GoalId_Status",
            table: "GoalPauses",
            columns: new[] { "GoalId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_GoalPauses_PersonId",
            table: "GoalPauses",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PauseVetoes_GoalPauseId_PersonId",
            table: "PauseVetoes",
            columns: new[] { "GoalPauseId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PauseVetoes_PersonId",
            table: "PauseVetoes",
            column: "PersonId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PauseVetoes");

        migrationBuilder.DropTable(
            name: "GoalPauses");

        migrationBuilder.DropColumn(
            name: "ClosedAt",
            table: "Goals");
    }
}
