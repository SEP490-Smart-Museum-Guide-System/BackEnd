using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SMGS.Infrastructure.Data;

public static class NotificationSchemaBootstrapper
{
    public const string CreateTableSql = """
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
