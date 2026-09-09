using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Q2.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Takes the emoji out of the two places the database stored one.
/// </summary>
/// <remarks>
/// The interface no longer uses emoji at all — an emoji is a picture with a
/// platform and a language behind it, drawn differently on every device,
/// with no accessible name we control and no way to style it. Both columns
/// therefore become names: a reaction is a <c>KudosKind</c>, a group's
/// avatar is an icon from <c>ConversationIcons</c>.
///
/// Written by hand rather than left as scaffolded. EF's own answer was to
/// drop each column and add the new one beside it, which is correct as
/// schema and wrong as a migration: every reaction anybody had ever given
/// would have been silently discarded. Renaming and then translating the
/// values keeps them.
///
/// The trailing catch-all updates are deliberate. Both old sets were closed
/// and validated at the boundary, so in a database this migration has ever
/// seen there is nothing left for them to match — but a value that survives
/// the translation would be read back into an enum that has no such member,
/// and one unreadable row would take down the whole chat screen rather than
/// itself.
/// </remarks>
public partial class QdosDesign : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MessageReactions_MessageId_PersonId_Emoji",
            table: "MessageReactions");

        migrationBuilder.RenameColumn(
            name: "Emoji",
            table: "MessageReactions",
            newName: "Kind");

        migrationBuilder.RenameColumn(
            name: "Emoji",
            table: "Conversations",
            newName: "Icon");

        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = 'Applause' WHERE Kind = '\U0001F44F';");
        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = 'Fire' WHERE Kind = '\U0001F525';");
        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = 'Strong' WHERE Kind = '❤️';");
        migrationBuilder.Sql("UPDATE MessageReactions SET Kind = 'Applause' WHERE Kind NOT IN ('Fire', 'Strong', 'Applause');");

        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'message-circle' WHERE Icon = '\U0001F4AC';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'sunrise' WHERE Icon = '\U0001F305';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'book-open' WHERE Icon = '\U0001F4DA';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'footprints' WHERE Icon = '\U0001F3C3';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'sprout' WHERE Icon = '\U0001F331';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'target' WHERE Icon = '\U0001F3AF';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'hand-heart' WHERE Icon = '\U0001F49A';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = 'party-popper' WHERE Icon = '\U0001F389';");
        migrationBuilder.Sql(
            "UPDATE Conversations SET Icon = 'message-circle' WHERE Icon IS NOT NULL AND Icon NOT IN ("
            + "'message-circle', 'sunrise', 'book-open', 'footprints', 'sprout', 'target', 'hand-heart', 'party-popper');");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId_PersonId_Kind",
            table: "MessageReactions",
            columns: new[] { "MessageId", "PersonId", "Kind" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MessageReactions_MessageId_PersonId_Kind",
            table: "MessageReactions");

        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = '\U0001F44F' WHERE Kind = 'Applause';");
        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = '\U0001F525' WHERE Kind = 'Fire';");
        migrationBuilder.Sql($"UPDATE MessageReactions SET Kind = '❤️' WHERE Kind = 'Strong';");

        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F4AC' WHERE Icon = 'message-circle';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F305' WHERE Icon = 'sunrise';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F4DA' WHERE Icon = 'book-open';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F3C3' WHERE Icon = 'footprints';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F331' WHERE Icon = 'sprout';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F3AF' WHERE Icon = 'target';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F49A' WHERE Icon = 'hand-heart';");
        migrationBuilder.Sql($"UPDATE Conversations SET Icon = '\U0001F389' WHERE Icon = 'party-popper';");

        migrationBuilder.RenameColumn(
            name: "Kind",
            table: "MessageReactions",
            newName: "Emoji");

        migrationBuilder.RenameColumn(
            name: "Icon",
            table: "Conversations",
            newName: "Emoji");

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId_PersonId_Emoji",
            table: "MessageReactions",
            columns: new[] { "MessageId", "PersonId", "Emoji" },
            unique: true);
    }
}
