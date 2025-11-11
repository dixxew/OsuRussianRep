using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class CleanInitial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("\nDELETE FROM \"__EFMigrationsHistory\";");
            migrationBuilder.Sql("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20251111120000_CleanInitial', '6.0.32');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
