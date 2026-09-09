using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProofsAndVotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProofPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                UploaderPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                ImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Attempt = table.Column<int>(type: "INTEGER", nullable: false),
                VotingDeadline = table.Column<DateTime>(type: "TEXT", nullable: false),
                CapturedInApp = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProofPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProofPhotos_GoalInstances_GoalInstanceId",
                    column: x => x.GoalInstanceId,
                    principalTable: "GoalInstances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProofReactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProofPhotoId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProofReactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProofReactions_ProofPhotos_ProofPhotoId",
                    column: x => x.ProofPhotoId,
                    principalTable: "ProofPhotos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProofVotes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProofPhotoId = table.Column<Guid>(type: "TEXT", nullable: false),
                VoterPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                CastAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProofVotes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProofVotes_ProofPhotos_ProofPhotoId",
                    column: x => x.ProofPhotoId,
                    principalTable: "ProofPhotos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProofPhotos_GoalInstanceId",
            table: "ProofPhotos",
            column: "GoalInstanceId");

        migrationBuilder.CreateIndex(
            name: "IX_ProofPhotos_Status_VotingDeadline",
            table: "ProofPhotos",
            columns: new[] { "Status", "VotingDeadline" });

        migrationBuilder.CreateIndex(
            name: "IX_ProofReactions_ProofPhotoId_PersonId",
            table: "ProofReactions",
            columns: new[] { "ProofPhotoId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProofVotes_ProofPhotoId_VoterPersonId",
            table: "ProofVotes",
            columns: new[] { "ProofPhotoId", "VoterPersonId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProofReactions");

        migrationBuilder.DropTable(
            name: "ProofVotes");

        migrationBuilder.DropTable(
            name: "ProofPhotos");
    }
}
