using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? throw new Exception("Set SMGS_TEST_CONNECTION; this suite uses an isolated schema.");
var schema = "smgs_presence_test_" + Guid.NewGuid().ToString("N");
var cs = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
var clock = new PresenceClock(DateTimeOffset.UtcNow);
await using var setup = new ApplicationDbContext(options);
var create = $"CREATE SCHEMA \"{schema}\""; var drop = $"DROP SCHEMA \"{schema}\" CASCADE";
await setup.Database.ExecuteSqlRawAsync(create);
WebApplication? host = null;
try
{
    await setup.GetService<IMigrator>().MigrateAsync("InitialCurrentSchema");
    var preservedUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "presence-upgrade-sentinel" };
    var preservedMuseum = new Museum { Name = "Existing museum before presence upgrade" };
    setup.AddRange(preservedUser, preservedMuseum); await setup.SaveChangesAsync();
    var preservedTour = new Tour { MuseumId = preservedMuseum.Id, CreatedBy = preservedUser.Id, Title = "Existing tour must survive upgrade", Status = "draft" };
    setup.Add(preservedTour); await setup.SaveChangesAsync();
    await setup.Database.MigrateAsync(); await setup.Database.MigrateAsync();
    Check(await setup.Tours.AsNoTracking().AnyAsync(t => t.Id == preservedTour.Id && t.Title == "Existing tour must survive upgrade")
        && await setup.Museums.AsNoTracking().AnyAsync(m => m.Id == preservedMuseum.Id), "incremental migration and repeat preserve pre-existing tour/museum rows");
    var visitor = Guid.NewGuid(); var other = Guid.NewGuid(); var staff = Guid.NewGuid(); var stranger = Guid.NewGuid();
    setup.Users.AddRange(new[] { visitor, other, staff, stranger }.Select(id => new ApplicationUser { Id = id, UserName = id.ToString() }));
    var museum = new Museum { Name = "Presence A", Latitude = 0, Longitude = 0 };
    var foreign = new Museum { Name = "Presence B", Latitude = 1, Longitude = 1 };
    setup.Museums.AddRange(museum, foreign); await setup.SaveChangesAsync();
    var building = new Building { MuseumId = museum.Id, Name = "Main" }; setup.Buildings.Add(building); await setup.SaveChangesAsync();
    var floor = new Floor { BuildingId = building.Id, Name = "Tầng 1", LevelNo = 1 }; setup.Floors.Add(floor); await setup.SaveChangesAsync();
    var gallery = new Gallery { FloorId = floor.Id, Name = "Phòng gốm" }; setup.Galleries.Add(gallery); await setup.SaveChangesAsync();
    var artifact = new Artifact { MuseumId = museum.Id, GalleryId = gallery.Id, Name = "Bình gốm", QrCode = "presence-artifact" }; setup.Artifacts.Add(artifact);
    setup.StaffAssignments.Add(new() { UserId = staff, MuseumId = museum.Id });
    var visit = new Visit { MuseumId = museum.Id, VisitorId = visitor };
    var foreignVisit = new Visit { MuseumId = museum.Id, VisitorId = other };
    setup.Visits.AddRange(visit, foreignVisit); await setup.SaveChangesAsync();
    var map = new IndoorMap { MuseumId = museum.Id, FloorId = floor.Id, Name = "Tầng 1", ImageWidth = 100, ImageHeight = 100 };
    setup.IndoorMaps.Add(map); await setup.SaveChangesAsync();
    var nodes = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    var entry = Guid.NewGuid(); var room = Guid.NewGuid(); var artMarker = Guid.NewGuid();
    var document = new MapDocument { Id = map.Id, MuseumId = museum.Id, FloorId = floor.Id, Revision = 1, Name = "Sơ đồ tầng 1", ImageWidth = 100, ImageHeight = 100,
        Nodes = nodes.Select((id, i) => new MapNode { Id = id, Label = "N" + i, X = i / 3d, Y = .5 }).ToList(),
        Markers = [ new() { Id = entry, Type = "entrance", Label = "Cổng chính", NodeId = nodes[0] },
            new() { Id = room, Type = "gallery", Label = "Phòng gốm", GalleryId = gallery.Id, NodeId = nodes[1] },
            new() { Id = artMarker, Type = "artifact", Label = artifact.Name, ArtifactId = artifact.Id, NodeId = nodes[2], IsDestination = true } ],
        Edges = [new() { Id = Guid.NewGuid(), FromNodeId = nodes[0], ToNodeId = nodes[1], IsAccessible = true },
            new() { Id = Guid.NewGuid(), FromNodeId = nodes[1], ToNodeId = nodes[2], IsAccessible = false }] };
    map.PublishedJson = JsonSerializer.Serialize(document); map.PublishedAt = clock.GetUtcNow().UtcDateTime; await setup.SaveChangesAsync();
    async Task<T> Run<T>(Func<MuseumPresenceService, Task<T>> action) { await using var db = new ApplicationDbContext(options); return await action(new(db, clock)); }
    async Task Denied(Func<Task> action, int status) { try { await action(); throw new Exception("Expected " + status); } catch (ExperienceException e) { Check(e.StatusCode == status, "denial " + status); } }
    var gps = new GpsAssessmentRequest(0, 0, 10, clock.GetUtcNow());
    Check((await Run(s => s.AssessGps(museum.Id, gps))).Status == "nearby", "fresh precise GPS is nearby, never confirmed");
    Check((await Run(s => s.Current(museum.Id, visitor, null))).Status == "unknown" && !await setup.Set<MuseumPresence>().AnyAsync(), "GPS assessment does not persist arrival or a location trail");
    foreach (var bad in new[] { gps with { IsLastKnown = true }, gps with { IsMocked = true }, gps with { AccuracyMeters = 100 },
        gps with { CapturedAt = clock.GetUtcNow().AddMinutes(-1) }, gps with { CapturedAt = clock.GetUtcNow().AddMinutes(1) } })
        Check((await Run(s => s.AssessGps(museum.Id, bad))).Status == "uncertain", "stale/mock/inaccurate/future GPS cannot establish nearby");
    Check((await Run(s => s.AssessGps(museum.Id, gps with { Latitude = 1 }))).Status == "outside", "far GPS is outside selected museum");
    Check((await Run(s => s.AssessGps(museum.Id, gps with { Latitude = .0013, AccuracyMeters = 30 }))).Status == "uncertain", "accuracy circle overlapping boundary is uncertain");
    var entryQr = await Run(s => s.QrText(museum.Id, map.Id, entry, staff, false));
    var roomQr = await Run(s => s.QrText(museum.Id, map.Id, room, staff, false));
    await Denied(async () => await Run(s => s.QrText(museum.Id, map.Id, entry, stranger, false)), 403);
    var request = new CheckpointScanRequest(entryQr, Guid.NewGuid(), visit.Id);
    var first = await Run(s => s.Scan(museum.Id, visitor, request));
    Check(first.IsFresh && first.Label == "Cổng chính" && first.MapId == map.Id, "entrance QR confirms museum and map anchor");
    await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Run(s => s.Scan(museum.Id, visitor, request))));
    Check(await setup.Set<MuseumPresence>().CountAsync() == 1, "concurrent retries persist exactly one observation");
    await Denied(async () => await Run(s => s.Scan(museum.Id, visitor, request with { Code = roomQr })), 409);
    await Denied(async () => await Run(s => s.Scan(foreign.Id, visitor, request with { VisitId = null, RequestId = Guid.NewGuid() })), 404);
    await Denied(async () => await Run(s => s.Scan(museum.Id, visitor, request with { VisitId = foreignVisit.Id, RequestId = Guid.NewGuid() })), 404);
    Check(!await setup.Set<VisitItem>().AnyAsync() && !await setup.Set<PointTransaction>().AnyAsync() && !await setup.Set<DailyQuizEntry>().AnyAsync(), "room/entrance QR does not award artifact progress, points or quiz unlocks");
    var route = JsonSerializer.SerializeToElement(await Run(s => s.Route(museum.Id, visitor, new(map.Id, null, artifact.Id, visit.Id))));
    Check(route.GetProperty("Route").GetProperty("NodeIds").GetArrayLength() == 3, "server routes from confirmed entrance to artifact");
    await Denied(async () => await Run(s => s.Route(museum.Id, visitor, new(map.Id, null, artifact.Id, visit.Id, true))), 422);
    clock.Advance(TimeSpan.FromSeconds(1));
    var roomPosition = await Run(s => s.Scan(museum.Id, visitor, new(roomQr, Guid.NewGuid(), visit.Id)));
    Check(roomPosition.GalleryName == gallery.Name && roomPosition.FloorName == floor.Name && roomPosition.NodeId == nodes[1], "room scan updates actual gallery/floor/node");
    clock.Advance(TimeSpan.FromMinutes(6));
    Check(!(await Run(s => s.Current(museum.Id, visitor, visit.Id))).IsFresh, "indoor anchor expires after five minutes");
    await Denied(async () => await Run(s => s.Route(museum.Id, visitor, new(map.Id, null, artifact.Id, visit.Id))), 409);
    Check((await Run(s => s.AssessGps(museum.Id, gps with { CapturedAt = clock.GetUtcNow() }))).Status == "nearby"
        && !(await Run(s => s.Current(museum.Id, visitor, visit.Id))).IsFresh, "GPS never refreshes an expired indoor anchor");
    var latest = await Run(s => s.ScanAny(visitor, new(roomQr, Guid.NewGuid(), visit.Id)));
    Check(latest.MuseumId == museum.Id && latest.IsFresh, "general scanner resolves museum from published QR");
    Check((await Run(s => s.Current(museum.Id, other, null))).Status == "unknown", "presence history is private to authenticated visitor");
    var floor2 = new Floor { BuildingId = building.Id, Name = "Tầng 2", LevelNo = 2 }; setup.Floors.Add(floor2); await setup.SaveChangesAsync();
    var map2 = new IndoorMap { MuseumId = museum.Id, FloorId = floor2.Id, Name = "Tầng 2" }; setup.IndoorMaps.Add(map2); await setup.SaveChangesAsync();
    var upperNode = Guid.NewGuid(); var upperMarker = Guid.NewGuid();
    map2.PublishedJson = JsonSerializer.Serialize(new MapDocument { Id = map2.Id, MuseumId = museum.Id, FloorId = floor2.Id, Revision = 1,
        Nodes = [new() { Id = upperNode, Label = "Lối vào tầng 2" }],
        Markers = [new() { Id = upperMarker, Type = "entrance", Label = "Cửa tầng 2", NodeId = upperNode }] });
    await setup.SaveChangesAsync();
    var upperQr = await Run(s => s.QrText(museum.Id, map2.Id, upperMarker, staff, false));
    var upper = await Run(s => s.ScanAny(visitor, new(upperQr, Guid.NewGuid(), visit.Id)));
    Check(upper.MapId == map2.Id && upper.FloorName == floor2.Name, "new floor scan selects the correct published map/floor");
    await Denied(async () => await Run(s => s.Route(museum.Id, visitor, new(map.Id, null, artifact.Id, visit.Id))), 422);
    clock.Advance(TimeSpan.FromSeconds(1));
    setup.Add(new RecognitionHistory { UserId = visitor, VisitId = visit.Id, ArtifactId = artifact.Id, IsSuccess = true,
        Method = "qr", RequestId = Guid.NewGuid(), RecognizedAt = clock.GetUtcNow().UtcDateTime });
    await setup.SaveChangesAsync();
    var recognized = await Run(s => s.Current(museum.Id, visitor, visit.Id));
    Check(recognized.ArtifactId == artifact.Id && recognized.MapId == map.Id && recognized.GalleryId == gallery.Id,
        "a newer artifact recognition replaces older checkpoint and supplies room/map");
    clock.Advance(TimeSpan.FromSeconds(1));
    await Run(s => s.ScanAny(visitor, new(roomQr, Guid.NewGuid(), visit.Id)));
    document.Revision = 2; map.PublishedJson = JsonSerializer.Serialize(document); await setup.SaveChangesAsync();
    Check(!(await Run(s => s.Current(museum.Id, visitor, visit.Id))).IsFresh, "republished map invalidates old checkpoint anchor");
    await Denied(async () => await Run(s => s.Scan(museum.Id, visitor, new(roomQr, Guid.NewGuid(), visit.Id))), 409);

    var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
    builder.Services.AddControllers().AddApplicationPart(typeof(MuseumPresenceController).Assembly);
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
    builder.Services.AddSingleton<TimeProvider>(clock); builder.Services.AddScoped<MuseumPresenceService>(); builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("audit-presence-only-token-key-20261120-1234567890"));
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key });
    builder.Services.AddAuthorization(); host = builder.Build(); host.UseAuthentication(); host.UseAuthorization(); host.MapControllers(); await host.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    void Login(Guid id, string role) { var jwt = new JwtSecurityToken(claims: [new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddHours(1), signingCredentials: new(key, SecurityAlgorithms.HmacSha256)); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt)); }
    Check((await client.GetAsync($"/api/museums/{museum.Id}/presence")).StatusCode == HttpStatusCode.Unauthorized, "HTTP requires login");
    Login(visitor, RoleNames.Visitor);
    Check((await client.GetAsync($"/api/museums/{museum.Id}/presence/checkpoints/{map.Id}/{entry}/qr")).StatusCode == HttpStatusCode.Forbidden, "HTTP Visitor cannot mint staff QR");
    Check((await client.GetAsync($"/api/museums/{museum.Id}/presence")).IsSuccessStatusCode, "HTTP visitor can retrieve own presence");
    Check((await client.PostAsJsonAsync($"/api/museums/{museum.Id}/presence/gps-assessment", gps with { Latitude = 100 })).StatusCode == HttpStatusCode.BadRequest, "HTTP rejects invalid coordinates");
    Login(stranger, RoleNames.MuseumStaff);
    Check((await client.GetAsync($"/api/museums/{museum.Id}/presence/checkpoints/{map.Id}/{entry}/qr")).StatusCode == HttpStatusCode.Forbidden, "HTTP staff assignment enforced");
    Login(staff, RoleNames.MuseumStaff);
    var png = await client.GetAsync($"/api/museums/{museum.Id}/presence/checkpoints/{map.Id}/{entry}/qr");
    Check(png.IsSuccessStatusCode && (await png.Content.ReadAsByteArrayAsync()).Take(4).SequenceEqual(new byte[] { 137, 80, 78, 71 }), "staff downloads real PNG checkpoint QR");
    Console.WriteLine($"PASS {checks} presence checks: GPS accuracy/age, QR ownership/revision/idempotency, freshness, rooms, routing, no progress side effects and HTTP roles.");
}
finally
{
    if (host != null) { await host.StopAsync(); await host.DisposeAsync(); }
    await setup.Database.ExecuteSqlRawAsync(drop);
}
sealed class PresenceClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset value = now;
    public override DateTimeOffset GetUtcNow() => value;
    public void Advance(TimeSpan duration) => value += duration;
}
