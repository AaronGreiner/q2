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
     * The order below is not the one EF scaffolded. The generated version
     * dropped People.IsCurrentUser before touching anything else, and that
     * column is the only thing in the old schema that says who "you" were —
     * without it, every goal, task and friendship in an existing database
     * becomes unattributable. So: add the new columns first, convert the
     * data while the flag is still there, and only then drop it.
     *
     * What converts exactly:
     *   - every goal and task belonged to the one flagged person, so that
     *     is who they now belong to;
     *   - a Requested friendship was somebody asking *you*, an Invited one
     *     was you asking them, and Accepted is symmetric — all three map
     *     onto the requester/addressee pair without inventing anything.
     *
     * What does not, and is deleted rather than guessed:
     *   - Suggested rows. A suggestion is no longer stored at all; it is
     *     derived from the friend graph on every read.
     *
     * What this migration cannot do: create accounts. A password hash is
     * not something a migration may invent, so a database seeded before
     * this change keeps all its data and has nobody able to sign in to it.
     * For a local Development database the answer is to start over —
     * README.md section 6 has the one-line command.
     */
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OwnerPersonId",
            table: "Goals",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<Guid>(
            name: "OwnerPersonId",
            table: "GoalTasks",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.DropForeignKey(
            name: "FK_Friendships_People_PersonId",
            table: "Friendships");

        migrationBuilder.DropIndex(
            name: "IX_Friendships_PersonId",
            table: "Friendships");

        migrationBuilder.RenameColumn(
            name: "PersonId",
            table: "Friendships",
            newName: "RequesterId");

        migrationBuilder.AddColumn<Guid>(
            name: "AddresseeId",
            table: "Friendships",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        // Left at its default for converted rows on purpose: the old schema
        // never recorded when a request was made, and a made-up timestamp
        // would read as fact. They simply sort last.
        migrationBuilder.AddColumn<DateTime>(
            name: "RequestedAt",
            table: "Friendships",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<DateTime>(
            name: "RespondedAt",
            table: "Friendships",
            type: "TEXT",
            nullable: true);

        // --- data conversion, while IsCurrentUser still exists ---
        migrationBuilder.Sql(
            """
            UPDATE Goals
            SET OwnerPersonId = (SELECT Id FROM People WHERE IsCurrentUser = 1)
            WHERE EXISTS (SELECT 1 FROM People WHERE IsCurrentUser = 1);
            """);

        migrationBuilder.Sql(
            """
            UPDATE GoalTasks
            SET OwnerPersonId = (SELECT Id FROM People WHERE IsCurrentUser = 1)
            WHERE EXISTS (SELECT 1 FROM People WHERE IsCurrentUser = 1);
            """);

        migrationBuilder.Sql("DELETE FROM Friendships WHERE Status = 'Suggested';");

        // They asked you, or you are already friends: they are the
        // requester, you are the addressee.
        migrationBuilder.Sql(
            """
            UPDATE Friendships
            SET AddresseeId = (SELECT Id FROM People WHERE IsCurrentUser = 1)
            WHERE Status IN ('Requested', 'Accepted')
              AND EXISTS (SELECT 1 FROM People WHERE IsCurrentUser = 1);
            """);

        // You asked them, so the two ends swap. SQLite evaluates every SET
        // against the row as it was, which is what makes this a swap rather
        // than two assignments of the same value.
        migrationBuilder.Sql(
            """
            UPDATE Friendships
            SET AddresseeId = RequesterId,
                RequesterId = (SELECT Id FROM People WHERE IsCurrentUser = 1)
            WHERE Status = 'Invited'
              AND EXISTS (SELECT 1 FROM People WHERE IsCurrentUser = 1);
            """);

        migrationBuilder.Sql(
            "UPDATE Friendships SET Status = 'Pending' WHERE Status IN ('Requested', 'Invited');");

        // --- the old shape can go now ---
        migrationBuilder.DropIndex(
            name: "IX_People_IsCurrentUser",
            table: "People");

        migrationBuilder.DropColumn(
            name: "IsCurrentUser",
            table: "People");

        migrationBuilder.DropColumn(
            name: "MutualFriends",
            table: "Friendships");

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
            name: "IX_Goals_OwnerPersonId",
            table: "Goals",
            column: "OwnerPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_GoalTasks_OwnerPersonId",
            table: "GoalTasks",
            column: "OwnerPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_AddresseeId",
            table: "Friendships",
            column: "AddresseeId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_RequesterId_AddresseeId",
            table: "Friendships",
            columns: new[] { "RequesterId", "AddresseeId" },
            unique: true);

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

        migrationBuilder.AddForeignKey(
            name: "FK_Friendships_People_AddresseeId",
            table: "Friendships",
            column: "AddresseeId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Friendships_People_RequesterId",
            table: "Friendships",
            column: "RequesterId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_GoalTasks_People_OwnerPersonId",
            table: "GoalTasks",
            column: "OwnerPersonId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Goals_People_OwnerPersonId",
            table: "Goals",
            column: "OwnerPersonId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <summary>
    /// Restores the old shape, not the old data: which person was "you" is
    /// gone once the flag is dropped, so every restored <c>IsCurrentUser</c>
    /// is false and the friendship directions stay as they are. Down is
    /// here so a local branch can be stepped back, not as a supported way
    /// to run the previous version against converted data.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Friendships_People_AddresseeId",
            table: "Friendships");

        migrationBuilder.DropForeignKey(
            name: "FK_Friendships_People_RequesterId",
            table: "Friendships");

        migrationBuilder.DropForeignKey(
            name: "FK_GoalTasks_People_OwnerPersonId",
            table: "GoalTasks");

        migrationBuilder.DropForeignKey(
            name: "FK_Goals_People_OwnerPersonId",
            table: "Goals");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.DropIndex(
            name: "IX_Goals_OwnerPersonId",
            table: "Goals");

        migrationBuilder.DropIndex(
            name: "IX_GoalTasks_OwnerPersonId",
            table: "GoalTasks");

        migrationBuilder.DropIndex(
            name: "IX_Friendships_AddresseeId",
            table: "Friendships");

        migrationBuilder.DropIndex(
            name: "IX_Friendships_RequesterId_AddresseeId",
            table: "Friendships");

        migrationBuilder.DropColumn(
            name: "OwnerPersonId",
            table: "Goals");

        migrationBuilder.DropColumn(
            name: "OwnerPersonId",
            table: "GoalTasks");

        migrationBuilder.DropColumn(
            name: "AddresseeId",
            table: "Friendships");

        migrationBuilder.DropColumn(
            name: "RequestedAt",
            table: "Friendships");

        migrationBuilder.DropColumn(
            name: "RespondedAt",
            table: "Friendships");

        migrationBuilder.RenameColumn(
            name: "RequesterId",
            table: "Friendships",
            newName: "PersonId");

        migrationBuilder.AddColumn<bool>(
            name: "IsCurrentUser",
            table: "People",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "MutualFriends",
            table: "Friendships",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_People_IsCurrentUser",
            table: "People",
            column: "IsCurrentUser");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_PersonId",
            table: "Friendships",
            column: "PersonId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Friendships_People_PersonId",
            table: "Friendships",
            column: "PersonId",
            principalTable: "People",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
