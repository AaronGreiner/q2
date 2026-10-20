using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// A goal's category replaces its free icon (#18).
/// </summary>
/// <remarks>
/// Every goal, waiting Do and recorded edit gets a category from the icon it
/// had: a medal or a trophy was sport, an open book was reading, and
/// everything else — a droplet, a sunrise, a flame — says nothing about what
/// the goal is, so it becomes "Sonstiges". The icon is derived from the
/// category from now on and is not stored.
///
/// The old columns are dropped with SQLite's own <c>ALTER TABLE … DROP
/// COLUMN</c> rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>). On the way down each category gets its own icon
/// back; which of several icons a goal had before is not recoverable.
/// </remarks>
public partial class GoalCategories : Migration
{
    private const string FromIcon =
        """
        CASE {0}
            WHEN 'medal' THEN 'Sport'
            WHEN 'trophy' THEN 'Sport'
            WHEN 'book-open' THEN 'Reading'
            ELSE 'Other'
        END
        """;

    private const string ToIcon =
        """
        CASE {0}
            WHEN 'Sport' THEN 'medal'
            WHEN 'Learning' THEN 'target'
            WHEN 'Reading' THEN 'book-open'
            WHEN 'Mindfulness' THEN 'hand-heart'
            ELSE 'target'
        END
        """;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "Goals",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Other");

        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "GoalDrafts",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "Other");

        migrationBuilder.AddColumn<string>(
            name: "CategoryFrom",
            table: "GoalChanges",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CategoryTo",
            table: "GoalChanges",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

        migrationBuilder.Sql($"UPDATE \"Goals\" SET \"Category\" = {string.Format(FromIcon, "\"Icon\"")};");
        migrationBuilder.Sql($"UPDATE \"GoalDrafts\" SET \"Category\" = {string.Format(FromIcon, "\"Icon\"")};");

        // An edit that only swapped one icon for another of the same category
        // keeps its row; it simply has nothing left to say about the category.
        migrationBuilder.Sql(
            $"""
            UPDATE "GoalChanges"
            SET "CategoryFrom" = {string.Format(FromIcon, "\"IconFrom\"")},
                "CategoryTo" = {string.Format(FromIcon, "\"IconTo\"")}
            WHERE "IconTo" IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "GoalChanges"
            SET "CategoryFrom" = NULL, "CategoryTo" = NULL
            WHERE "CategoryFrom" = "CategoryTo";
            """);

        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"Icon\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalDrafts\" DROP COLUMN \"Icon\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalChanges\" DROP COLUMN \"IconFrom\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalChanges\" DROP COLUMN \"IconTo\";");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Icon",
            table: "Goals",
            type: "TEXT",
            maxLength: 40,
            nullable: false,
            defaultValue: "target");

        migrationBuilder.AddColumn<string>(
            name: "Icon",
            table: "GoalDrafts",
            type: "TEXT",
            maxLength: 40,
            nullable: false,
            defaultValue: "target");

        migrationBuilder.AddColumn<string>(
            name: "IconFrom",
            table: "GoalChanges",
            type: "TEXT",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "IconTo",
            table: "GoalChanges",
            type: "TEXT",
            maxLength: 40,
            nullable: true);

        migrationBuilder.Sql($"UPDATE \"Goals\" SET \"Icon\" = {string.Format(ToIcon, "\"Category\"")};");
        migrationBuilder.Sql($"UPDATE \"GoalDrafts\" SET \"Icon\" = {string.Format(ToIcon, "\"Category\"")};");
        migrationBuilder.Sql(
            $"""
            UPDATE "GoalChanges"
            SET "IconFrom" = {string.Format(ToIcon, "\"CategoryFrom\"")},
                "IconTo" = {string.Format(ToIcon, "\"CategoryTo\"")}
            WHERE "CategoryTo" IS NOT NULL;
            """);

        migrationBuilder.Sql("ALTER TABLE \"Goals\" DROP COLUMN \"Category\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalDrafts\" DROP COLUMN \"Category\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalChanges\" DROP COLUMN \"CategoryFrom\";");
        migrationBuilder.Sql("ALTER TABLE \"GoalChanges\" DROP COLUMN \"CategoryTo\";");
    }
}
