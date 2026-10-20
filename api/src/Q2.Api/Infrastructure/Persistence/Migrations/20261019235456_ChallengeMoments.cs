using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// A challenge appears at a moment of the day, and the first two hours after
/// it are "the moment" (#136).
/// </summary>
/// <remarks>
/// Rows already running or past keep midnight and no moment — nothing about
/// them changes after the fact. Rows still queued for later days, which nobody
/// has seen, been told about or answered, are taken out so the queue writes
/// them again with their moment on its next pass, a minute after start-up.
///
/// Down drops the column with SQLite's own <c>ALTER TABLE … DROP COLUMN</c>
/// rather than EF's rebuild, which would switch foreign keys off
/// (<c>MigrationTests</c>).
/// </remarks>
public partial class ChallengeMoments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "MomentEndsAt",
            table: "Challenges",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            """
            DELETE FROM "Challenges"
            WHERE "AnnouncedAt" IS NULL
              AND "PublishedAt" > datetime('now')
              AND "Id" NOT IN (SELECT "ChallengeId" FROM "ChallengeEntries");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Challenges\" DROP COLUMN \"MomentEndsAt\";");
    }
}
