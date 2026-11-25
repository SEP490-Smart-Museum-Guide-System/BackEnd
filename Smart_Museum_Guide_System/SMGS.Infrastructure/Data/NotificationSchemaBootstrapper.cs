using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SMGS.Infrastructure.Data;

public static class NotificationSchemaBootstrapper
{
    public const string CreateTableSql = """
        ALTER TABLE IF EXISTS "Users" ADD COLUMN IF NOT EXISTS "PreferredLanguage" character varying(2) NOT NULL DEFAULT 'vi';
        ALTER TABLE IF EXISTS "Users" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
        ALTER TABLE IF EXISTS "recognition_history" ADD COLUMN IF NOT EXISTS "ConfirmedQrScan" boolean NOT NULL DEFAULT FALSE;

        CREATE TABLE IF NOT EXISTS "artifact_detail_views" (
            "RequestId" uuid NOT NULL,
            "ArtifactId" integer NOT NULL,
            "ViewedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_artifact_detail_views" PRIMARY KEY ("RequestId"),
            CONSTRAINT "FK_artifact_detail_views_artifacts_ArtifactId" FOREIGN KEY ("ArtifactId") REFERENCES "artifacts" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_artifact_detail_views_ArtifactId" ON "artifact_detail_views" ("ArtifactId");

        CREATE TABLE IF NOT EXISTS "museum_translations" (
            "Id" integer GENERATED ALWAYS AS IDENTITY,
            "MuseumId" integer NOT NULL,
            "LanguageCode" character varying(2) NOT NULL,
            "Name" character varying(200) NOT NULL,
            "Description" text NULL,
            "Address" character varying(500) NULL,
            "OpeningHours" character varying(500) NULL,
            CONSTRAINT "PK_museum_translations" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_museum_translations_museums_MuseumId" FOREIGN KEY ("MuseumId") REFERENCES "museums" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_museum_translations_MuseumId_LanguageCode" ON "museum_translations" ("MuseumId", "LanguageCode");

        CREATE TABLE IF NOT EXISTS "artifact_translations" (
            "Id" integer GENERATED ALWAYS AS IDENTITY,
            "ArtifactId" integer NOT NULL,
            "LanguageCode" character varying(2) NOT NULL,
            "Name" character varying(300) NOT NULL,
            CONSTRAINT "PK_artifact_translations" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_artifact_translations_artifacts_ArtifactId" FOREIGN KEY ("ArtifactId") REFERENCES "artifacts" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_artifact_translations_ArtifactId_LanguageCode" ON "artifact_translations" ("ArtifactId", "LanguageCode");

        CREATE TABLE IF NOT EXISTS "notifications" (
            "Id" bigint GENERATED ALWAYS AS IDENTITY,
            "RecipientId" uuid NOT NULL,
            "MuseumId" integer NULL,
            "Type" character varying(50) NOT NULL,
            "Title" character varying(300) NOT NULL,
            "Body" character varying(1000) NOT NULL,
            "ReferenceType" character varying(50) NULL,
            "ReferenceId" character varying(100) NULL,
            "ActionUrl" character varying(500) NULL,
            "IsRead" boolean NOT NULL DEFAULT FALSE,
            "ReadAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_notifications" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_notifications_Users_RecipientId" FOREIGN KEY ("RecipientId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_notifications_museums_MuseumId" FOREIGN KEY ("MuseumId") REFERENCES "museums" ("Id") ON DELETE SET NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_notifications_MuseumId" ON "notifications" ("MuseumId");
        CREATE INDEX IF NOT EXISTS "IX_notifications_RecipientId_CreatedAt" ON "notifications" ("RecipientId", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_notifications_RecipientId_IsRead_CreatedAt" ON "notifications" ("RecipientId", "IsRead", "CreatedAt");

        CREATE TABLE IF NOT EXISTS "guide_chat_messages" (
            "Id" bigint GENERATED ALWAYS AS IDENTITY,
            "UserId" uuid NOT NULL,
            "ArtifactId" integer NOT NULL,
            "MuseumId" integer NOT NULL,
            "Question" character varying(1000) NOT NULL,
            "Answer" character varying(4000) NOT NULL,
            "Supported" boolean NOT NULL,
            "ContentVersionId" integer NULL,
            "ContentVersionNo" integer NULL,
            "ContentTitle" character varying(300) NULL,
            "LanguageCode" character varying(10) NOT NULL DEFAULT 'vi',
            "CreatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_guide_chat_messages" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_guide_chat_messages_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_guide_chat_messages_artifacts_ArtifactId" FOREIGN KEY ("ArtifactId") REFERENCES "artifacts" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_guide_chat_messages_museums_MuseumId" FOREIGN KEY ("MuseumId") REFERENCES "museums" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_guide_chat_messages_ArtifactId" ON "guide_chat_messages" ("ArtifactId");
        CREATE INDEX IF NOT EXISTS "IX_guide_chat_messages_UserId_ArtifactId_CreatedAt" ON "guide_chat_messages" ("UserId", "ArtifactId", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_guide_chat_messages_MuseumId_Supported_CreatedAt" ON "guide_chat_messages" ("MuseumId", "Supported", "CreatedAt");
        """;

    public static async Task EnsureNotificationsTableAsync(IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync(CreateTableSql);
        }
        catch
        {
            // Ignore during design-time or when database is not yet initialized.
        }
    }
}
