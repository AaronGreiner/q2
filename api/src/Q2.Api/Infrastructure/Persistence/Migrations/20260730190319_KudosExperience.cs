using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class KudosExperience : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing goals are removed rather than converted, and this is the
        // one hand-written part of this migration.
        //
        // Two things make a conversion impossible rather than merely
        // tedious. A goal used to store a percentage and now stores steps,
        // and there is no honest way to turn "45%" into "how many of how
        // many" — the scaffolded RenameColumn would have made it a goal
        // needing 45 steps with none of them done. A participant used to be
        // a name typed into a box and is now a reference to a Person; there
        // is nobody for those names to point at, and the zero GUID the
        // scaffolder chose as a default would violate the new foreign key
        // on the first row.
        //
        // The alternative — rewriting InitialSchema so this history never
        // existed — would silently break any database that has already
        // applied it. Deleting the rows is the smaller loss, and it says so
        // out loud. See docs/adr/0009-single-known-person.md.
        migrationBuilder.Sql("DELETE FROM GoalParticipants;");
        migrationBuilder.Sql("DELETE FROM Goals;");

        migrationBuilder.DropIndex(
            name: "IX_GoalParticipants_GoalId_DisplayName",
            table: "GoalParticipants");

        migrationBuilder.DropColumn(
            name: "DisplayName",
            table: "GoalParticipants");

        migrationBuilder.DropColumn(
            name: "ProgressPercent",
            table: "Goals");

        migrationBuilder.AddColumn<int>(
            name: "TotalSteps",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "CompletedSteps",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Icon",
            table: "Goals",
            type: "TEXT",
            maxLength: 40,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsGroup",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "ReminderAt",
            table: "Goals",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Rhythm",
            table: "Goals",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "PersonId",
            table: "GoalParticipants",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                Emoji = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", x => x.Id);
                table.ForeignKey(
                    name: "FK_Conversations_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "GoalContributions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalContributions", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalContributions_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GoalTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Rhythm = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ReminderAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                WeeklyOn = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                DueOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CompletedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                MeasuredValue = table.Column<double>(type: "REAL", nullable: true),
                TargetValue = table.Column<double>(type: "REAL", nullable: true),
                MeasureUnit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalTasks_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "People",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Handle = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Initials = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                AvatarColor = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                IsCurrentUser = table.Column<bool>(type: "INTEGER", nullable: false),
                KudosReceived = table.Column<int>(type: "INTEGER", nullable: false),
                GoalsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_People", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ActivityEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ActorPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Subject = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                Amount = table.Column<int>(type: "INTEGER", nullable: true),
                KudosCount = table.Column<int>(type: "INTEGER", nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ActivityEvents_People_ActorPersonId",
                    column: x => x.ActorPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                SenderPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChatMessages_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ChatMessages_People_SenderPersonId",
                    column: x => x.SenderPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ConversationParticipants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                LastReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationParticipants_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ConversationParticipants_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DailyCheckIns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyCheckIns", x => x.Id);
                table.ForeignKey(
                    name: "FK_DailyCheckIns_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Friendships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                MutualFriends = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Friendships", x => x.Id);
                table.ForeignKey(
                    name: "FK_Friendships_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonBadges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Badge = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                EarnedOn = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonBadges", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonBadges_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Theme = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                NotifyReminders = table.Column<bool>(type: "INTEGER", nullable: false),
                NotifyKudos = table.Column<bool>(type: "INTEGER", nullable: false),
                NotifyMessages = table.Column<bool>(type: "INTEGER", nullable: false),
                NotifyWeeklyReview = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSettings_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ActivityKudos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityKudos", x => x.Id);
                table.ForeignKey(
                    name: "FK_ActivityKudos_ActivityEvents_ActivityEventId",
                    column: x => x.ActivityEventId,
                    principalTable: "ActivityEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ActivityKudos_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MessageReactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                Emoji = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageReactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_MessageReactions_ChatMessages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "ChatMessages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MessageReactions_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_GoalId_PersonId",
            table: "GoalParticipants",
            columns: new[] { "GoalId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_PersonId",
            table: "GoalParticipants",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityEvents_ActorPersonId",
            table: "ActivityEvents",
            column: "ActorPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityEvents_OccurredAt",
            table: "ActivityEvents",
            column: "OccurredAt");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityEvents_SourceId",
            table: "ActivityEvents",
            column: "SourceId");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityKudos_ActivityEventId_PersonId",
            table: "ActivityKudos",
            columns: new[] { "ActivityEventId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ActivityKudos_PersonId",
            table: "ActivityKudos",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ConversationId_SentAt",
            table: "ChatMessages",
            columns: new[] { "ConversationId", "SentAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_SenderPersonId",
            table: "ChatMessages",
            column: "SenderPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_ConversationId_PersonId",
            table: "ConversationParticipants",
            columns: new[] { "ConversationId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_PersonId",
            table: "ConversationParticipants",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_GoalId",
            table: "Conversations",
            column: "GoalId");

        migrationBuilder.CreateIndex(
            name: "IX_DailyCheckIns_PersonId_Date",
            table: "DailyCheckIns",
            columns: new[] { "PersonId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_PersonId",
            table: "Friendships",
            column: "PersonId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalContributions_GoalId_Date",
            table: "GoalContributions",
            columns: new[] { "GoalId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalTasks_GoalId",
            table: "GoalTasks",
            column: "GoalId");

        migrationBuilder.CreateIndex(
            name: "IX_GoalTasks_SortOrder",
            table: "GoalTasks",
            column: "SortOrder");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId_PersonId_Emoji",
            table: "MessageReactions",
            columns: new[] { "MessageId", "PersonId", "Emoji" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_PersonId",
            table: "MessageReactions",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_People_Handle",
            table: "People",
            column: "Handle",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_People_IsCurrentUser",
            table: "People",
            column: "IsCurrentUser");

        migrationBuilder.CreateIndex(
            name: "IX_PersonBadges_PersonId_Badge",
            table: "PersonBadges",
            columns: new[] { "PersonId", "Badge" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSettings_PersonId",
            table: "UserSettings",
            column: "PersonId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_GoalParticipants_People_PersonId",
            table: "GoalParticipants",
            column: "PersonId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_GoalParticipants_People_PersonId",
            table: "GoalParticipants");

        migrationBuilder.DropTable(
            name: "ActivityKudos");

        migrationBuilder.DropTable(
            name: "ConversationParticipants");

        migrationBuilder.DropTable(
            name: "DailyCheckIns");

        migrationBuilder.DropTable(
            name: "Friendships");

        migrationBuilder.DropTable(
            name: "GoalContributions");

        migrationBuilder.DropTable(
            name: "GoalTasks");

        migrationBuilder.DropTable(
            name: "MessageReactions");

        migrationBuilder.DropTable(
            name: "PersonBadges");

        migrationBuilder.DropTable(
            name: "UserSettings");

        migrationBuilder.DropTable(
            name: "ActivityEvents");

        migrationBuilder.DropTable(
            name: "ChatMessages");

        migrationBuilder.DropTable(
            name: "Conversations");

        migrationBuilder.DropTable(
            name: "People");

        migrationBuilder.DropIndex(
            name: "IX_GoalParticipants_GoalId_PersonId",
            table: "GoalParticipants");

        migrationBuilder.DropIndex(
            name: "IX_GoalParticipants_PersonId",
            table: "GoalParticipants");

        migrationBuilder.DropColumn(
            name: "CompletedSteps",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "Icon",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "IsGroup",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "ReminderAt",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "Rhythm",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "PersonId",
            table: "GoalParticipants");

        // Mirrors the delete in Up(): going back leaves the goal tables
        // structurally correct and empty, rather than full of rows whose
        // participants point at a table that no longer exists.
        migrationBuilder.Sql("DELETE FROM GoalParticipants;");
        migrationBuilder.Sql("DELETE FROM Goals;");

        migrationBuilder.DropColumn(
            name: "TotalSteps",
            table: "Goals");

        migrationBuilder.AddColumn<int>(
            name: "ProgressPercent",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "GoalParticipants",
            type: "TEXT",
            maxLength: 80,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_GoalId_DisplayName",
            table: "GoalParticipants",
            columns: new[] { "GoalId", "DisplayName" },
            unique: true);
    }
}
