using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class DailyChallenge : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Challenges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Prompt = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Challenges", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ChallengeEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ChallengeId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                ImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                CapturedInApp = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChallengeEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChallengeEntries_Challenges_ChallengeId",
                    column: x => x.ChallengeId,
                    principalTable: "Challenges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ChallengeEntries_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ChallengeReactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ChallengeEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChallengeReactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChallengeReactions_ChallengeEntries_ChallengeEntryId",
                    column: x => x.ChallengeEntryId,
                    principalTable: "ChallengeEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChallengeEntries_ChallengeId_PersonId",
            table: "ChallengeEntries",
            columns: new[] { "ChallengeId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChallengeEntries_PersonId_CreatedAt",
            table: "ChallengeEntries",
            columns: new[] { "PersonId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ChallengeReactions_ChallengeEntryId_PersonId",
            table: "ChallengeReactions",
            columns: new[] { "ChallengeEntryId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Challenges_Day",
            table: "Challenges",
            column: "Day",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Challenges_PublishedAt_ExpiresAt",
            table: "Challenges",
            columns: new[] { "PublishedAt", "ExpiresAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChallengeReactions");

        migrationBuilder.DropTable(
            name: "ChallengeEntries");

        migrationBuilder.DropTable(
            name: "Challenges");
    }
}
