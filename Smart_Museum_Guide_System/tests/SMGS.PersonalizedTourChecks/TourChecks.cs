using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

public static class TourChecks
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
    private static JsonElement Data(JsonElement value) => value.GetProperty("data");
    private static int[] StopIds(JsonElement p) => p.GetProperty("stops").EnumerateArray().Select(s => s.GetProperty("artifactId").GetInt32()).ToArray();

    public static async Task Run()
    {
        var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? throw new Exception("Set SMGS_TEST_CONNECTION; tests use an isolated temporary schema.");
        var tourKey = Environment.GetEnvironmentVariable("AI_TOUR_SERVICE_KEY") ?? throw new Exception("Set AI_TOUR_SERVICE_KEY for the running AI Tour service.");
        var schema = "smgs_personal_test_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs.ConnectionString).Options);
        await using var control = new NpgsqlConnection(connection); await control.OpenAsync();
        await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", control).ExecuteNonQueryAsync();
        WebApplication? host = null;
        try {
            await using (var setup = Open()) { await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript()); }
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = cs.ConnectionString,
                ["JwtSettings:Key"] = "Isolated-personal-tour-test-signing-key-1234567890",
                ["JwtSettings:Issuer"] = "personal-tour-test", ["JwtSettings:Audience"] = "personal-tour-test",
                ["JwtSettings:AccessTokenMinutes"] = "30",
                ["PersonalizedTours:BaseUrl"] = Environment.GetEnvironmentVariable("AI_TOUR_URL") ?? "http://127.0.0.1:8004/",
                ["PersonalizedTours:ServiceKey"] = tourKey,
            });
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllers().AddApplicationPart(typeof(PersonalizedToursController).Assembly);
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtSettings>>((o, s) => o.TokenValidationParameters = s.Value.CreateValidationParameters());
            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("personal-tour", l => { l.PermitLimit = 100; l.Window = TimeSpan.FromMinutes(1); }));
            host = builder.Build(); host.UseRouting(); host.UseAuthentication(); host.UseAuthorization(); host.UseRateLimiter(); host.MapControllers();
            var qr = new Dictionary<int, string>(); var ceramic = new List<int>(); var bronze = new List<int>();
            int museumId;
            using (var scope = host.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var email in new[] { "owner@personal.test", "other@personal.test" }) {
                    var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, FullName = "Khách kiểm thử", IsActive = true, CreatedAt = DateTime.UtcNow };
                    var created = await users.CreateAsync(user, "TourTest123");
                    if (!created.Succeeded) throw new Exception("Cannot prepare test user");
                    await users.AddToRoleAsync(user, RoleNames.Visitor);
                }
                var owner = await users.FindByEmailAsync("owner@personal.test") ?? throw new Exception();
                var museum = new Museum { Name = "Bảo tàng kiểm thử cá nhân", IsActive = true, CreatedAt = DateTime.UtcNow };
                db.Add(museum); await db.SaveChangesAsync(); museumId = museum.Id;
                var building = new Building { MuseumId = museumId, Name = "Tòa nhà kiểm thử" }; db.Add(building); await db.SaveChangesAsync();
                var floor = new Floor { BuildingId = building.Id, LevelNo = 1, Name = "Tầng kiểm thử" }; db.Add(floor); await db.SaveChangesAsync();
                var gallery = new Gallery { FloorId = floor.Id, Name = "Phòng kiểm thử" }; db.Add(gallery); await db.SaveChangesAsync();
                foreach (var (name, topicName, state, active, premium) in new[] {
                    ("Bình gốm hoa nâu", "Gốm sứ", "published", true, false), ("Bát men gốm", "Gốm sứ", "published", true, false),
                    ("Trống đồng", "Đồ đồng", "published", true, false), ("Thạp đồng", "Đồ đồng", "published", true, false),
                    ("Gốm nháp kín", "Gốm sứ", "draft", true, false), ("Gốm ngừng trưng bày", "Gốm sứ", "published", false, false),
                    ("Gốm premium chưa mua", "Gốm sứ", "published", true, true),
                }) {
                    var artifact = new Artifact { MuseumId = museumId, GalleryId = gallery.Id, Name = name, QrCode = Guid.NewGuid().ToString(), AccessLevel = premium ? "premium" : "free", IsActive = active, CreatedAt = DateTime.UtcNow };
                    db.Add(artifact); await db.SaveChangesAsync(); qr[artifact.Id] = artifact.QrCode;
                    var topic = await db.Topics.SingleOrDefaultAsync(t => t.Name == topicName) ?? new Topic { MuseumId = museumId, Name = topicName, IsActive = true, CreatedAt = DateTime.UtcNow };
                    if (topic.Id == 0) { db.Add(topic); await db.SaveChangesAsync(); }
                    db.Add(new ArtifactTopic { ArtifactId = artifact.Id, TopicId = topic.Id });
                    var content = new ArtifactContent { ArtifactId = artifact.Id, LanguageCode = "vi" }; db.Add(content); await db.SaveChangesAsync();
                    db.Add(new ArtifactContentVersion { ArtifactContentId = content.Id, Title = name, Description = name,
                        NarrationText = name, VersionNo = 1, Status = state, IsCurrent = state == "published", CreatedBy = owner.Id,
                        ReviewedBy = state == "published" ? owner.Id : null, PublishedAt = state == "published" ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow });
                    if (state == "published" && active && !premium) (topicName == "Gốm sứ" ? ceramic : bronze).Add(artifact.Id);
                }
                await db.SaveChangesAsync();
            }
            await host.StartAsync();
            var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };
            async Task<string> Login(string email) {
                var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TourTest123" });
                response.EnsureSuccessStatusCode(); return Data(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
            }
            var ownerToken = await Login("owner@personal.test"); var otherToken = await Login("other@personal.test");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            async Task<JsonElement> Recommend(string interest, int minutes = 30) {
                var r = await client.PostAsJsonAsync("/api/personalized-tours/recommendations", new { museumId, availableMinutes = minutes, interests = new[] { interest } });
                if (!r.IsSuccessStatusCode) throw new Exception($"Recommend failed {r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
                return Data(await r.Content.ReadFromJsonAsync<JsonElement>()).Clone();
            }
            var pottery = await Recommend("Gốm sứ");
            Check(StopIds(pottery).Order().SequenceEqual(ceramic.Order()), "AI uses interests and excludes draft, inactive and inaccessible premium artifacts");
            Check(pottery.GetProperty("estimatedMinutes").GetDouble() <= 30, "Preview respects the time budget");
            var id = pottery.GetProperty("id").GetGuid();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            Check((await client.PostAsJsonAsync("/api/personalized-tours", new { recommendationId = id })).StatusCode == HttpStatusCode.Conflict, "Other visitor cannot save this preview");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            var save1 = await client.PostAsJsonAsync("/api/personalized-tours", new { recommendationId = id });
            Check(save1.StatusCode == HttpStatusCode.Created, "Preview saved in PostgreSQL");
            Check((await client.PostAsJsonAsync("/api/personalized-tours", new { recommendationId = id })).StatusCode == HttpStatusCode.Created, "Save retry is idempotent");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            Check((await client.GetAsync($"/api/personalized-tours/{id}")).StatusCode == HttpStatusCode.NotFound, "Saved itinerary is private");
            Check((await client.PostAsJsonAsync("/api/visits", new { museumId, personalizedTourId = id })).StatusCode == HttpStatusCode.NotFound, "Other visitor cannot start saved itinerary");
            var freeVisit = await client.PostAsJsonAsync("/api/visits", new { museumId });
            Check(freeVisit.StatusCode == HttpStatusCode.Created, "Free visit remains an independent option");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            var bronzePlan = await Recommend("Đồ đồng");
            Check(StopIds(bronzePlan).Order().SequenceEqual(bronze.Order()), "Changing interests changes actual route stops");
            var saved = Data(await client.GetFromJsonAsync<JsonElement>($"/api/personalized-tours/{id}"));
            Check(StopIds(saved).SequenceEqual(StopIds(pottery)), "New recommendation does not mutate saved route");
            var noFit = await Recommend("Gốm sứ", 5);
            Check(StopIds(noFit).Length == 0, "Insufficient time returns an explicit empty recommendation");
            Check((await client.PostAsJsonAsync("/api/personalized-tours", new { recommendationId = noFit.GetProperty("id").GetGuid() })).StatusCode == HttpStatusCode.Conflict, "Empty recommendation cannot be saved");
            var fallback = await Recommend("Thiên thạch sao Hỏa");
            Check(fallback.GetProperty("usedFallback").GetBoolean(), "Unknown interests are labelled as fallback");
            var invalidStart = await client.PostAsJsonAsync("/api/visits", new { museumId, tourId = 123, personalizedTourId = id });
            Check(invalidStart.StatusCode == HttpStatusCode.BadRequest, "Cannot attach commercial and personal tours together");
            var start = await client.PostAsJsonAsync("/api/visits", new { museumId, personalizedTourId = id });
            Check(start.StatusCode == HttpStatusCode.Created, "Saved itinerary starts a real visit");
            var visitId = Data(await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            var progress = Data(await client.GetFromJsonAsync<JsonElement>($"/api/visits/{visitId}/progress"));
            Check(StopIds(progress).SequenceEqual(StopIds(pottery)), "Visit progress uses immutable personal stops");
            Check((await client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { })).StatusCode == HttpStatusCode.Conflict, "Cannot complete before scanning planned stops");
            foreach (var artifactId in StopIds(pottery)) {
                var scan = await client.PostAsJsonAsync("/api/recognitions/qr", new { qrCode = qr[artifactId], visitId, requestId = Guid.NewGuid() });
                Check(scan.IsSuccessStatusCode, "Real QR endpoint updates personal progress");
            }
            Check((await client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { })).IsSuccessStatusCode, "All scanned stops allow completion");
            await using (var db = Open()) {
                Check(await db.PersonalizedTours.CountAsync() == 1, "Only explicit save persists one route");
                var first = await db.Artifacts.SingleAsync(a => a.Id == ceramic[0]); first.IsActive = false; await db.SaveChangesAsync();
            }
            Check((await client.PostAsJsonAsync("/api/visits", new { museumId, personalizedTourId = id })).StatusCode == HttpStatusCode.Conflict, "Changed availability is rechecked before starting");
            Console.WriteLine($"PASS: {checks} checks across Flutter-compatible HTTP → BE → AI Tour → PostgreSQL; fixture schema only.");
        }
        finally {
            if (host is not null) { await host.StopAsync(); await host.DisposeAsync(); }
            if (!schema.StartsWith("smgs_personal_test_") || schema.Length != 51) throw new Exception("Unsafe test schema cleanup");
            await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", control).ExecuteNonQueryAsync();
        }
    }
}
