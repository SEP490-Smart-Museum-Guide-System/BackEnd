using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Repositories;

internal static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION for database checks.");
        // All DDL and test data stay in a randomly named schema, removed in finally.
        var schema = "smgs_content_test_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);
        await using var setup = Open();
        var createSchemaSql = $"CREATE SCHEMA \"{schema}\""; // Identifier contains only a fixed prefix and Guid N format.
        var dropSchemaSql = $"DROP SCHEMA \"{schema}\" CASCADE";
        await setup.Database.ExecuteSqlRawAsync(createSchemaSql);
        try
        {
            // Minimal existing tables referenced by this migration. The legacy initial migration
            // is outside this test: it contains a QR index filter using a different column name.
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "Users" ("Id" uuid PRIMARY KEY);
                CREATE TABLE "Roles" ("Id" uuid PRIMARY KEY, "Name" varchar(256), "NormalizedName" varchar(256), "ConcurrencyStamp" text);
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                CREATE TABLE artifacts (
                    "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    "MuseumId" integer NOT NULL, "GalleryId" integer, "Code" varchar(100), "Name" varchar(300) NOT NULL,
                    "QrCode" varchar(200), "MapNodeId" integer, "MapX" real, "MapY" real, "MapIcon" varchar(500),
                    "AccessLevel" varchar(20) NOT NULL DEFAULT 'free', "IsActive" boolean NOT NULL DEFAULT true,
                    "CreatedAt" timestamptz NOT NULL);
                """);
            var migration = setup.GetService<IMigrator>().GenerateScript("20261019043314_kj", "20261020103942_AddArtifactContentReview");
            await setup.Database.ExecuteSqlRawAsync(migration);
            var creator = Guid.NewGuid();
            var curator = Guid.NewGuid();
            await setup.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\") VALUES ({creator}), ({curator})");
            var artifact = new Artifact { Name = "Test Artifact", MuseumId = 1 };
            setup.Artifacts.Add(artifact);
            await setup.SaveChangesAsync();
            async Task<int> Submit(string language = "vi")
            {
                await using var db = Open();
                var service = new ArtifactContentService(new ArtifactContentRepository(db), new ArtifactRepository(db));
                var result = await service.SubmitAsync(artifact.Id, new() { LanguageCode = language, Title = "AI title", Description = "AI description" }, creator);
                Check(result.StatusCode == 201, "Submission failed.");
                return result.Data!.Id;
            }
            async Task Review(int id, bool approve, string? note = null)
            {
                await using var db = Open();
                var service = new ArtifactContentService(new ArtifactContentRepository(db), new ArtifactRepository(db));
                Check((await service.ReviewAsync(id, new() { Note = note }, curator, approve)).StatusCode == 200, "Review failed.");
            }
            async Task<ArtifactContentVersion?> Published(string language = "vi")
            {
                await using var db = Open();
                return await new ArtifactContentRepository(db).GetPublishedAsync(artifact.Id, language);
            }
            var first = await Submit();
            Check(await Published() is null, "Pending content leaked to visitors.");
            await Review(first, true, "Verified");
            var second = await Submit();
            Check((await Published())?.Id == first, "Submission replaced published content before review.");
            await Review(second, false, "Wrong date");
            Check((await Published())?.Id == first, "Rejection removed current publication.");
            var third = await Submit();
            await Review(third, true);
            Check((await Published())?.Id == third, "Replacement was not published.");
            await using (var db = Open())
            {
                var old = await db.ArtifactContentVersions.FindAsync(first);
                Check(old is { Status: ContentStatuses.Archived, IsCurrent: false, ReviewNote: "Verified" } && old.ReviewedBy == curator && old.PublishedAt is not null, "Old version/audit history was not preserved.");
                var rejected = await db.ArtifactContentVersions.FindAsync(second);
                Check(rejected is { Status: ContentStatuses.Rejected, ReviewNote: "Wrong date", IsCurrent: false }, "Rejection audit was not saved.");
                Check((await db.ArtifactContentVersions.FindAsync(third))?.VersionNo == 3, "Version numbers must increase.");
            }
            var en = await Submit("en");
            await Review(en, true);
            Check((await Published())?.Id == third && (await Published("en"))?.Id == en, "Publication must be independent per language.");
            // Deterministic race: both requests read before either commits.
            var fourth = await Submit();
            var fifth = await Submit();
            await using (var a = Open())
            await using (var b = Open())
            {
                var ra = new ArtifactContentRepository(a);
                var rb = new ArtifactContentRepository(b);
                var va = (await ra.GetByIdAsync(fourth))!;
                var vb = (await rb.GetByIdAsync(fifth))!;
                Check(await ra.ReviewAsync(va, curator, true, null), "First concurrent review failed.");
                Check(!await rb.ReviewAsync(vb, curator, true, null), "Stale review of another version must conflict.");
            }
            Check((await Published())?.Id == fourth, "Losing review changed publication.");
            await using (var a = Open())
            await using (var b = Open())
            {
                var ra = new ArtifactContentRepository(a);
                var rb = new ArtifactContentRepository(b);
                var va = (await ra.GetByIdAsync(fifth))!;
                var vb = (await rb.GetByIdAsync(fifth))!;
                Check(await ra.ReviewAsync(va, curator, false, "Wrong source"), "First rejection failed.");
                Check(!await rb.ReviewAsync(vb, curator, true, null), "Stale review of same version must conflict.");
            }
            // A failure after archiving must roll back the entire publication transaction.
            var sixth = await Submit();
            await using (var db = Open())
            {
                var repo = new ArtifactContentRepository(db);
                var version = (await repo.GetByIdAsync(sixth))!;
                try { await repo.ReviewAsync(version, Guid.NewGuid(), true, null); throw new Exception("Expected reviewer FK failure."); }
                catch (DbUpdateException) { }
            }
            Check((await Published())?.Id == fourth, "Failed review did not roll back archival.");
            await using (var db = Open())
            {
                var repo = new ArtifactContentRepository(db);
                var (items, count) = await repo.GetPagedAsync(1, artifact.Id, ContentStatuses.PendingReview, 1, 10);
                Check(count == 1 && items.Single().Id == sixth, "Queue filters or rollback failed.");
                artifact = (await db.Artifacts.FindAsync(artifact.Id))!;
                artifact.IsActive = false;
                await db.SaveChangesAsync();
            }
            Check(await Published() is null, "Inactive artifact must not expose published content.");
            Console.WriteLine("PASS: PostgreSQL migration, version history, language isolation, concurrent reviews and transactional rollback.");
        }
        finally { await setup.Database.ExecuteSqlRawAsync(dropSchemaSql); }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
