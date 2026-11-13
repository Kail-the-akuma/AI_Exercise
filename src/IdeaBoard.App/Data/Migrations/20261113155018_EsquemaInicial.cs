using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeaBoard.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    entraObjectId = table.Column<string>(type: "TEXT", nullable: false),
                    displayName = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: false),
                    firstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Idea",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    authorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Idea", x => x.id);
                    table.ForeignKey(
                        name: "FK_Idea_User_authorUserId",
                        column: x => x.authorUserId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vote",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ideaId = table.Column<int>(type: "INTEGER", nullable: false),
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vote", x => x.id);
                    table.ForeignKey(
                        name: "FK_Vote_Idea_ideaId",
                        column: x => x.ideaId,
                        principalTable: "Idea",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vote_User_userId",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Idea_authorUserId",
                table: "Idea",
                column: "authorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Idea_createdAt",
                table: "Idea",
                column: "createdAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_User_entraObjectId",
                table: "User",
                column: "entraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vote_ideaId_userId",
                table: "Vote",
                columns: new[] { "ideaId", "userId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vote_userId",
                table: "Vote",
                column: "userId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vote");

            migrationBuilder.DropTable(
                name: "Idea");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
