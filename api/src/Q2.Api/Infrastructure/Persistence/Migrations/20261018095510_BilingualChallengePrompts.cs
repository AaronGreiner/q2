using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gives the daily challenge its prompt in English as well as German (#105):
/// on the challenge, and on a kept notification whose subject is a prompt.
/// </summary>
/// <remarks>
/// Both columns are nullable and start empty for the rows that exist. A
/// challenge reads in German until the queue finds its prompt in the
/// configuration and fills in the English (<c>ChallengeQueueWorker</c>); a
/// notification written before this keeps the subject it was written with.
/// Nothing is inserted or rewritten here.
///
/// Down drops the columns with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>), as <c>Onboarding</c> does.
/// </remarks>
public partial class BilingualChallengePrompts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SubjectEnglish",
            table: "Notifications",
            type: "TEXT",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PromptEnglish",
            table: "Challenges",
            type: "TEXT",
            maxLength: 160,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Notifications\" DROP COLUMN \"SubjectEnglish\";");
        migrationBuilder.Sql("ALTER TABLE \"Challenges\" DROP COLUMN \"PromptEnglish\";");
    }
}
