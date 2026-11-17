using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeaBoard.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisoDeVotosNovos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "lastNotificationsSeenAt",
                table: "User",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lastNotificationsSeenAt",
                table: "User");
        }
    }
}
