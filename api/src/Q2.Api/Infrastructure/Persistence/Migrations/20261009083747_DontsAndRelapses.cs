using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds Don'ts (#62): a goal's kind, limit and proof mode, a window's limit,
/// and the uses its owner logs.
/// </summary>
/// <remarks>
/// Every existing goal becomes a Do, which is what each of them is, and its
/// windows keep a null limit. The new <c>Relapse</c> window status needs no
/// schema change: statuses are stored as text.
///
/// Down drops columns with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class DontsAndRelapses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "Goals",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Do");

        migrationBuilder.AddColumn<int>(
            name: "Limit",
            table: "Goals",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProofMode",
            table: "Goals",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Limit",
            table: "GoalInstances",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DontUses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                ImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DontUses", x => x.Id);
                table.ForeignKey(
                    name: "FK_DontUses_GoalInstances_GoalInstanceId",
                    column: x => x.GoalInstanceId,
                    principalTable: "GoalInstances",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DontUses_GoalInstanceId",
            table: "DontUses",
            column: "GoalInstanceId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DontUses");

        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"Kind\";");
        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"Limit\";");
        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"ProofMode\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalInstances\" DROP COLUMN \"Limit\";");
    }
}
