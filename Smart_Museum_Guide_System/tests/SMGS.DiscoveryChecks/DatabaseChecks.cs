using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
        var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection");
        var schema = "smgs_discovery_test_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);
        // Identifier is generated locally from a fixed prefix and Guid N, never input.
        var createSchemaSql = $"CREATE SCHEMA \"{schema}\"";
        var dropSchemaSql = $"DROP SCHEMA \"{schema}\" CASCADE";
        await db.Database.ExecuteSqlRawAsync(createSchemaSql);
        try
        {
            // Existing museum baseline only; legacy migrations are deliberately not replayed.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE museums (
                  "Id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                  "Name" varchar(200) NOT NULL, "Description" text, "Address" varchar(500),
                  "Latitude" decimal(9,6), "Longitude" decimal(9,6), "OpeningHours" varchar(500),
                  "CoverImageUrl" varchar(1000), "IsActive" boolean NOT NULL DEFAULT true,
                  "CreatedAt" timestamptz NOT NULL);
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                INSERT INTO museums ("Name", "CreatedAt") VALUES ('Legacy', now());
                """);
            var migrator = db.GetService<IMigrator>();
            var script = migrator.GenerateScript("20261021025653_InitialCreate", "20261021093939_AddMuseumDiscoveryCities");
            await db.Database.ExecuteSqlRawAsync(script);
            Check(await db.Cities.CountAsync() == 34, "City seed missing.");
            Check((await db.Museums.SingleAsync()).CityCode is null, "Migration changed legacy data.");
            var near = new Museum { Name = "Test Near", CityCode = "79", Latitude = 0, Longitude = 1 };
            var tied = new Museum { Name = "Test Tied", CityCode = "79", Latitude = 0, Longitude = 1 };
            var far = new Museum { Name = "Test Far", CityCode = "79", Latitude = 0, Longitude = 2 };
            db.Museums.AddRange(near, tied, far,
                new() { Name = "Test Unknown", CityCode = "79" },
                new() { Name = "Test Inactive", CityCode = "79", IsActive = false, Latitude = 0, Longitude = 0 },
                new() { Name = "Test Other city", CityCode = "01", Latitude = 0, Longitude = 0 },
                new() { Name = "Dateline", CityCode = "01", Latitude = 0, Longitude = -179 },
                new() { Name = "Antipode", CityCode = "01", Latitude = 0, Longitude = 180 });
            await db.SaveChangesAsync();
            var repo = new MuseumRepository(db);
            var query = new MuseumDiscoveryQuery { Keyword = "test", CityCode = "79", IsActive = true,
                Latitude = 0, Longitude = 0, SortBy = "distance", Page = 1, PageSize = 1 };
            var first = await repo.DiscoverAsync(query);
            Check(first.TotalCount == 4 && first.Items.Single().Museum.Id == near.Id, "Filters/sort must run before pagination.");
            Check(Math.Abs(first.Items[0].DistanceKm!.Value - 111.1949) < 0.001, "Haversine result wrong.");
            query.Page = 2;
            Check((await repo.DiscoverAsync(query)).Items.Single().Museum.Id == tied.Id, "Equal distance ordering unstable.");
            query.Page = 4;
            Check((await repo.DiscoverAsync(query)).Items.Single().DistanceKm is null, "Unknown location must sort last.");
            query.Page = 1; query.Keyword = "Dateline"; query.CityCode = "01"; query.Longitude = 179;
            Check(Math.Abs((await repo.DiscoverAsync(query)).Items.Single().DistanceKm!.Value - 222.38985) < 0.001, "Dateline distance incorrect.");
            query.Keyword = "Antipode"; query.Longitude = 0;
            Check(double.IsFinite((await repo.DiscoverAsync(query)).Items.Single().DistanceKm!.Value), "Antipodal distance non-finite.");
            query.Latitude = null; query.Longitude = null; query.SortBy = null;
            Check((await repo.DiscoverAsync(query)).Items.Single().DistanceKm is null, "No origin must mean no distance.");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { await repo.DiscoverAsync(query, canceled.Token); throw new Exception("DB cancellation swallowed."); }
            catch (OperationCanceledException) { }
            Console.WriteLine("PASS: isolated PostgreSQL migration, preserved data, FK/catalog, Haversine translation, pagination, ties, dateline and cancellation.");
        }
        finally { await db.Database.ExecuteSqlRawAsync(dropSchemaSql); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
