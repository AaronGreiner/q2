using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Images : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AvatarImageId",
            table: "People",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Images",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Purpose = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ByteSize = table.Column<int>(type: "INTEGER", nullable: false),
                Width = table.Column<int>(type: "INTEGER", nullable: false),
                Height = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Images", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Images_OwnerPersonId",
            table: "Images",
            column: "OwnerPersonId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Images");

        migrationBuilder.DropColumn(
            name: "AvatarImageId",
            table: "People");
    }
}
