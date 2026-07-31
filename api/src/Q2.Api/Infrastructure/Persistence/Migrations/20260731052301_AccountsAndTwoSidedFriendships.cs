using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AccountsAndTwoSidedFriendships : Migration
{
    /*
     * Accounts arrive, and with them a second person who can sign in.
     *
     * The order below is not the one EF scaffolded. People.IsCurrentUser is
     * the only thing in the old schema that says who "you" were, so goals,
     * tasks and friendships are converted while that flag still exists.
     *
     * SQLite table changes are also explicit. EF's generated table rebuilds
     * turn foreign keys off outside the migration transaction, which can leave
     * a partly converted database after a failure. Create/copy/drop/rename
     * keeps this migration atomic and keeps foreign-key enforcement enabled.
     *
     * Suggested friendships are deleted rather than guessed because they are
     * now derived from the graph. Accounts cannot be created here: a migration
     * must not invent password hashes. See README.md section 6.
     */
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RebuildGoalsWithOwners(migrationBuilder);
        RebuildTwoSidedFriendships(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "IX_People_IsCurrentUser",
            table: "People");

        // SQLite supports dropping this ordinary column transactionally. EF's
        // generic SQLite operation would instead schedule a table rebuild.
        migrationBuilder.Sql("ALTER TABLE \"People\" DROP COLUMN \"IsCurrentUser\";");

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUsers_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_PersonId",
            table: "AspNetUsers",
            column: "PersonId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true);
    }

    /// <summary>
    /// Restores the old shape, not the old data: which person was "you" is
    /// gone once the flag is dropped, so every restored <c>IsCurrentUser</c>
    /// is false and friendship directions stay as they are.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.AddColumn<bool>(
            name: "IsCurrentUser",
            table: "People",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        RebuildOneSidedFriendships(migrationBuilder);
        RebuildGoalsWithoutOwners(migrationBuilder);

        migrationBuilder.CreateIndex(
            name: "IX_People_IsCurrentUser",
            table: "People",
            column: "IsCurrentUser");
    }

    private static void RebuildGoalsWithOwners(MigrationBuilder migrationBuilder)
    {
        BackupGoalDependents(migrationBuilder, includeOwner: false);
        DropGoalDependents(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "__q2_Goals_with_owner",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CompletedSteps = table.Column<int>(type: "INTEGER", nullable: false),
                TotalSteps = table.Column<int>(type: "INTEGER", nullable: false),
                Icon = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                IsGroup = table.Column<bool>(type: "INTEGER", nullable: false),
                ReminderAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                Rhythm = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Goals", x => x.Id);
                table.ForeignKey(
                    name: "FK_Goals_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO "__q2_Goals_with_owner"
                ("Id", "Title", "Description", "Status", "CompletedSteps", "TotalSteps", "Icon", "IsGroup",
                 "ReminderAt", "Rhythm", "CreatedAt", "TargetDate", "OwnerPersonId")
            SELECT "Id", "Title", "Description", "Status", "CompletedSteps", "TotalSteps", "Icon", "IsGroup",
                   "ReminderAt", "Rhythm", "CreatedAt", "TargetDate",
                   (SELECT "Id" FROM "People" WHERE "IsCurrentUser" = 1)
            FROM "Goals";
            """);

        migrationBuilder.DropTable(
            name: "Goals");

        migrationBuilder.RenameTable(
            name: "__q2_Goals_with_owner",
            newName: "Goals");

        CreateGoalIndexes(migrationBuilder, includeOwner: true);
        RestoreConversationGoalLinks(migrationBuilder);
        RestoreGoalDependents(migrationBuilder, includeOwner: true);
    }

    private static void RebuildGoalsWithoutOwners(MigrationBuilder migrationBuilder)
    {
        BackupGoalDependents(migrationBuilder, includeOwner: true);
        DropGoalDependents(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "__q2_Goals_without_owner",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CompletedSteps = table.Column<int>(type: "INTEGER", nullable: false),
                TotalSteps = table.Column<int>(type: "INTEGER", nullable: false),
                Icon = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                IsGroup = table.Column<bool>(type: "INTEGER", nullable: false),
                ReminderAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                Rhythm = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Goals", x => x.Id);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO "__q2_Goals_without_owner"
                ("Id", "Title", "Description", "Status", "CompletedSteps", "TotalSteps", "Icon", "IsGroup",
                 "ReminderAt", "Rhythm", "CreatedAt", "TargetDate")
            SELECT "Id", "Title", "Description", "Status", "CompletedSteps", "TotalSteps", "Icon", "IsGroup",
                   "ReminderAt", "Rhythm", "CreatedAt", "TargetDate"
            FROM "Goals";
            """);

        migrationBuilder.DropTable(
            name: "Goals");

        migrationBuilder.RenameTable(
            name: "__q2_Goals_without_owner",
            newName: "Goals");

        CreateGoalIndexes(migrationBuilder, includeOwner: false);
        RestoreConversationGoalLinks(migrationBuilder);
        RestoreGoalDependents(migrationBuilder, includeOwner: false);
    }

    private static void BackupGoalDependents(MigrationBuilder migrationBuilder, bool includeOwner)
    {
        var taskOwnerColumn = includeOwner ? ", \"OwnerPersonId\"" : string.Empty;

        migrationBuilder.Sql(
            $$"""
            CREATE TABLE "__q2_GoalParticipants_backup" AS
            SELECT "Id", "GoalId", "PersonId" FROM "GoalParticipants";

            CREATE TABLE "__q2_GoalContributions_backup" AS
            SELECT "Id", "GoalId", "Date" FROM "GoalContributions";

            CREATE TABLE "__q2_GoalTasks_backup" AS
            SELECT "Id", "GoalId", "Title", "Rhythm", "ReminderAt", "WeeklyOn", "DueOn", "CompletedOn",
                   "MeasuredValue", "TargetValue", "MeasureUnit", "SortOrder", "CreatedAt"{{taskOwnerColumn}}
            FROM "GoalTasks";

            CREATE TABLE "__q2_ConversationGoals_backup" AS
            SELECT "Id", "GoalId" FROM "Conversations" WHERE "GoalId" IS NOT NULL;
            """);
    }

    private static void DropGoalDependents(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GoalParticipants");

        migrationBuilder.DropTable(
            name: "GoalContributions");

        migrationBuilder.DropTable(
            name: "GoalTasks");
    }

    private static void RestoreConversationGoalLinks(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Conversations"
            SET "GoalId" = (
                SELECT "GoalId"
                FROM "__q2_ConversationGoals_backup"
                WHERE "__q2_ConversationGoals_backup"."Id" = "Conversations"."Id"
            )
            WHERE "Id" IN (SELECT "Id" FROM "__q2_ConversationGoals_backup");

            DROP TABLE "__q2_ConversationGoals_backup";
            """);
    }

    private static void RestoreGoalDependents(MigrationBuilder migrationBuilder, bool includeOwner)
    {
        migrationBuilder.CreateTable(
            name: "GoalParticipants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalParticipants_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GoalParticipants_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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

        if (includeOwner)
        {
            CreateGoalTasksWithOwners(migrationBuilder);
        }
        else
        {
            CreateGoalTasksWithoutOwners(migrationBuilder);
        }

        migrationBuilder.Sql(
            """
            INSERT INTO "GoalParticipants" ("Id", "GoalId", "PersonId")
            SELECT "Id", "GoalId", "PersonId" FROM "__q2_GoalParticipants_backup";

            INSERT INTO "GoalContributions" ("Id", "GoalId", "Date")
            SELECT "Id", "GoalId", "Date" FROM "__q2_GoalContributions_backup";

            DROP TABLE "__q2_GoalParticipants_backup";
            DROP TABLE "__q2_GoalContributions_backup";
            """);

        var taskOwnerTarget = includeOwner ? ", \"OwnerPersonId\"" : string.Empty;
        var taskOwnerSource = includeOwner
            ? ", (SELECT \"Id\" FROM \"People\" WHERE \"IsCurrentUser\" = 1)"
            : string.Empty;

        migrationBuilder.Sql(
            $$"""
            INSERT INTO "GoalTasks"
                ("Id", "GoalId", "Title", "Rhythm", "ReminderAt", "WeeklyOn", "DueOn", "CompletedOn",
                 "MeasuredValue", "TargetValue", "MeasureUnit", "SortOrder", "CreatedAt"{{taskOwnerTarget}})
            SELECT "Id", "GoalId", "Title", "Rhythm", "ReminderAt", "WeeklyOn", "DueOn", "CompletedOn",
                   "MeasuredValue", "TargetValue", "MeasureUnit", "SortOrder", "CreatedAt"{{taskOwnerSource}}
            FROM "__q2_GoalTasks_backup";

            DROP TABLE "__q2_GoalTasks_backup";
            """);

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
            name: "IX_GoalContributions_GoalId_Date",
            table: "GoalContributions",
            columns: new[] { "GoalId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalTasks_GoalId",
            table: "GoalTasks",
            column: "GoalId");

        if (includeOwner)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_OwnerPersonId",
                table: "GoalTasks",
                column: "OwnerPersonId");
        }

        migrationBuilder.CreateIndex(
            name: "IX_GoalTasks_SortOrder",
            table: "GoalTasks",
            column: "SortOrder");
    }

    private static void CreateGoalTasksWithOwners(MigrationBuilder migrationBuilder)
    {
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
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                table.ForeignKey(
                    name: "FK_GoalTasks_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    private static void CreateGoalTasksWithoutOwners(MigrationBuilder migrationBuilder)
    {
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
    }

    private static void CreateGoalIndexes(MigrationBuilder migrationBuilder, bool includeOwner)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Goals_CreatedAt",
            table: "Goals",
            column: "CreatedAt");

        if (includeOwner)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerPersonId",
                table: "Goals",
                column: "OwnerPersonId");
        }

        migrationBuilder.CreateIndex(
            name: "IX_Goals_Status",
            table: "Goals",
            column: "Status");
    }

    private static void RebuildTwoSidedFriendships(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "__q2_Friendships_two_sided",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                AddresseeId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                RespondedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Friendships", x => x.Id);
                table.ForeignKey(
                    name: "FK_Friendships_People_AddresseeId",
                    column: x => x.AddresseeId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Friendships_People_RequesterId",
                    column: x => x.RequesterId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO "__q2_Friendships_two_sided"
                ("Id", "RequesterId", "AddresseeId", "Status", "RequestedAt", "RespondedAt")
            SELECT "Id",
                   CASE
                       WHEN "Status" = 'Invited'
                           THEN (SELECT "Id" FROM "People" WHERE "IsCurrentUser" = 1)
                       ELSE "PersonId"
                   END,
                   CASE
                       WHEN "Status" = 'Invited' THEN "PersonId"
                       WHEN "Status" IN ('Requested', 'Accepted')
                           THEN (SELECT "Id" FROM "People" WHERE "IsCurrentUser" = 1)
                       ELSE '00000000-0000-0000-0000-000000000000'
                   END,
                   CASE WHEN "Status" IN ('Requested', 'Invited') THEN 'Pending' ELSE "Status" END,
                   '0001-01-01 00:00:00',
                   NULL
            FROM "Friendships"
            WHERE "Status" <> 'Suggested';
            """);

        migrationBuilder.DropTable(
            name: "Friendships");

        migrationBuilder.RenameTable(
            name: "__q2_Friendships_two_sided",
            newName: "Friendships");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_AddresseeId",
            table: "Friendships",
            column: "AddresseeId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_RequesterId_AddresseeId",
            table: "Friendships",
            columns: new[] { "RequesterId", "AddresseeId" },
            unique: true);
    }

    private static void RebuildOneSidedFriendships(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "__q2_Friendships_one_sided",
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

        migrationBuilder.Sql(
            """
            INSERT INTO "__q2_Friendships_one_sided" ("Id", "PersonId", "Status", "MutualFriends")
            SELECT "Id", "RequesterId", "Status", 0 FROM "Friendships";
            """);

        migrationBuilder.DropTable(
            name: "Friendships");

        migrationBuilder.RenameTable(
            name: "__q2_Friendships_one_sided",
            newName: "Friendships");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_PersonId",
            table: "Friendships",
            column: "PersonId",
            unique: true);
    }
}
