using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ChatPhotos : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ImageId",
            table: "ChatMessages",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ImageId",
            table: "ChatMessages",
            column: "ImageId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ChatMessages_ImageId",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "ImageId",
            table: "ChatMessages");
    }
}
