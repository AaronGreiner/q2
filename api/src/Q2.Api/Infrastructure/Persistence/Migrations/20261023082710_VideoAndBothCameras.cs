using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class VideoAndBothCameras : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "InsetImageId",
            table: "ProofPhotos",
            type: "TEXT",
            nullable: true);

        // Every proof delivered before #124 is a single photograph.
        migrationBuilder.AddColumn<string>(
            name: "Media",
            table: "ProofPhotos",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Photo");

        migrationBuilder.AddColumn<Guid>(
            name: "PosterImageId",
            table: "ProofPhotos",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DurationMilliseconds",
            table: "Images",
            type: "INTEGER",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InsetImageId",
            table: "ProofPhotos");

        migrationBuilder.DropColumn(
            name: "Media",
            table: "ProofPhotos");

        migrationBuilder.DropColumn(
            name: "PosterImageId",
            table: "ProofPhotos");

        migrationBuilder.DropColumn(
            name: "DurationMilliseconds",
            table: "Images");
    }
}
