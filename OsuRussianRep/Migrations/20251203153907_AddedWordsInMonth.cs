using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class AddedWordsInMonth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordMonths",
                columns: table => new
                {
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    WordId = table.Column<long>(type: "bigint", nullable: false),
                    Cnt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordMonths", x => new { x.Month, x.WordId });
                    table.ForeignKey(
                        name: "FK_WordMonths_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WordMonths_Month",
                table: "WordMonths",
                column: "Month");

            migrationBuilder.CreateIndex(
                name: "IX_WordMonths_WordId",
                table: "WordMonths",
                column: "WordId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordMonths");
        }
    }
}
