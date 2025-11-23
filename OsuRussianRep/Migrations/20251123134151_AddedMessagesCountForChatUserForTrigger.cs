using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class AddedMessagesCountForChatUserForTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MessagesCount",
                table: "ChatUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // функция для инсерта/апдейта в Messages
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION update_messages_count_on_insert()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE ""ChatUsers""
    SET ""MessagesCount"" = ""MessagesCount"" + 1
    WHERE ""Id"" = NEW.""UserId"";
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
");

            // функция для удаления сообщений
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION update_messages_count_on_delete()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE ""ChatUsers""
    SET ""MessagesCount"" = ""MessagesCount"" - 1
    WHERE ""Id"" = OLD.""UserId"";
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;
");

            // триггер на insert
            migrationBuilder.Sql(@"
CREATE TRIGGER messages_count_insert
AFTER INSERT ON ""Messages""
FOR EACH ROW
EXECUTE FUNCTION update_messages_count_on_insert();
");

            // триггер на delete
            migrationBuilder.Sql(@"
CREATE TRIGGER messages_count_delete
AFTER DELETE ON ""Messages""
FOR EACH ROW
EXECUTE FUNCTION update_messages_count_on_delete();
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS messages_count_insert ON ""Messages"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS messages_count_delete ON ""Messages"";");

            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS update_messages_count_on_insert();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS update_messages_count_on_delete();");

            migrationBuilder.DropColumn(
                name: "MessagesCount",
                table: "ChatUsers");
        }
    }
}