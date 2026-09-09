using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PushSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "NotifyChallenge",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "QuietHoursFrom",
            table: "UserSettings",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "QuietHoursTo",
            table: "UserSettings",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "AnnouncedAt",
            table: "Challenges",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PushSubscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Endpoint = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                PublicKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                AuthSecret = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastDeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                ConsecutiveFailures = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_PushSubscriptions_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PushSubscriptions_Endpoint",
            table: "PushSubscriptions",
            column: "Endpoint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PushSubscriptions_PersonId",
            table: "PushSubscriptions",
            column: "PersonId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PushSubscriptions");

        migrationBuilder.DropColumn(
            name: "NotifyChallenge",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "QuietHoursFrom",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "QuietHoursTo",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "AnnouncedAt",
            table: "Challenges");
    }
}
