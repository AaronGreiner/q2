using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// A goal's own conversation: at most one per goal
/// (docs/adr/0027-goal-conversations.md).
/// </summary>
/// <remarks>
/// Every conversation that pointed at a goal before this was a free chat
/// pinned to it, and a free chat is no longer about a goal — so the reference
/// is cleared first, which is also what lets the index be unique. The messages
/// stay where they were. The goals' own conversations are opened afterwards by
/// the maintenance pass, which is idempotent, rather than inserted here: a
/// migration changes the schema and never writes rows of its own.
///
/// The foreign key keeps SetNull. Changing it would make SQLite rebuild the
/// table with foreign keys switched off; the code removes a goal's
/// conversation with the goal instead.
/// </remarks>
public partial class GoalConversations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Conversations\" SET \"GoalId\" = NULL WHERE \"Kind\" <> 'Goal';");

        migrationBuilder.DropIndex(
            name: "IX_Conversations_GoalId",
            table: "Conversations");

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_GoalId",
            table: "Conversations",
            column: "GoalId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Conversations_GoalId",
            table: "Conversations");

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_GoalId",
            table: "Conversations",
            column: "GoalId");
    }
}
