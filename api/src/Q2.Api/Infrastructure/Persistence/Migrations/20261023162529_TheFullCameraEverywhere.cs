using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class TheFullCameraEverywhere : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Every challenge contribution, chat photograph and logged use before
        // #152 is a single photograph: "Photo" is the default for the rows
        // already there.
        migrationBuilder.AddColumn<Guid>(
            name: "InsetImageId",
            table: "DontUses",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InsetMedia",
            table: "DontUses",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetPosterImageId",
            table: "DontUses",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Media",
            table: "DontUses",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Photo");

        migrationBuilder.AddColumn<Guid>(
            name: "PosterImageId",
            table: "DontUses",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetImageId",
            table: "ChatMessages",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InsetMedia",
            table: "ChatMessages",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetPosterImageId",
            table: "ChatMessages",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Media",
            table: "ChatMessages",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Photo");

        migrationBuilder.AddColumn<Guid>(
            name: "PosterImageId",
            table: "ChatMessages",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetImageId",
            table: "ChallengeEntries",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InsetMedia",
            table: "ChallengeEntries",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InsetPosterImageId",
            table: "ChallengeEntries",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Media",
            table: "ChallengeEntries",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Photo");

        migrationBuilder.AddColumn<Guid>(
            name: "PosterImageId",
            table: "ChallengeEntries",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_InsetImageId",
            table: "ChatMessages",
            column: "InsetImageId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_InsetPosterImageId",
            table: "ChatMessages",
            column: "InsetPosterImageId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_PosterImageId",
            table: "ChatMessages",
            column: "PosterImageId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ChatMessages_InsetImageId",
            table: "ChatMessages");

        migrationBuilder.DropIndex(
            name: "IX_ChatMessages_InsetPosterImageId",
            table: "ChatMessages");

        migrationBuilder.DropIndex(
            name: "IX_ChatMessages_PosterImageId",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "InsetImageId",
            table: "DontUses");

        migrationBuilder.DropColumn(
            name: "InsetMedia",
            table: "DontUses");

        migrationBuilder.DropColumn(
            name: "InsetPosterImageId",
            table: "DontUses");

        migrationBuilder.DropColumn(
            name: "Media",
            table: "DontUses");

        migrationBuilder.DropColumn(
            name: "PosterImageId",
            table: "DontUses");

        migrationBuilder.DropColumn(
            name: "InsetImageId",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "InsetMedia",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "InsetPosterImageId",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "Media",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "PosterImageId",
            table: "ChatMessages");

        migrationBuilder.DropColumn(
            name: "InsetImageId",
            table: "ChallengeEntries");

        migrationBuilder.DropColumn(
            name: "InsetMedia",
            table: "ChallengeEntries");

        migrationBuilder.DropColumn(
            name: "InsetPosterImageId",
            table: "ChallengeEntries");

        migrationBuilder.DropColumn(
            name: "Media",
            table: "ChallengeEntries");

        migrationBuilder.DropColumn(
            name: "PosterImageId",
            table: "ChallengeEntries");
    }
}
