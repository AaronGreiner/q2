using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InviteCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InviteCode",
            table: "People",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_People_InviteCode",
            table: "People",
            column: "InviteCode",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_People_InviteCode",
            table: "People");

        migrationBuilder.DropColumn(
            name: "InviteCode",
            table: "People");
    }
}
