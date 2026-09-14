using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// One notification pipeline: the bell, a switch per kind, muting, and the
/// evening warning moving out of the feed
/// (docs/adr/0024-one-notification-pipeline.md).
/// </summary>
/// <remarks>
/// Written by hand where EF guessed. It matched the old switches to the new
/// ones by position rather than by meaning — it would have turned somebody's
/// "no kudos" into "no verdicts" — so the renames here are the real ones:
///
/// - <c>NotifyKudos</c> becomes <c>NotifyReactions</c> and
///   <c>NotifyReminders</c> becomes <c>NotifyFriendsAtRisk</c>: the same switch
///   under an honest name, and everybody keeps what they chose.
/// - <c>NotifyWeeklyReview</c> is reused as <c>NotifyVotesDue</c> rather than
///   dropped. SQLite cannot drop a column without EF rebuilding the table with
///   foreign keys switched off outside the transaction (api/AGENTS.md §4, and
///   <c>MigrationTests</c> refuses it). What somebody chose for a weekly review
///   that never existed says nothing about votes, so every row is set to the new
///   switch's default: on.
/// - The three other new switches arrive on, which is what every switch is for
///   somebody who never opened the screen.
/// - The evening warning leaves the feed. Its rows are deleted, with any kudos
///   on them: they were about one evening, and keeping a record of somebody
///   nearly missing is exactly what "kein Nachtreten" rules out. From now on the
///   warning is a line in each friend's bell. Down cannot bring those rows back.
/// </remarks>
public partial class NotificationPipeline : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "NotifyKudos",
            table: "UserSettings",
            newName: "NotifyReactions");

        migrationBuilder.RenameColumn(
            name: "NotifyReminders",
            table: "UserSettings",
            newName: "NotifyFriendsAtRisk");

        migrationBuilder.RenameColumn(
            name: "NotifyWeeklyReview",
            table: "UserSettings",
            newName: "NotifyVotesDue");

        migrationBuilder.Sql("UPDATE \"UserSettings\" SET \"NotifyVotesDue\" = 1;");

        migrationBuilder.AddColumn<bool>(
            name: "NotifyFriendships",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "NotifyProofResults",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "NotifyGoalUpdates",
            table: "UserSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NotificationsSeenAt",
            table: "People",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "MutedAt",
            table: "ConversationParticipants",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            "DELETE FROM \"ActivityKudos\" WHERE \"ActivityEventId\" IN "
            + "(SELECT \"Id\" FROM \"ActivityEvents\" WHERE \"Kind\" = 'WindowAtRisk');");

        migrationBuilder.Sql("DELETE FROM \"ActivityEvents\" WHERE \"Kind\" = 'WindowAtRisk';");

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RecipientPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ActorPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                Subject = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                Amount = table.Column<int>(type: "INTEGER", nullable: true),
                Target = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_Notifications_People_ActorPersonId",
                    column: x => x.ActorPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Notifications_People_RecipientPersonId",
                    column: x => x.RecipientPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ActorPersonId",
            table: "Notifications",
            column: "ActorPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_OccurredAt",
            table: "Notifications",
            column: "OccurredAt");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_RecipientPersonId_OccurredAt",
            table: "Notifications",
            columns: new[] { "RecipientPersonId", "OccurredAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropColumn(
            name: "NotifyFriendships",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "NotifyProofResults",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "NotifyGoalUpdates",
            table: "UserSettings");

        migrationBuilder.DropColumn(
            name: "NotificationsSeenAt",
            table: "People");

        migrationBuilder.DropColumn(
            name: "MutedAt",
            table: "ConversationParticipants");

        migrationBuilder.RenameColumn(
            name: "NotifyVotesDue",
            table: "UserSettings",
            newName: "NotifyWeeklyReview");

        // The weekly review was off by default, and nobody chose it on in the
        // meantime: the switch did not exist.
        migrationBuilder.Sql("UPDATE \"UserSettings\" SET \"NotifyWeeklyReview\" = 0;");

        migrationBuilder.RenameColumn(
            name: "NotifyFriendsAtRisk",
            table: "UserSettings",
            newName: "NotifyReminders");

        migrationBuilder.RenameColumn(
            name: "NotifyReactions",
            table: "UserSettings",
            newName: "NotifyKudos");
    }
}
