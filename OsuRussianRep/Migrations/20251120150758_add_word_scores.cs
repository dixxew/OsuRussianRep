using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class add_word_scores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WordScore",
                table: "Words",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WordScore",
                table: "Words");
        }
    }
}
