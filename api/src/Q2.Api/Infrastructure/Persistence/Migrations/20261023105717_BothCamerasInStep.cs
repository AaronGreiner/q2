using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class BothCamerasInStep : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InsetMedia",
            table: "ProofPhotos",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetPosterImageId",
            table: "ProofPhotos",
            type: "TEXT",
            nullable: true);

        // Every front picture delivered before #151 is a photograph.
        migrationBuilder.Sql("UPDATE \"ProofPhotos\" SET \"InsetMedia\" = 'Photo' WHERE \"InsetImageId\" IS NOT NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InsetMedia",
            table: "ProofPhotos");

        migrationBuilder.DropColumn(
            name: "InsetPosterImageId",
            table: "ProofPhotos");
    }
}
