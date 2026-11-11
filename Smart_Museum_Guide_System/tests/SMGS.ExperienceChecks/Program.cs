using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL: " + message); }
static async Task Reject(Func<Task> action, int status) {
    try { await action(); throw new Exception("Expected status " + status); }
    catch (ExperienceException e) { Check(e.StatusCode == status, e.Message + " (status)"); }
}
foreach (var c in new[] { typeof(RecognitionController), typeof(VisitorNavigationController), typeof(VisitProgressController), typeof(DailyQuizController), typeof(QuizAttemptController) }) {
    Check(c.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleNames.Visitor, c.Name + " visitor only");
    Check(!c.GetMethods().Any(m => m.IsDefined(typeof(AllowAnonymousAttribute))), c.Name + " cannot bypass auth");
}
Check(typeof(QuestionBankController).GetMethod("Review")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles == RoleNames.Admin + "," + RoleNames.Curator, "Review is curator/admin only");
Check(typeof(GamificationController).GetMethod("Create")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles == RoleNames.Admin, "Only admin configures badges");
Check(DailyQuizService.LocalDate(new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc), "Asia/Ho_Chi_Minh") == new DateOnly(2026, 11, 2), "Vietnam day boundary");
var publicJson = JsonSerializer.Serialize(new AttemptQuestion(1, "Q", [new(1, "A")], null, null, null));
Check(!publicJson.Contains("IsCorrect\":true"), "No option answer key in visitor contract");
Console.WriteLine("Experience authorization, time-zone and public-contract checks passed.");
if (!args.Contains("--database")) return;

var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
    .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true).AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION.");
var schema = "smgs_experience_test_" + Guid.NewGuid().ToString("N");
var cs = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false, Timeout = 5 };
ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
var clock = new TestClock(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero));
var options = Options.Create(new ExperienceOptions { ScanPoints = 3, CorrectAnswerPoints = 5 });
GamificationService Rewards(ApplicationDbContext db) => new(db, clock, options);
QuizAttemptService Attempts(ApplicationDbContext db) => new(db, Rewards(db), new CommerceService(db), clock, options);
RecognitionService Recognition(ApplicationDbContext db) => new(db, new VisitProgressService(db, clock), new DailyQuizService(db, clock, options), Rewards(db), clock);
async Task<T> Run<T>(Func<ApplicationDbContext, Task<T>> action) { await using var db = Open(); return await action(db); }
await using var setup = Open();
var createSchema = $"CREATE SCHEMA \"{schema}\"";
var dropSchema = $"DROP SCHEMA \"{schema}\" CASCADE";
await setup.Database.ExecuteSqlRawAsync(createSchema);
try {
    var migrator = setup.GetService<IMigrator>();
    // Older migration histories support the legacy upgrade regression; squashed histories start at the current schema.
    if (setup.Database.GetMigrations().Contains("20261028090549_ConfirmProductValidityPolicy")) {
    await migrator.MigrateAsync("20261028090549_ConfirmProductValidityPolicy");
    var legacyUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "legacy", NormalizedEmail = "LEGACY@TEST" };
    var legacyMuseum = new Museum { Name = "Legacy museum" };
    setup.AddRange(legacyUser, legacyMuseum); await setup.SaveChangesAsync();
    var legacyVisit = new Visit { VisitorId = legacyUser.Id, MuseumId = legacyMuseum.Id, TourId = 123456, Status = VisitStatus.Completed, StartedAt = clock.GetUtcNow().UtcDateTime };
    setup.Add(legacyVisit); await setup.SaveChangesAsync();
    try { await migrator.MigrateAsync(); throw new Exception("Expected legacy TourId guard"); }
    catch (PostgresException e) when (e.SqlState == "P0001") { Check(e.MessageText.Contains("Legacy visits"), "Migration explains legacy relationship issue"); }
    Check(await setup.Visits.AsNoTracking().AnyAsync(v => v.Id == legacyVisit.Id && v.TourId == 123456), "Migration preserves legacy data on failure");
    // Only this isolated test fixture is reconciled; application data is never modified by the test.
    legacyVisit.TourId = null; await setup.SaveChangesAsync(); await migrator.MigrateAsync();
    Check(await setup.Visits.AnyAsync(v => v.Id == legacyVisit.Id), "Upgrade preserves visits");
    } else {
        await migrator.MigrateAsync();
    }
    await using (var creationDb = Open()) {
        var repo = new SMGS.Infrastructure.Repositories.MuseumRepository(creationDb);
        var standalone = await repo.CreateAsync(new Museum { Name = "Standalone creation" });
        Check(await setup.Museums.AnyAsync(m => m.Id == standalone.Id), "Standalone museum creation commits");
        var layouts = new IndoorMapService(creationDb);
        await layouts.SetLayout(standalone.Id, "single", Guid.Empty, true);
        Check((await layouts.Structure(standalone.Id, Guid.Empty, true)).Layout == "single", "Default structure allows initial single layout selection");
        await layouts.SetLayout(standalone.Id, "single", Guid.Empty, true);
        try {
            await layouts.SetLayout(standalone.Id, "floors", Guid.Empty, true);
            throw new Exception("Existing configured structure must prevent layout changes");
        } catch (IndoorMapException e) when (e.StatusCode == 409) { }
        var multi = await repo.CreateAsync(new Museum { Name = "Initial floors layout" });
        var initialFloor = (await layouts.Structure(multi.Id, Guid.Empty, true)).Floors.Single();
        var floorMapId = await layouts.Create(multi.Id, new(initialFloor.Id, "Ground floor"), Guid.Empty, true);
        Check(await setup.IndoorMaps.AnyAsync(m => m.Id == floorMapId && m.FloorId == initialFloor.Id), "Create floor map without prior layout selection");
        var structure = await layouts.Structure(multi.Id, Guid.Empty, true);
        Check(structure.Layout == "floors" && structure.Floors.Count == 1, "Initial floors selection preserves default floor");
        await using var outer = await creationDb.Database.BeginTransactionAsync();
        var nested = await repo.CreateAsync(new Museum { Name = "Multipart transaction" });
        var building = await creationDb.Buildings.SingleAsync(b => b.MuseumId == nested.Id);
        var floor = await creationDb.Floors.SingleAsync(f => f.BuildingId == building.Id);
        Check(creationDb.Database.CurrentTransaction == outer, "Repository preserves caller transaction");
        Check(!await setup.Museums.AnyAsync(m => m.Id == nested.Id), "Repository does not commit caller transaction");
        await outer.RollbackAsync();
        Check(!await setup.Museums.AnyAsync(m => m.Id == nested.Id)
            && !await setup.Buildings.AnyAsync(b => b.Id == building.Id)
            && !await setup.Floors.AnyAsync(f => f.Id == floor.Id), "Caller rollback removes museum and default structure");
    }
    var visitor = Guid.NewGuid(); var other = Guid.NewGuid(); var staff = Guid.NewGuid(); var curator = Guid.NewGuid();
    setup.Users.AddRange(new[] { visitor, other, staff, curator }.Select(id => new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedEmail = id + "@TEST" }));
    var museum = new Museum { Name = "Experience test" }; var foreign = new Museum { Name = "Other museum" };
    setup.AddRange(museum, foreign); await setup.SaveChangesAsync();
    setup.StaffAssignments.AddRange(new StaffAssignment { MuseumId = museum.Id, UserId = staff }, new StaffAssignment { MuseumId = museum.Id, UserId = curator });
    var a = new Artifact { MuseumId = museum.Id, Name = "A", QrCode = Guid.NewGuid().ToString(), AccessLevel = "free" };
    var b = new Artifact { MuseumId = museum.Id, Name = "B", QrCode = Guid.NewGuid().ToString(), AccessLevel = "free" };
    var c = new Artifact { MuseumId = foreign.Id, Name = "C", QrCode = Guid.NewGuid().ToString() };
    setup.AddRange(a, b, c); await setup.SaveChangesAsync();
    var tourRequest = new TourRequest("Tour", "Description", 30, [a.Id, b.Id]);
    await Reject(async () => await Run(db => new TourService(db).CreateAsync(museum.Id, tourRequest, other, false)), 403);
    await Reject(async () => await Run(db => new TourService(db).CreateAsync(museum.Id, tourRequest with { ArtifactIds = [a.Id, c.Id] }, staff, false)), 400);
    var tour = await Run(db => new TourService(db).CreateAsync(museum.Id, tourRequest, staff, false));
    await Reject(async () => await Run(db => new VisitProgressService(db, clock).StartVisitAsync(visitor, new() { MuseumId = museum.Id, TourId = tour.Id })), 400);
    await Run(db => new TourService(db).PublishAsync(tour.Id, tour.Revision, staff, false));
    var visit = await Run(db => new VisitProgressService(db, clock).StartVisitAsync(visitor, new() { MuseumId = museum.Id, TourId = tour.Id }));
    await Reject(async () => await Run(db => new VisitProgressService(db, clock).CompleteTourAsync(visitor, visit.Id)), 409);
    await Reject(async () => await Run(db => new VisitProgressService(db, clock).GetProgressAsync(other, visit.Id)), 404);
    await Reject(async () => await Run(db => new TourService(db).UpdateStopsAsync(tour.Id, new(2, tourRequest), staff, false)), 409);
    var free = await Run(db => new VisitProgressService(db, clock).StartVisitAsync(other, new() { MuseumId = museum.Id }));
    Check((await Run(db => new VisitProgressService(db, clock).CompleteTourAsync(other, free.Id))).Status == VisitStatus.Completed, "Free visit completes with zero scans");
    var qids = new List<int>();
    for (var i = 0; i < 4; i++) {
        var q = await Run(db => new QuestionBankService(db, clock).CreateAsync(a.Id, new("Question " + i, "Explanation", "vi", [new("Correct", true), new("Wrong", false)]), staff, false));
        await Run(db => new QuestionBankService(db, clock).ReviewQuestionAsync(q.Id, new(true, null), curator, false)); qids.Add(q.Id);
    }
    await Reject(async () => await Run(db => new QuestionBankService(db, clock).UpdateAsync(qids[0], new("Changed", "", "vi", [new("C", true), new("W", false)]), staff, false)), 409);
    var quiz = await Run(db => new QuestionBankService(db, clock).CreateQuizAsync(a.Id, new("Quiz", qids.ToArray(), 2), staff, false));
    await Run(db => new QuestionBankService(db, clock).PublishQuizAsync(quiz.Id, curator, false));
    var badge = await Run(db => Rewards(db).CreateBadgeAsync(new("SCAN_2", "Two artifacts", museum.Id, "scan_artifacts", 2)));
    var qr = new ScanQrRequest(a.QrCode!, visit.Id, Guid.NewGuid());
    var scans = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Run(db => Recognition(db).RecordQrAsync(qr, visitor))));
    Check(scans.Select(s => s.RecognitionId).Distinct().Count() == 1, "Concurrent retries create one recognition");
    Check(await setup.Set<DailyQuizEntry>().CountAsync() == 1, "One daily unlock");
    Check(await setup.Set<PointTransaction>().CountAsync() == 1, "One scan award");
    await Reject(async () => await Run(db => Recognition(db).RecordQrAsync(qr with { QrCode = b.QrCode! }, visitor)), 409);
    await Reject(async () => await Run(db => Recognition(db).RecordQrAsync(new(c.QrCode!, visit.Id, Guid.NewGuid()), visitor)), 400);
    var state = await Run(db => new VisitProgressService(db, clock).GetProgressAsync(visitor, visit.Id));
    Check(state.NextArtifactId == b.Id && state.Stops.Count(s => s.LastVisitedAt != null) == 1, "QR updates correct stop");
    var nodeA = Guid.NewGuid(); var nodeB = Guid.NewGuid();
    var map = new IndoorMap { MuseumId = museum.Id, Name = "Published map", PublishedAt = clock.GetUtcNow().UtcDateTime };
    map.PublishedJson = JsonSerializer.Serialize(new MapDocument { MuseumId = museum.Id, Revision = 7, ImageWidth = 100, ImageHeight = 100,
        Nodes = [new() { Id = nodeA, Label = "A", X = 0, Y = 0 }, new() { Id = nodeB, Label = "B", X = 1, Y = 0 }],
        Markers = [new() { Id = Guid.NewGuid(), Label = "A", ArtifactId = a.Id, NodeId = nodeA }, new() { Id = Guid.NewGuid(), Label = "B", ArtifactId = b.Id, NodeId = nodeB }],
        Edges = [new() { Id = Guid.NewGuid(), FromNodeId = nodeA, ToNodeId = nodeB }] });
    setup.Add(map); await setup.SaveChangesAsync();
    var route = await Run(db => new VisitorNavigationService(db, new VisitProgressService(db, clock)).RouteToArtifactAsync(visitor, visit.Id, map.Id, null, false));
    Check(route.Revision == 7 && route.Route.NodeIds.SequenceEqual(new[] { nodeA, nodeB }), "Route uses published snapshot, not empty draft graph");
    var secondScan = await Run(db => Recognition(db).RecordQrAsync(new(b.QrCode!, visit.Id, Guid.NewGuid()), visitor));
    Check(secondScan.NewBadges.Any(x => x.Id == badge.Id), "Scan returns newly awarded badge");
    Check((await Run(db => new VisitProgressService(db, clock).CompleteTourAsync(visitor, visit.Id))).Status == VisitStatus.Completed, "Tour completes after all stops");
    Check(await setup.Set<UserBadge>().CountAsync(x => x.UserId == visitor && x.BadgeId == badge.Id) == 1, "One badge grant");
    var entry = await setup.Set<DailyQuizEntry>().SingleAsync();
    await Reject(async () => await Run(db => Attempts(db).StartAsync(new(entry.Id), other)), 404);
    await Reject(async () => await Run(async db => { await new DailyQuizService(db, clock, options).SetDeferredAsync(entry.Id, true, other); return true; }), 404);
    await Run(async db => { await new DailyQuizService(db, clock, options).SetDeferredAsync(entry.Id, true, visitor); return true; });
    await Run(async db => { await new DailyQuizService(db, clock, options).SetDeferredAsync(entry.Id, true, visitor); return true; });
    Check((await Run(db => new DailyQuizService(db, clock, options).GetTodayAsync(visitor))).Single().Status == "deferred", "Deferred quiz persists and repeat save is safe");
    await Run(async db => { await new DailyQuizService(db, clock, options).SetDeferredAsync(entry.Id, false, visitor); return true; });
    Check((await Run(db => new DailyQuizService(db, clock, options).GetTodayAsync(visitor))).Single().Status == "pending", "Remove deferred flag");
    var attempt = await Run(db => Attempts(db).StartAsync(new(entry.Id), visitor));
    await Reject(async () => await Run(async db => { await new DailyQuizService(db, clock, options).SetDeferredAsync(entry.Id, true, visitor); return true; }), 409);
    Check(attempt.Questions.All(q => q.IsCorrect is null && q.Explanation is null), "No answer leak before submission");
    var first = attempt.Questions[0]; var correct = await setup.Set<QuestionOption>().SingleAsync(o => o.QuestionId == first.Id && o.IsCorrect);
    var foreignOption = await setup.Set<QuestionOption>().FirstAsync(o => o.QuestionId != first.Id);
    await Reject(async () => await Run(db => Attempts(db).AnswerAsync(attempt.Id, new(first.Id, foreignOption.Id), visitor)), 400);
    await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Run(db => Attempts(db).AnswerAsync(attempt.Id, new(first.Id, correct.Id), visitor))));
    Check(await setup.Set<PointTransaction>().CountAsync(p => p.RefType == "question" && p.RefId == first.Id) == 1, "Concurrent answers award once");
    var wrong = await setup.Set<QuestionOption>().SingleAsync(o => o.QuestionId == first.Id && !o.IsCorrect);
    await Reject(async () => await Run(db => Attempts(db).AnswerAsync(attempt.Id, new(first.Id, wrong.Id), visitor)), 409);
    var second = attempt.Questions[1]; var correct2 = await setup.Set<QuestionOption>().SingleAsync(o => o.QuestionId == second.Id && o.IsCorrect);
    var done = await Run(db => Attempts(db).AnswerAsync(attempt.Id, new(second.Id, correct2.Id), visitor));
    Check(done.PointsEarned == 10, "Returns awarded points for both correct answers");
    Check(done.Status == "completed" && done.Score == 100, "Complete and score attempt");
    var reset = await Run(db => Attempts(db).ResetAsync(attempt.Id, visitor));
    Check(!reset.Questions.Select(q => q.Id).Intersect(attempt.Questions.Select(q => q.Id)).Any(), "Reset selects new questions");
    Check((await Run(db => Attempts(db).GetResultsAsync(attempt.Id, visitor))).Status == "completed", "Reset preserves results");
    Check((await Run(db => Attempts(db).ResetAsync(attempt.Id, visitor))).Id == reset.Id, "Reset retry returns successor");
    var resetAgain = await Run(db => Attempts(db).ResetAsync(reset.Id, visitor));
    Check((await Run(db => Attempts(db).GetResultsAsync(reset.Id, visitor))).Status == "abandoned", "Reset preserves partially answered attempt");
    foreach (var question in resetAgain.Questions) {
        var opt = await setup.Set<QuestionOption>().SingleAsync(o => o.QuestionId == question.Id && o.IsCorrect);
        await Run(db => Attempts(db).AnswerAsync(resetAgain.Id, new(question.Id, opt.Id), visitor));
    }
    Check(await setup.Set<PointTransaction>().CountAsync(p => p.RefType == "question") == 2, "Repeated questions never reward twice");
    clock.Now = clock.Now.AddDays(1);
    Check((await Run(db => new DailyQuizService(db, clock, options).GetTodayAsync(visitor))).Count == 0, "New day has empty daily list");
    await Reject(async () => await Run(db => Attempts(db).ResetAsync(resetAgain.Id, visitor)), 409);
    await Run(db => Recognition(db).RecordQrAsync(new(a.QrCode!, null, Guid.NewGuid()), visitor));
    Check(await setup.Set<DailyQuizEntry>().CountAsync() == 2 && await setup.Set<PointTransaction>().CountAsync(p => p.RefType == "artifact") == 2, "New day unlocks; no repeated scan points");
    Check(await setup.Set<QuizAttempt>().CountAsync() == 3, "Historical attempts retained across midnight");
    var todayEntry = await setup.Set<DailyQuizEntry>().OrderByDescending(x => x.Id).FirstAsync();
    await HttpChecks.Run(cs.ConnectionString, clock, visitor, staff, curator, a.Id, museum.Id, tour.Id, todayEntry.Id);
    var activeToday = await setup.Set<QuizAttempt>().SingleAsync(x => x.DailyEntryId == todayEntry.Id && x.Status == "in_progress");
    a.AccessLevel = "premium"; await setup.SaveChangesAsync();
    await Reject(async () => await Run(db => Attempts(db).StartAsync(new(todayEntry.Id), visitor)), 403);
    await Reject(async () => await Run(db => Attempts(db).GetResultsAsync(activeToday.Id, visitor)), 403);
    a.AccessLevel = "free"; await setup.SaveChangesAsync();
    Console.WriteLine("Experience database checks passed: scope, tour/free visits, QR concurrency, published routing, quiz ownership, scoring/reset, rewards and day rollover.");
}
finally {
    setup.ChangeTracker.Clear();
    await setup.Database.ExecuteSqlRawAsync(dropSchema);
}

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
