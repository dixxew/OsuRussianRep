using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class AddedOsuIdAndProfileUrlToChatUserByWhois : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OsuProfileUrl",
                table: "ChatUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OsuUserId",
                table: "ChatUsers",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsuProfileUrl",
                table: "ChatUsers");

            migrationBuilder.DropColumn(
                name: "OsuUserId",
                table: "ChatUsers");
        }
    }
}
