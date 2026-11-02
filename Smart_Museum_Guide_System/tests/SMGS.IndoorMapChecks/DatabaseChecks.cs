using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;

internal static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection");
        var schema = "smgs_indoor_test_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options;
        await using var setup = new ApplicationDbContext(options);
        var createSql = $"CREATE SCHEMA \"{schema}\""; // Fixed prefix and generated UUID only.
        var dropSql = $"DROP SCHEMA \"{schema}\" CASCADE";
        await setup.Database.ExecuteSqlRawAsync(createSql);
        try
        {
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
            var museum = new Museum { Name = "Fixture museum" }; var other = new Museum { Name = "Other museum" };
            setup.Museums.AddRange(museum, other); await setup.SaveChangesAsync();
            var migrator = setup.GetService<IMigrator>();
            await setup.Database.ExecuteSqlRawAsync("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)");
            await setup.Database.ExecuteSqlRawAsync(migrator.GenerateScript("20261026002857_AddIndoorMaps", "20261021025653_InitialCreate"));
            await setup.Database.ExecuteSqlRawAsync(migrator.GenerateScript("20261021025653_InitialCreate", "20261026002857_AddIndoorMaps"));
            setup.ChangeTracker.Clear();
            Check(await setup.Museums.CountAsync() == 2 && await setup.Cities.CountAsync() == 34, "migration preserves museums and cities");
            var staff = Guid.NewGuid(); var outsider = Guid.NewGuid();
            setup.Users.Add(new ApplicationUser { Id = staff, UserName = "indoor-fixture", NormalizedUserName = "INDOOR-FIXTURE", Email = "indoor-fixture@example.invalid", NormalizedEmail = "INDOOR-FIXTURE@EXAMPLE.INVALID" });
            var role = await setup.Roles.SingleAsync(r => r.Name == RoleNames.MuseumStaff);
            setup.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staff, RoleId = role.Id }); await setup.SaveChangesAsync();
            async Task<T> Call<T>(Func<IndoorMapService, Task<T>> action)
            {
                await using var db = new ApplicationDbContext(options); return await action(new IndoorMapService(db));
            }
            async Task CallVoid(Func<IndoorMapService, Task> action)
            {
                await using var db = new ApplicationDbContext(options); await action(new IndoorMapService(db));
            }
            await CallVoid(s => s.Assign(museum.Id, staff));
            Check((await Call(s => s.Museums(staff, false))).Count == 1, "assigned museums only");
            await Deny(() => Call(s => s.Create(museum.Id, new(null, "Unknown"), staff, false)), 400);
            await CallVoid(s => s.SetLayout(museum.Id, "single", staff, false));
            var id = await Call(s => s.Create(museum.Id, new(null, "Map"), staff, false));
            await Deny(() => Call(s => s.Get(id, outsider, false)), 403);
            await Deny(() => Call(s => s.Create(other.Id, new(null, "Forbidden"), staff, false)), 403);
            await Deny(() => CallVoid(s => s.SetLayout(museum.Id, "floors", staff, false)), 409);
            await Deny(() => Call(s => s.Create(museum.Id, new(null, "Duplicate"), staff, false)), 409);
            var map = await Call(s => s.Get(id, staff, false));
            var artifact = new Artifact { MuseumId = museum.Id, Name = "Fixture artifact", QrCode = "INDOOR-ARTIFACT-FIXTURE" };
            setup.Artifacts.Add(artifact); await setup.SaveChangesAsync();
            var n1 = new MapNode { Id = Guid.NewGuid(), Label = "Start", Type = "corridor", X = .1, Y = .2 };
            var n2 = new MapNode { Id = Guid.NewGuid(), Label = "End", X = .8, Y = .7 };
            map.Nodes = [n1, n2];
            map.Edges = [new() { Id = Guid.NewGuid(), FromNodeId = n1.Id, ToNodeId = n2.Id }];
            map.Markers = [new() { Id = Guid.NewGuid(), Label = "Artifact", Type = "artifact", ArtifactId = artifact.Id, NodeId = n1.Id, X = .1, Y = .2 }];
            map = await Call(s => s.Save(id, map, staff, false));
            Check(map.Nodes.Count == 2 && (await Call(s => s.Get(id, staff, false))).Edges.Count == 1, "graph roundtrip");
            var stale = map.Revision;
            map = await Call(s => s.SetImage(id, map.Revision, Guid.NewGuid().ToString("N") + ".png", 1000, 500, false, staff, false));
            await Deny(() => Call(s => s.Publish(id, stale, true, staff, false)), 409);
            await Deny(() => Call(s => s.SetImage(id, map.Revision, "unused.png", 100, 100, false, staff, false)), 409);
            map = await Call(s => s.Publish(id, map.Revision, true, staff, false));
            var snapshot = await Call(s => s.Published(id));
            var qrArtifact = await Call(s => s.QrArtifactId(id, map.Markers[0].Id, staff, false));
            Check(qrArtifact == artifact.Id, "marker reuses existing artifact QR");
            await Deny(() => Call(s => s.QrArtifactId(id, map.Markers[0].Id, outsider, false)), 403);
            var utility = new MapMarker { Id = Guid.NewGuid(), Label = "Stair", Type = "stair" };
            map.Markers.Add(utility);
            map = await Call(s => s.Save(id, map, staff, false));
            await Deny(() => Call(s => s.QrArtifactId(id, utility.Id, staff, false)), 404);
            map.Markers.RemoveAll(m => m.Id == utility.Id);
            map.Nodes[0].X = .3;
            map = await Call(s => s.Save(id, map, staff, false));
            Check((await Call(s => s.Published(id))).Nodes[0].X == snapshot.Nodes[0].X, "published graph immutable");
            map = await Call(s => s.SetImage(id, map.Revision, Guid.NewGuid().ToString("N") + ".png", 1000, 500, false, staff, false));
            Check((await Call(s => s.Published(id))).ImageKey == snapshot.ImageKey, "published image immutable");
            var oldRevision = map.Revision; map.Revision--;
            await Deny(() => Call(s => s.Save(id, map, staff, false)), 409); map.Revision = oldRevision;
            var foreign = new Artifact { MuseumId = other.Id, Name = "Foreign" };
            setup.Artifacts.Add(foreign); await setup.SaveChangesAsync();
            map.Markers.Add(new() { Id = Guid.NewGuid(), Label = "Wrong museum", Type = "artifact", ArtifactId = foreign.Id });
            await Deny(() => Call(s => s.Save(id, map, staff, false)), 400); map.Markers.RemoveAt(1);
            map.Nodes.RemoveAt(0);
            await Deny(() => Call(s => s.Save(id, map, staff, false)), 400);
            Check((await Call(s => s.Get(id, staff, false))).Nodes.Count == 2, "failed save leaves graph intact");
            await CallVoid(s => s.SetLayout(other.Id, "floors", staff, true));
            var building = await Call(s => s.AddBuilding(other.Id, new("Building"), staff, true));
            var floor = await Call(s => s.AddFloor(other.Id, new(building, 0, "Ground"), staff, true));
            await Deny(() => Call(s => s.Create(museum.Id, new(floor, "Wrong floor"), staff, false)), 400);
            var floorMap = await Call(s => s.Create(other.Id, new(floor, "Floor map"), staff, true));
            Check((await Call(s => s.Get(floorMap, staff, true))).FloorId == floor, "floor scoped map");
            map = await Call(s => s.Get(id, staff, false));
            await Call(s => s.Publish(id, map.Revision, false, staff, false));
            await Deny(() => Call(s => s.Published(id)), 404);
            await HttpChecks.Run(builder.ConnectionString, id, museum.Id, staff);
            Console.WriteLine("PASS: PostgreSQL migration roundtrip, preserved data, staff scope, layout/floor checks, graph roundtrip, atomic failures, revision conflicts, immutable publication, QR and foreign links.");
        }
        finally { await setup.Database.ExecuteSqlRawAsync(dropSql); }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
    private static async Task Deny(Func<Task> action, int status)
    {
        try { await action(); } catch (IndoorMapException ex) when (ex.StatusCode == status) { return; }
        throw new Exception($"Expected rejection {status}");
    }
}
