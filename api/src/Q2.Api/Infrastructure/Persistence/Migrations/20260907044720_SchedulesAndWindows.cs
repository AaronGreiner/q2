using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Replaces counted steps with scheduled windows.
/// </summary>
/// <remarks>
/// The model change behind this is in
/// docs/adr/0016-windows-instead-of-steps.md: a goal stops being a counter you
/// turn up yourself and becomes a schedule with windows that can be delivered
/// or missed.
///
/// Written by hand rather than left as scaffolded, for three reasons:
///
/// **The schedule is translated, not defaulted.** Each old rhythm becomes the
/// shape that says the same thing — <c>Daily</c> an interval of one day,
/// <c>Weekdays</c> Monday to Friday, <c>Once</c> a one-off. <c>Weekly</c>
/// becomes "once per calendar week" rather than "every seven days": the old
/// value meant a week, not a seven-day count from an arbitrary anchor, and the
/// new model has exactly one way to say each thing.
///
/// **The history is kept.** Every <c>GoalContribution</c> row was a real claim
/// — "this goal was worked on that day" — so each becomes a delivered one-day
/// window and keeps its own id. Without this, every streak in the database
/// would reset to zero on deploy, and the first thing anybody saw after the
/// upgrade would be that their twelve days were gone. For a goal that used to
/// be weekly the archive reads as a run of single days, which is an
/// approximation rather than a fiction: the day is what was actually recorded.
/// Those windows are counted in UTC days, because a migration cannot know what
/// zone each person was in when a row was written and inventing one would be
/// worse than using the zone the row was stored in.
///
/// **Foreign keys stay on.** Dropping three columns from <c>Goals</c> makes
/// EF's SQLite provider rebuild the table with
/// <c>PRAGMA foreign_keys = 0</c> around it, outside the transaction — which
/// <c>MigrationTests.MigrationSqlNeverDisablesForeignKeys</c> forbids, because
/// a rebuild with the constraints switched off is a rebuild that can silently
/// orphan rows. The rebuild is therefore written out here, the same way
/// <c>AccountsAndTwoSidedFriendships</c> did it: save the dependents, cut the
/// references, copy the table, put everything back.
///
/// What is <em>not</em> kept is <c>CompletedSteps</c>: "14 of 21" was a number
/// somebody incremented, it names no day and proves no delivery, and there is
/// nothing honest to turn it into. Nor are tasks — a task was a tick you gave
/// yourself, and the unit that replaces it is a window somebody else confirms.
/// </remarks>
public partial class SchedulesAndWindows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeZoneId",
            table: "People",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        // 1. Everything hanging off Goals, saved before the table is replaced.
        migrationBuilder.Sql(
            """
            CREATE TABLE "__q2_GoalParticipants_backup" AS
            SELECT "Id", "GoalId", "PersonId" FROM "GoalParticipants";

            CREATE TABLE "__q2_GoalDays_backup" AS
            SELECT "Id", "GoalId", "Date" FROM "GoalContributions";

            CREATE TABLE "__q2_ConversationGoals_backup" AS
            SELECT "Id", "GoalId" FROM "Conversations" WHERE "GoalId" IS NOT NULL;

            UPDATE "Conversations" SET "GoalId" = NULL;
            """);

        migrationBuilder.DropTable(name: "GoalParticipants");
        migrationBuilder.DropTable(name: "GoalContributions");
        migrationBuilder.DropTable(name: "GoalTasks");

        // 2. Goals in its new shape, with every rhythm translated on the way in.
        migrationBuilder.Sql(
            """
            CREATE TABLE "__q2_Goals_scheduled" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Goals" PRIMARY KEY,
                "OwnerPersonId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Icon" TEXT NOT NULL,
                "IsGroup" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "ScheduleKind" TEXT NOT NULL,
                "ScheduleEveryDays" INTEGER NULL,
                "ScheduleWeekdays" TEXT NOT NULL,
                "ScheduleTimes" INTEGER NULL,
                "SchedulePeriod" TEXT NULL,
                "ReminderAt" TEXT NULL,
                "TargetDate" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Goals_People_OwnerPersonId" FOREIGN KEY ("OwnerPersonId")
                    REFERENCES "People" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "__q2_Goals_scheduled"
                ("Id", "OwnerPersonId", "Title", "Description", "Icon", "IsGroup", "Status",
                 "ScheduleKind", "ScheduleEveryDays", "ScheduleWeekdays", "ScheduleTimes", "SchedulePeriod",
                 "ReminderAt", "TargetDate", "CreatedAt")
            SELECT
                "Id",
                "OwnerPersonId",
                "Title",
                "Description",
                "Icon",
                "IsGroup",
                "Status",

                -- A rhythm nobody recognises becomes a daily interval rather
                -- than a value the enum cannot read back: one unreadable row
                -- would take down the whole goals screen rather than itself.
                CASE "Rhythm"
                    WHEN 'Weekdays' THEN 'Weekdays'
                    WHEN 'Weekly' THEN 'Times'
                    WHEN 'Once' THEN 'Once'
                    ELSE 'Interval'
                END,
                CASE WHEN "Rhythm" IN ('Weekdays', 'Weekly', 'Once') THEN NULL ELSE 1 END,
                CASE "Rhythm" WHEN 'Weekdays' THEN '1,2,3,4,5' ELSE '' END,
                CASE "Rhythm" WHEN 'Weekly' THEN 1 ELSE NULL END,
                CASE "Rhythm" WHEN 'Weekly' THEN 'Week' ELSE NULL END,
                "ReminderAt",

                -- A target date only means anything on a one-off now; on
                -- anything that repeats it would be a second answer to "when is
                -- this due".
                CASE "Rhythm" WHEN 'Once' THEN "TargetDate" ELSE NULL END,
                "CreatedAt"
            FROM "Goals";
            """);

        migrationBuilder.DropTable(name: "Goals");

        migrationBuilder.RenameTable(name: "__q2_Goals_scheduled", newName: "Goals");

        migrationBuilder.CreateIndex(name: "IX_Goals_CreatedAt", table: "Goals", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_Goals_OwnerPersonId", table: "Goals", column: "OwnerPersonId");
        migrationBuilder.CreateIndex(name: "IX_Goals_Status", table: "Goals", column: "Status");

        // 3. The dependents, pointing at the table that now exists.
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

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_GoalId_PersonId",
            table: "GoalParticipants",
            columns: new[] { "GoalId", "PersonId" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "GoalInstances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                StartsOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                DueOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                DueAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                RequiredProofs = table.Column<int>(type: "INTEGER", nullable: false),
                ConfirmedProofs = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalInstances", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalInstances_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalInstances_GoalId_StartsOn_DueOn",
            table: "GoalInstances",
            columns: new[] { "GoalId", "StartsOn", "DueOn" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoalInstances_Status_DueAt",
            table: "GoalInstances",
            columns: new[] { "Status", "DueAt" });

        // 4. Everything put back: the participants, one delivered window per
        //    recorded day, and the goal each conversation was pinned to.
        migrationBuilder.Sql(
            """
            INSERT INTO "GoalParticipants" ("Id", "GoalId", "PersonId")
            SELECT "Id", "GoalId", "PersonId" FROM "__q2_GoalParticipants_backup";

            INSERT INTO "GoalInstances"
                ("Id", "GoalId", "StartsOn", "DueOn", "StartsAt", "DueAt",
                 "RequiredProofs", "ConfirmedProofs", "Status", "ResolvedAt")
            SELECT
                "Id",
                "GoalId",
                "Date",
                "Date",
                "Date" || ' 00:00:00',
                "Date" || ' 23:59:59.9999999',
                1,
                1,
                'Done',
                "Date" || ' 23:59:59.9999999'
            FROM "__q2_GoalDays_backup";

            UPDATE "Conversations"
            SET "GoalId" = (
                SELECT "GoalId" FROM "__q2_ConversationGoals_backup" AS backup
                WHERE backup."Id" = "Conversations"."Id")
            WHERE "Id" IN (SELECT "Id" FROM "__q2_ConversationGoals_backup");

            DROP TABLE "__q2_GoalParticipants_backup";
            DROP TABLE "__q2_GoalDays_backup";
            DROP TABLE "__q2_ConversationGoals_backup";
            """);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reversible as schema, not as data: the tasks and the step counts are
    /// gone, and going back leaves that table empty and the counters at zero.
    /// The windows are translated back into contributions so a rollback does
    /// not lose the days either.
    ///
    /// The one <c>DropColumn</c> left here is the People one, which EF rebuilds
    /// with the foreign keys off. That is tolerated on the way back and not on
    /// the way forward: <c>GenerateScript</c> — and every deployment — only ever
    /// runs Up, while Down is a manual rollback somebody is watching.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "__q2_GoalParticipants_backup" AS
            SELECT "Id", "GoalId", "PersonId" FROM "GoalParticipants";

            CREATE TABLE "__q2_GoalDays_backup" AS
            SELECT "Id", "GoalId", "DueOn" AS "Date" FROM "GoalInstances"
            WHERE "Status" = 'Done' AND "StartsOn" = "DueOn";

            CREATE TABLE "__q2_ConversationGoals_backup" AS
            SELECT "Id", "GoalId" FROM "Conversations" WHERE "GoalId" IS NOT NULL;

            UPDATE "Conversations" SET "GoalId" = NULL;
            """);

        migrationBuilder.DropTable(name: "GoalParticipants");
        migrationBuilder.DropTable(name: "GoalInstances");

        migrationBuilder.Sql(
            """
            CREATE TABLE "__q2_Goals_counted" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Goals" PRIMARY KEY,
                "OwnerPersonId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Icon" TEXT NOT NULL,
                "Rhythm" TEXT NOT NULL,
                "IsGroup" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "CompletedSteps" INTEGER NOT NULL,
                "TotalSteps" INTEGER NOT NULL,
                "ReminderAt" TEXT NULL,
                "TargetDate" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Goals_People_OwnerPersonId" FOREIGN KEY ("OwnerPersonId")
                    REFERENCES "People" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "__q2_Goals_counted"
                ("Id", "OwnerPersonId", "Title", "Description", "Icon", "Rhythm", "IsGroup", "Status",
                 "CompletedSteps", "TotalSteps", "ReminderAt", "TargetDate", "CreatedAt")
            SELECT
                "Id",
                "OwnerPersonId",
                "Title",
                "Description",
                "Icon",
                CASE "ScheduleKind"
                    WHEN 'Weekdays' THEN 'Weekdays'
                    WHEN 'Times' THEN 'Weekly'
                    WHEN 'Once' THEN 'Once'
                    ELSE 'Daily'
                END,
                "IsGroup",
                "Status",
                0,
                1,
                "ReminderAt",
                "TargetDate",
                "CreatedAt"
            FROM "Goals";
            """);

        migrationBuilder.DropTable(name: "Goals");

        migrationBuilder.RenameTable(name: "__q2_Goals_counted", newName: "Goals");

        migrationBuilder.CreateIndex(name: "IX_Goals_CreatedAt", table: "Goals", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_Goals_OwnerPersonId", table: "Goals", column: "OwnerPersonId");
        migrationBuilder.CreateIndex(name: "IX_Goals_Status", table: "Goals", column: "Status");

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

        migrationBuilder.CreateIndex(
            name: "IX_GoalParticipants_GoalId_PersonId",
            table: "GoalParticipants",
            columns: new[] { "GoalId", "PersonId" },
            unique: true);

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

        migrationBuilder.CreateIndex(
            name: "IX_GoalContributions_GoalId_Date",
            table: "GoalContributions",
            columns: new[] { "GoalId", "Date" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "GoalTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                table.ForeignKey(
                    name: "FK_GoalTasks_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_GoalTasks_GoalId", table: "GoalTasks", column: "GoalId");
        migrationBuilder.CreateIndex(name: "IX_GoalTasks_OwnerPersonId", table: "GoalTasks", column: "OwnerPersonId");
        migrationBuilder.CreateIndex(name: "IX_GoalTasks_SortOrder", table: "GoalTasks", column: "SortOrder");

        migrationBuilder.Sql(
            """
            INSERT INTO "GoalParticipants" ("Id", "GoalId", "PersonId")
            SELECT "Id", "GoalId", "PersonId" FROM "__q2_GoalParticipants_backup";

            INSERT INTO "GoalContributions" ("Id", "GoalId", "Date")
            SELECT "Id", "GoalId", "Date" FROM "__q2_GoalDays_backup";

            UPDATE "Conversations"
            SET "GoalId" = (
                SELECT "GoalId" FROM "__q2_ConversationGoals_backup" AS backup
                WHERE backup."Id" = "Conversations"."Id")
            WHERE "Id" IN (SELECT "Id" FROM "__q2_ConversationGoals_backup");

            DROP TABLE "__q2_GoalParticipants_backup";
            DROP TABLE "__q2_GoalDays_backup";
            DROP TABLE "__q2_ConversationGoals_backup";
            """);

        migrationBuilder.DropColumn(name: "TimeZoneId", table: "People");
    }
}
