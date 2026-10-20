using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Deletes the feed's "ist bei … auf 1 %" lines (#90).
/// </summary>
/// <remarks>
/// <c>ActivityKind.GoalProgress</c> is gone: a goal has no percentage since
/// windows replaced steps (docs/adr/0016-windows-instead-of-steps.md), and
/// only the seeds ever wrote one. A row left behind could no longer be read
/// into the enum, so it goes rather than being hidden — with its kudos, by the
/// foreign key's cascade, and with the bell lines that pointed at it.
/// Nothing to restore on the way down: the rows said something untrue.
/// </remarks>
public partial class RetireGoalProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "Notifications"
            WHERE "Target" = 'Activity'
              AND "TargetId" IN (SELECT "Id" FROM "ActivityEvents" WHERE "Kind" = 'GoalProgress');
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM "ActivityKudos"
            WHERE "ActivityEventId" IN (SELECT "Id" FROM "ActivityEvents" WHERE "Kind" = 'GoalProgress');
            """);

        migrationBuilder.Sql("""DELETE FROM "ActivityEvents" WHERE "Kind" = 'GoalProgress';""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
