using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMGS.Domain.Entities;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION; this test uses its own temporary schema.");
var schema = "smgs_seed_test_" + Guid.NewGuid().ToString("N");
await using var control = new NpgsqlConnection(connection);
await control.OpenAsync();
await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", control).ExecuteNonQueryAsync();
try
{
    var scopedConnection = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false }.ConnectionString;
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:DefaultConnection"] = scopedConnection,
        ["CloudflareR2:SeedDemoMedia"] = "false"
    }).Build();
    var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config);
    services.AddInfrastructure(config);
    await using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
    // A pre-existing unrelated museum used to suppress all development data.
    db.Museums.Add(new Museum { Name = "Bảo tàng của người dùng", IsActive = false });
    await db.SaveChangesAsync();
    await DataSeeder.SeedDevelopmentDataAsync(provider);
    db.ChangeTracker.Clear();
    void Check(bool condition, string label) {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }
    Check(await db.Users.CountAsync() == 5 && await db.Museums.CountAsync() == 2, "Existing museum does not suppress demo seed or get changed");
    Check(await db.Artifacts.CountAsync() == 8 && await db.ArtifactContentVersions.CountAsync() == 10, "Complete artifacts, narration and review fixture created");
    Check(await db.Tours.CountAsync() == 2 && await db.TourVersions.CountAsync() == 3, "Two public tour versions and one review version created");
    var version = await db.ArtifactContentVersions.OrderBy(x => x.Id).FirstAsync();
    version.Title = "Nội dung đã chỉnh sửa cần giữ nguyên";
    await db.SaveChangesAsync();
    var originalCounts = (await db.StaffAssignments.CountAsync(), await db.ArtifactTopics.CountAsync(), await db.ArtifactContents.CountAsync());
    await DataSeeder.SeedDevelopmentDataAsync(provider);
    db.ChangeTracker.Clear();
    Check(await db.Artifacts.CountAsync() == 8 && await db.ArtifactContentVersions.CountAsync() == 10 &&
        await db.Tours.CountAsync() == 2 && await db.TourVersions.CountAsync() == 3 &&
        originalCounts == (await db.StaffAssignments.CountAsync(), await db.ArtifactTopics.CountAsync(), await db.ArtifactContents.CountAsync()),
        "Repeated seed adds no duplicate content, assignments, topics or tours");
    Check(await db.ArtifactContentVersions.AnyAsync(x => x.Title == "Nội dung đã chỉnh sửa cần giữ nguyên") &&
        await db.Museums.AnyAsync(x => x.Name == "Bảo tàng của người dùng" && !x.IsActive), "Edited content and unrelated user museum preserved");
    Check(await db.Artifact3DModels.CountAsync() == 0 && await db.ArtifactMedias.CountAsync() == 0,
        "Database seed does not upload artificial media with media flag disabled");
    var museum = await db.Museums.SingleAsync(x => x.Name == "Bảo tàng Lịch sử Quốc gia");
    var tours = new TourService(db);
    var publicTours = await tours.ListAsync(museum.Id, false, null, false, 1, 20);
    Check(publicTours.Count == 2 && publicTours.All(t => t.Versions.Count == 1 && t.Versions.All(v => v.IsPublicAt(DateTime.UtcNow))),
        "Visitor tour list never includes the submitted seed version");
    var publicDetail = await tours.GetAsync(publicTours.First().Id, null, false);
    Check(publicDetail.Versions.All(v => v.IsPublicAt(DateTime.UtcNow)), "Visitor tour detail excludes unpublished versions");
    var curator = await db.Users.SingleAsync(x => x.Email == "curator@museum.vn");
    var managedTours = await tours.ListAsync(museum.Id, true, curator.Id, false, 1, 20);
    Check(managedTours.Any(t => t.Versions.Any(v => v.Status == "submitted")), "Assigned curator can still see pending review data");
}
finally
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^smgs_seed_test_[a-f0-9]{32}$"))
        throw new Exception("Refusing unsafe test cleanup");
    await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", control).ExecuteNonQueryAsync();
}
