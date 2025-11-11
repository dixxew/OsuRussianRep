using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OsuRussianRep.Migrations
{
    public partial class reinitial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ChatUsers
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'chatusers') THEN
        CREATE TABLE ""ChatUsers"" (
            ""Id"" uuid NOT NULL PRIMARY KEY,
            ""Nickname"" text NOT NULL UNIQUE,
            ""Reputation"" bigint NULL,
            ""LastRepTime"" timestamptz NULL,
            ""LastUsedAddRep"" timestamptz NULL,
            ""LastRepNickname"" text NULL,
            ""LastMessageDate"" timestamptz NOT NULL
        );
    END IF;
END $$;
");

            // IngestOffsets
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'ingestoffsets') THEN
        CREATE TABLE ""IngestOffsets"" (
            ""Day"" date NOT NULL PRIMARY KEY,
            ""LastSeq"" bigint NOT NULL
        );
    END IF;
END $$;
");

            // Words
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'words') THEN
        CREATE TABLE ""Words"" (
            ""Id"" bigserial PRIMARY KEY,
            ""Lemma"" varchar(128) NOT NULL UNIQUE
        );
    END IF;
END $$;
");

            // Messages
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'messages') THEN
        CREATE TABLE ""Messages"" (
            ""Seq"" bigserial PRIMARY KEY,
            ""Id"" uuid NOT NULL UNIQUE,
            ""Text"" varchar(8000) NOT NULL,
            ""Date"" timestamptz NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""ChatChannel"" varchar(200) NOT NULL,
            CONSTRAINT ""FK_Messages_ChatUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""ChatUsers"" (""Id"") ON DELETE CASCADE
        );
        CREATE INDEX ""IX_Messages_ChatChannel_Date"" ON ""Messages"" (""ChatChannel"", ""Date"");
        CREATE INDEX ""IX_Messages_Date"" ON ""Messages"" (""Date"");
        CREATE INDEX ""IX_Messages_UserId_Date"" ON ""Messages"" (""UserId"", ""Date"");
    END IF;
END $$;
");

            // WordDays
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'worddays') THEN
        CREATE TABLE ""WordDays"" (
            ""Day"" date NOT NULL,
            ""WordId"" bigint NOT NULL,
            ""Cnt"" bigint NOT NULL,
            PRIMARY KEY (""Day"", ""WordId""),
            CONSTRAINT ""FK_WordDays_Words_WordId"" FOREIGN KEY (""WordId"") REFERENCES ""Words"" (""Id"") ON DELETE RESTRICT
        );
        CREATE INDEX ""IX_WordDays_Day"" ON ""WordDays"" (""Day"");
        CREATE INDEX ""IX_WordDays_WordId"" ON ""WordDays"" (""WordId"");
    END IF;
END $$;
");

            // WordUsers
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'wordusers') THEN
        CREATE TABLE ""WordUsers"" (
            ""UserId"" uuid NOT NULL,
            ""WordId"" bigint NOT NULL,
            ""Cnt"" bigint NOT NULL,
            PRIMARY KEY (""UserId"", ""WordId""),
            CONSTRAINT ""FK_WordUsers_ChatUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""ChatUsers"" (""Id"") ON DELETE CASCADE,
            CONSTRAINT ""FK_WordUsers_Words_WordId"" FOREIGN KEY (""WordId"") REFERENCES ""Words"" (""Id"") ON DELETE RESTRICT
        );
        CREATE INDEX ""IX_WordUsers_UserId"" ON ""WordUsers"" (""UserId"");
        CREATE INDEX ""IX_WordUsers_WordId"" ON ""WordUsers"" (""WordId"");
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
