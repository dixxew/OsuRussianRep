using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class AddedHistoryOfUserOldNicknames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatUserNickHistories",
                columns: table => new
                {
                    ChatUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatUserNickHistories", x => new { x.ChatUserId, x.Nickname });
                    table.ForeignKey(
                        name: "FK_ChatUserNickHistories_ChatUsers_ChatUserId",
                        column: x => x.ChatUserId,
                        principalTable: "ChatUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatUserNickHistories");
        }
    }
}
