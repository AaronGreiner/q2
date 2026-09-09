using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class BlocksAndReports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Blocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                BlockerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                BlockedPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Blocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_Blocks_People_BlockedPersonId",
                    column: x => x.BlockedPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Blocks_People_BlockerPersonId",
                    column: x => x.BlockerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Reports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ReporterPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reports", x => x.Id);
                table.ForeignKey(
                    name: "FK_Reports_People_ReporterPersonId",
                    column: x => x.ReporterPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Blocks_BlockedPersonId",
            table: "Blocks",
            column: "BlockedPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Blocks_BlockerPersonId_BlockedPersonId",
            table: "Blocks",
            columns: new[] { "BlockerPersonId", "BlockedPersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reports_ReporterPersonId_TargetKind_TargetId",
            table: "Reports",
            columns: new[] { "ReporterPersonId", "TargetKind", "TargetId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reports_TargetKind_TargetId",
            table: "Reports",
            columns: new[] { "TargetKind", "TargetId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Blocks");

        migrationBuilder.DropTable(
            name: "Reports");
    }
}
