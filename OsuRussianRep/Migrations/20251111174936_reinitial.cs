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
            // ===== ChatUsers =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""ChatUsers""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""ChatUsers"" (
            ""Id"" uuid NOT NULL PRIMARY KEY,
            ""Nickname"" text NOT NULL,
            ""Reputation"" bigint NULL,
            ""LastRepTime"" timestamptz NULL,
            ""LastUsedAddRep"" timestamptz NULL,
            ""LastRepNickname"" text NULL,
            ""LastMessageDate"" timestamptz NOT NULL
        );
        $sql$;
    END IF;

    -- UNIQUE по Nickname (если нет)
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'UQ_ChatUsers_Nickname'
    ) THEN
        EXECUTE 'ALTER TABLE ""ChatUsers"" ADD CONSTRAINT ""UQ_ChatUsers_Nickname"" UNIQUE (""Nickname"")';
    END IF;
END $$;");

            // ===== IngestOffsets =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""IngestOffsets""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""IngestOffsets"" (
            ""Day"" date NOT NULL PRIMARY KEY,
            ""LastSeq"" bigint NOT NULL
        );
        $sql$;
    END IF;
END $$;");

            // ===== Words =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""Words""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""Words"" (
            ""Id"" bigserial PRIMARY KEY,
            ""Lemma"" varchar(128) NOT NULL
        );
        $sql$;
    END IF;

    -- UNIQUE по Lemma
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'UQ_Words_Lemma'
    ) THEN
        EXECUTE 'ALTER TABLE ""Words"" ADD CONSTRAINT ""UQ_Words_Lemma"" UNIQUE (""Lemma"")';
    END IF;
END $$;");

            // ===== Messages =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""Messages""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""Messages"" (
            ""Seq"" bigserial PRIMARY KEY,
            ""Id"" uuid NOT NULL,
            ""Text"" varchar(8000) NOT NULL,
            ""Date"" timestamptz NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""ChatChannel"" varchar(200) NOT NULL
        );
        $sql$;
    END IF;

    -- UNIQUE по Id (альтернативный ключ)
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'UQ_Messages_Id'
    ) THEN
        EXECUTE 'ALTER TABLE ""Messages"" ADD CONSTRAINT ""UQ_Messages_Id"" UNIQUE (""Id"")';
    END IF;

    -- FK Messages.UserId -> ChatUsers.Id
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_Messages_ChatUsers_UserId'
    ) THEN
        EXECUTE 'ALTER TABLE ""Messages""
                 ADD CONSTRAINT ""FK_Messages_ChatUsers_UserId""
                 FOREIGN KEY (""UserId"") REFERENCES ""ChatUsers""(""Id"") ON DELETE CASCADE';
    END IF;

    -- Индексы
    IF to_regclass('public.""IX_Messages_Date""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_Messages_Date"" ON ""Messages""(""Date"")';
    END IF;

    IF to_regclass('public.""IX_Messages_ChatChannel_Date""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_Messages_ChatChannel_Date"" ON ""Messages""(""ChatChannel"",""Date"")';
    END IF;

    IF to_regclass('public.""IX_Messages_UserId_Date""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_Messages_UserId_Date"" ON ""Messages""(""UserId"",""Date"")';
    END IF;
END $$;");

            // ===== WordDays =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""WordDays""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""WordDays"" (
            ""Day"" date NOT NULL,
            ""WordId"" bigint NOT NULL,
            ""Cnt"" bigint NOT NULL,
            PRIMARY KEY (""Day"", ""WordId"")
        );
        $sql$;
    END IF;

    -- FK WordDays.WordId -> Words.Id (RESTRICT)
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_WordDays_Words_WordId'
    ) THEN
        EXECUTE 'ALTER TABLE ""WordDays""
                 ADD CONSTRAINT ""FK_WordDays_Words_WordId""
                 FOREIGN KEY (""WordId"") REFERENCES ""Words""(""Id"") ON DELETE RESTRICT';
    END IF;

    -- Индексы
    IF to_regclass('public.""IX_WordDays_Day""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_WordDays_Day"" ON ""WordDays""(""Day"")';
    END IF;

    IF to_regclass('public.""IX_WordDays_WordId""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_WordDays_WordId"" ON ""WordDays""(""WordId"")';
    END IF;
END $$;");

            // ===== WordUsers =====
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""WordUsers""') IS NULL THEN
        EXECUTE $sql$
        CREATE TABLE ""WordUsers"" (
            ""UserId"" uuid NOT NULL,
            ""WordId"" bigint NOT NULL,
            ""Cnt"" bigint NOT NULL,
            PRIMARY KEY (""UserId"", ""WordId"")
        );
        $sql$;
    END IF;

    -- FKs
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_WordUsers_ChatUsers_UserId'
    ) THEN
        EXECUTE 'ALTER TABLE ""WordUsers""
                 ADD CONSTRAINT ""FK_WordUsers_ChatUsers_UserId""
                 FOREIGN KEY (""UserId"") REFERENCES ""ChatUsers""(""Id"") ON DELETE CASCADE';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_WordUsers_Words_WordId'
    ) THEN
        EXECUTE 'ALTER TABLE ""WordUsers""
                 ADD CONSTRAINT ""FK_WordUsers_Words_WordId""
                 FOREIGN KEY (""WordId"") REFERENCES ""Words""(""Id"") ON DELETE RESTRICT';
    END IF;

    -- Индексы
    IF to_regclass('public.""IX_WordUsers_UserId""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_WordUsers_UserId"" ON ""WordUsers""(""UserId"")';
    END IF;

    IF to_regclass('public.""IX_WordUsers_WordId""') IS NULL THEN
        EXECUTE 'CREATE INDEX ""IX_WordUsers_WordId"" ON ""WordUsers""(""WordId"")';
    END IF;
END $$;");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
