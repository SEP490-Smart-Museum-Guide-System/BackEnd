using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SMGS.Application.Services;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Repositories;

/// <summary>
/// Chạy với PostgreSQL thật trong một schema ngẫu nhiên, xóa trong finally.
/// Schema được tạo từ model hiện tại (GenerateCreateScript), không phụ thuộc lịch sử migration.
/// </summary>
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

        var schema = "smgs_feedback_test_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);

        await using var setup = Open();
        await setup.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());

            var visitor = new ApplicationUser { Id = Guid.NewGuid(), UserName = "v", Email = "v@x.vn", FullName = "Visitor" };
            var staff = new ApplicationUser { Id = Guid.NewGuid(), UserName = "s", Email = "s@x.vn", FullName = "Staff" };
            setup.Users.AddRange(visitor, staff);
            var museumA = new Museum { Name = "A", IsActive = true };
            var museumB = new Museum { Name = "B", IsActive = true };
            setup.Museums.AddRange(museumA, museumB);
            await setup.SaveChangesAsync();

            // Luồng đầy đủ qua repository thật
            int visitId, feedbackId;
            await using (var db = Open())
            {
                var visitService = new VisitService(new VisitRepository(db), new MuseumRepository(db));
                var feedbackService = new FeedbackService(new FeedbackRepository(db), new VisitRepository(db));

                var v = await visitService.StartAsync(visitor.Id, new() { MuseumId = museumA.Id });
                Check(v.StatusCode == 201, $"Start visit: {v.StatusCode} {v.Message}");
                visitId = v.Data!.Id;
                Check((await visitService.CompleteAsync(visitId, visitor.Id)).StatusCode == 200, "Complete visit.");

                var f = await feedbackService.CreateAsync(visitor.Id, new() { VisitId = visitId, OverallRating = 4, Comment = "Tốt" });
                Check(f.StatusCode == 201 && f.Data!.MuseumId == museumA.Id && f.Data.VisitorId == visitor.Id, "Feedback lưu và map được Visit.");
                feedbackId = f.Data.Id;

                var r = await feedbackService.RespondAsync(feedbackId, staff.Id, new() { Content = "Cảm ơn" });
                Check(r.StatusCode == 200, $"Staff response: {r.StatusCode} {r.Message}");
            }

            await using (var db = Open())
            {
                var feedbackService = new FeedbackService(new FeedbackRepository(db), new VisitRepository(db));
                var mine = await feedbackService.GetByVisitAsync(visitId, visitor.Id);
                Check(mine.Data?.StaffResponse?.Content == "Cảm ơn", "Include StaffResponse khi đọc lại từ DB.");
                Check((await feedbackService.GetByMuseumAsync(museumA.Id, 1, 10)).Data!.TotalCount == 1, "Lọc theo bảo tàng A.");
                Check((await feedbackService.GetByMuseumAsync(museumB.Id, 1, 10)).Data!.TotalCount == 0, "Bảo tàng B không thấy feedback của A.");

                var visitService = new VisitService(new VisitRepository(db), new MuseumRepository(db));
                Check((await visitService.GetByIdAsync(visitId, visitor.Id)).Data!.HasFeedback, "Visit báo HasFeedback.");
            }

            // Ràng buộc DB: unique VisitId, check constraint rating, FK user
            await using (var db = Open())
            {
                db.VisitFeedbacks.Add(new VisitFeedback { VisitId = visitId, OverallRating = 5 });
                await ExpectDbError(db, "Unique index phải chặn feedback thứ 2 cho cùng Visit.");
            }
            await using (var db = Open())
            {
                var visit2 = new Visit { VisitorId = visitor.Id, MuseumId = museumB.Id, Status = VisitStatus.Completed };
                db.Visits.Add(visit2);
                await db.SaveChangesAsync();
                db.VisitFeedbacks.Add(new VisitFeedback { VisitId = visit2.Id, OverallRating = 9 });
                await ExpectDbError(db, "Check constraint phải chặn rating 9.");
            }
            await using (var db = Open())
            {
                db.Complaints.Add(new Complaint { VisitorId = Guid.NewGuid(), Subject = "x", Body = "x" });
                await ExpectDbError(db, "FK phải chặn complaint của user không tồn tại.");
            }

            // Complaint qua repository thật
            await using (var db = Open())
            {
                var service = new ComplaintService(new ComplaintRepository(db));
                var c = await service.CreateAsync(visitor.Id, new() { Subject = "Lỗi", Body = "Không quét được QR" });
                Check(c.StatusCode == 201, "Tạo complaint.");
                Check((await service.AssignToMeAsync(c.Data!.Id, staff.Id)).StatusCode == 200, "Assign complaint.");
                var list = await service.GetAllAsync(null, staff.Id, 1, 10);
                Check(list.Data!.TotalCount == 1, "Lọc complaint theo người xử lý.");
            }

            Console.WriteLine("PASS (PostgreSQL): luồng visit → feedback → staff response, lọc theo bảo tàng, unique/check/FK constraints, complaint.");
        }
        finally
        {
            await setup.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE");
        }
    }

    private static async Task ExpectDbError(DbContext db, string message)
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return; }
        throw new Exception("FAIL: " + message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
