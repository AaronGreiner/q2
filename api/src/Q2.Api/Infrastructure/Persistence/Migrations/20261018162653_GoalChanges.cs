using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class GoalChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GoalChanges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                TitleFrom = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                TitleTo = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                IconFrom = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                IconTo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                DescriptionChanged = table.Column<bool>(type: "INTEGER", nullable: false),
                DescriptionFrom = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                DescriptionTo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                ScheduleFromKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                ScheduleFromEveryDays = table.Column<int>(type: "INTEGER", nullable: true),
                ScheduleFromWeekdays = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                ScheduleFromTimes = table.Column<int>(type: "INTEGER", nullable: true),
                ScheduleFromPeriod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                ScheduleToKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                ScheduleToEveryDays = table.Column<int>(type: "INTEGER", nullable: true),
                ScheduleToWeekdays = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                ScheduleToTimes = table.Column<int>(type: "INTEGER", nullable: true),
                ScheduleToPeriod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                LimitFrom = table.Column<int>(type: "INTEGER", nullable: true),
                LimitTo = table.Column<int>(type: "INTEGER", nullable: true),
                ProofModeFrom = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                ProofModeTo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                TargetDateFrom = table.Column<DateOnly>(type: "TEXT", nullable: true),
                TargetDateTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Objectable = table.Column<bool>(type: "INTEGER", nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                AppliesOn = table.Column<DateOnly>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalChanges", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalChanges_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GoalChanges_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GoalChangeCheckers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalChangeId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Added = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalChangeCheckers", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalChangeCheckers_GoalChanges_GoalChangeId",
                    column: x => x.GoalChangeId,
                    principalTable: "GoalChanges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GoalChangeCheckers_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GoalChangeVetoes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalChangeId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalChangeVetoes", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalChangeVetoes_GoalChanges_GoalChangeId",
                    column: x => x.GoalChangeId,
                    principalTable: "GoalChanges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GoalChangeVetoes_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalChangeCheckers_GoalChangeId_PersonId",
            table: "GoalChangeCheckers",
            columns: new[] { "GoalChangeId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalChangeCheckers_PersonId",
            table: "GoalChangeCheckers",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_GoalChangeVetoes_GoalChangeId_PersonId",
            table: "GoalChangeVetoes",
            columns: new[] { "GoalChangeId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalChangeVetoes_PersonId",
            table: "GoalChangeVetoes",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_GoalChanges_GoalId_Status",
            table: "GoalChanges",
            columns: new[] { "GoalId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_GoalChanges_PersonId",
            table: "GoalChanges",
            column: "PersonId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GoalChangeCheckers");

        migrationBuilder.DropTable(
            name: "GoalChangeVetoes");

        migrationBuilder.DropTable(
            name: "GoalChanges");
    }
}
