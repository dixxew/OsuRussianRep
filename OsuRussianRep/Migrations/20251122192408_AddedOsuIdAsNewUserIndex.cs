using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class AddedOsuIdAsNewUserIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatUsers_Nickname",
                table: "ChatUsers");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUsers_OsuUserId",
                table: "ChatUsers",
                column: "OsuUserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatUsers_OsuUserId",
                table: "ChatUsers");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUsers_Nickname",
                table: "ChatUsers",
                column: "Nickname",
                unique: true);
        }
    }
}
