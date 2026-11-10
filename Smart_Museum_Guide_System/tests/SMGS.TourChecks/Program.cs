using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.Visit;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

static void Check(bool ok, string message)
{
    if (!ok)
    {
        throw new Exception("FAIL: " + message);
    }
}

static void CheckThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
        throw new Exception("FAIL (no exception): " + message);
    }
    catch (TException)
    {
    }
}

static async Task Reject(Func<Task> action, int expectedStatus, string message)
{
    try
    {
        await action();
        throw new Exception($"FAIL ({message}): Expected HTTP {expectedStatus} but succeeded.");
    }
    catch (ExperienceException e)
    {
        Check(e.StatusCode == expectedStatus, $"{message} (got {e.StatusCode}, expected {expectedStatus}: {e.Message})");
    }
}

// =============================================================================
// 1. KIỂM THỬ PHÂN QUYỀN CONTROLLER & STATE MACHINE / THUẬT TOÁN GỢI Ý (IN-MEMORY)
//    Chạy mặc định trong CI không cần kết nối DB ngoài.
// =============================================================================

// TC-015 (phần RBAC trên Controller):
// Chỉ Curator/Admin mới được Approve hoặc Reject; chỉ Staff/Admin mới được tạo/sửa/gửi duyệt.
var approveAttr = typeof(TourController).GetMethod(nameof(TourController.ApproveVersion))!.GetCustomAttribute<AuthorizeAttribute>()!;
var rejectAttr = typeof(TourController).GetMethod(nameof(TourController.RejectVersion))!.GetCustomAttribute<AuthorizeAttribute>()!;
var submitAttr = typeof(TourController).GetMethod(nameof(TourController.SubmitVersion))!.GetCustomAttribute<AuthorizeAttribute>()!;
var createDraftAttr = typeof(TourController).GetMethod(nameof(TourController.CreateVersionDraft))!.GetCustomAttribute<AuthorizeAttribute>()!;
var updateDraftAttr = typeof(TourController).GetMethod(nameof(TourController.UpdateVersionDraft))!.GetCustomAttribute<AuthorizeAttribute>()!;
var saveRouteAttr = typeof(TourController).GetMethod(nameof(TourController.SaveRoute))!.GetCustomAttribute<AuthorizeAttribute>()!;

Check(approveAttr.Roles == RoleNames.Admin + "," + RoleNames.Curator, "TC-015: ApproveVersion requires Curator or Admin role");
Check(rejectAttr.Roles == RoleNames.Admin + "," + RoleNames.Curator, "TC-015: RejectVersion requires Curator or Admin role");
Check(submitAttr.Roles == RoleNames.Admin + "," + RoleNames.MuseumStaff, "TC-015: SubmitVersion requires MuseumStaff or Admin role");
Check(createDraftAttr.Roles == RoleNames.Admin + "," + RoleNames.MuseumStaff, "TC-015: CreateVersionDraft requires MuseumStaff or Admin role");
Check(updateDraftAttr.Roles == RoleNames.Admin + "," + RoleNames.MuseumStaff, "TC-015: UpdateVersionDraft requires MuseumStaff or Admin role");
Check(saveRouteAttr.Roles == RoleNames.Visitor, "TC-019: SaveRoute requires Visitor role");

// TC-024: Chuyển trạng thái hợp lệ Draft -> Submitted -> Approved và Draft -> Submitted -> Rejected.
var staffUser = Guid.NewGuid();
var curatorUser = Guid.NewGuid();
var t0 = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
var tPublic = t0.AddHours(2);

var vApprove = new TourVersion
{
    TourId = 1,
    VersionNo = 1,
    Title = "Gốm sứ Việt Nam",
    Description = "Bản v1",
    EstimatedMinutes = 45,
    CreatedBy = staffUser,
    CreatedAt = t0
};
Check(vApprove.Status == TourVersionStatuses.Draft, "TC-024: Initial status is draft");
vApprove.Submit(staffUser, t0.AddMinutes(5));
Check(vApprove.Status == TourVersionStatuses.Submitted && vApprove.SubmittedBy == staffUser, "TC-024: Draft -> Submitted records staff & timestamp");
vApprove.Approve(curatorUser, tPublic, "Nội dung chuẩn xác", t0.AddMinutes(10));
Check(
    vApprove.Status == TourVersionStatuses.Approved &&
    vApprove.ReviewedBy == curatorUser &&
    vApprove.PublicAt == tPublic &&
    vApprove.ReviewNote == "Nội dung chuẩn xác",
    "TC-024: Submitted -> Approved records curator, PublicAt and note");

var vReject = new TourVersion
{
    TourId = 1,
    VersionNo = 2,
    Title = "Bản lỗi",
    Description = "Mô tả",
    EstimatedMinutes = 30,
    CreatedBy = staffUser,
    CreatedAt = t0
};
vReject.Submit(staffUser, t0.AddMinutes(5));
vReject.Reject(curatorUser, "Thiếu thông tin niên đại", t0.AddMinutes(10));
Check(
    vReject.Status == TourVersionStatuses.Rejected &&
    vReject.PublicAt is null &&
    vReject.ReviewNote == "Thiếu thông tin niên đại",
    "TC-024: Submitted -> Rejected clears PublicAt and saves note");

// TC-025: Mọi chuyển trạng thái sai quy trình hoặc tự duyệt đều bị chặn.
var vInvalid = new TourVersion { TourId = 1, VersionNo = 3, CreatedBy = staffUser };
CheckThrows<InvalidOperationException>(
    () => vInvalid.Approve(curatorUser, t0, null, t0),
    "TC-025: Cannot approve a Draft version directly");
CheckThrows<InvalidOperationException>(
    () => vInvalid.Reject(curatorUser, "Lý do", t0),
    "TC-025: Cannot reject a Draft version directly");

vInvalid.Submit(staffUser, t0);
CheckThrows<InvalidOperationException>(
    () => vInvalid.Submit(staffUser, t0),
    "TC-025: Cannot submit an already Submitted version");
CheckThrows<InvalidOperationException>(
    () => vInvalid.Approve(staffUser, t0, null, t0),
    "TC-015/TC-025: Submitter cannot self-approve their own TourVersion");
CheckThrows<ArgumentException>(
    () => vInvalid.Reject(curatorUser, "   ", t0),
    "TC-025: Rejecting without a reason note is forbidden");
CheckThrows<InvalidOperationException>(
    () => vApprove.Submit(staffUser, t0),
    "TC-025: Cannot re-submit an Approved version");
CheckThrows<InvalidOperationException>(
    () => vReject.Approve(curatorUser, t0, null, t0),
    "TC-025: Cannot approve a Rejected version");

// TC-016 & TC-026: Kiểm tra hiển thị theo PublicAt và trạng thái Rejected/Draft/Submitted.
Check(!vApprove.IsPublicAt(tPublic.AddSeconds(-1)), "TC-016/TC-026: Future Approved version (before PublicAt) is invisible");
Check(vApprove.IsPublicAt(tPublic), "TC-014/TC-026: Approved version becomes public at exact PublicAt");
Check(vApprove.IsPublicAt(tPublic.AddMinutes(30)), "TC-014/TC-026: Approved version remains public after PublicAt");
Check(!vReject.IsPublicAt(tPublic.AddDays(1)), "TC-016: Rejected version is never public");
Check(!vInvalid.IsPublicAt(tPublic.AddDays(1)), "TC-016: Submitted (unapproved) version is never public");

// TC-017: Gợi ý tour theo sở thích (TopicIds) và thời gian có sẵn (AvailableMinutes).
var topicNames = new Dictionary<int, string>
{
    [1] = "Gốm sứ",
    [2] = "Triều Nguyễn",
    [3] = "Chiến tranh"
};

var candidates = new List<TourRecommender.CandidateTour>
{
    new(
        TourId: 10,
        TourVersionId: 101,
        VersionNo: 1,
        Title: "Tinh hoa Gốm sứ & Cung đình",
        Description: "Khám phá gốm sứ và cổ vật Triều Nguyễn",
        EstimatedMinutes: 50,
        ArtifactIds: [1, 2, 3],
        TopicIds: [1, 2],
        TopicNames: topicNames,
        AlreadyCompletedByVisitor: false,
        CompletedVisitCount: 5),
    new(
        TourId: 20,
        TourVersionId: 201,
        VersionNo: 1,
        Title: "Chuyên đề Gốm cổ chuyên sâu",
        Description: "Hành trình dài 90 phút",
        EstimatedMinutes: 90,
        ArtifactIds: [1, 4, 5, 6],
        TopicIds: [1],
        TopicNames: topicNames,
        AlreadyCompletedByVisitor: false,
        CompletedVisitCount: 10),
    new(
        TourId: 30,
        TourVersionId: 301,
        VersionNo: 1,
        Title: "Lịch sử kháng chiến",
        Description: "Hiện vật thời kỳ chiến tranh",
        EstimatedMinutes: 30,
        ArtifactIds: [7, 8],
        TopicIds: [3],
        TopicNames: topicNames,
        AlreadyCompletedByVisitor: false,
        CompletedVisitCount: 20)
};

// Khách thích Gốm sứ (1) + Triều Nguyễn (2), có 60 phút -> Tour 20 (90p) bị loại, Tour 10 khớp 2/2 chủ đề đứng đầu.
var rec1 = TourRecommender.Recommend(candidates, [1, 2], availableMinutes: 60);
Check(rec1.FallbackCode is null && rec1.Recommendations.Count == 1, "TC-017: Only matching tours within time budget are returned");
Check(rec1.Recommendations[0].TourVersionId == 101 && rec1.Recommendations[0].InterestScore == 1.0, "TC-017: Tour matching both topics ranks #1 with InterestScore = 1.0");
Check(rec1.Recommendations[0].Explanation.Contains("Gốm sứ") && rec1.Recommendations[0].Explanation.Contains("50/60"), "TC-017: Recommendation includes human-readable explanation");

// Khi khách có 120 phút và chỉ thích Gốm sứ (1): cả Tour 10 và Tour 20 đều khớp, nhưng Tour 20 khớp 1/1 chủ đề thuần túy (Jaccard 1.0 > 0.5) nên lên #1.
var rec2 = TourRecommender.Recommend(candidates, [1], availableMinutes: 120);
Check(rec2.Recommendations.Count == 2 && rec2.Recommendations[0].TourVersionId == 201, "TC-017: Changing available minutes and interests changes the selected top route");

// TC-018: Kiểm tra cả 3 trường hợp Fallback có mã lý do và giải thích rõ ràng.
var fbEmpty = TourRecommender.Recommend([], [1], availableMinutes: 60);
Check(fbEmpty.FallbackCode == TourRecommender.FallbackNoPublicTour && !string.IsNullOrWhiteSpace(fbEmpty.FallbackExplanation), "TC-018: NO_PUBLIC_TOUR fallback when museum has no public tours");

var fbShort = TourRecommender.Recommend(candidates, [1, 2], availableMinutes: 15);
Check(
    fbShort.FallbackCode == TourRecommender.FallbackTimeTooShort &&
    fbShort.FallbackSuggestion?.TourVersionId == 301 &&
    fbShort.FallbackExplanation!.Contains("15 phút"),
    "TC-018: TIME_TOO_SHORT fallback suggests the shortest available tour and explains extra minutes needed");

var fbNoMatch = TourRecommender.Recommend(candidates, [999], availableMinutes: 60);
Check(
    fbNoMatch.FallbackCode == TourRecommender.FallbackNoInterestMatch &&
    fbNoMatch.FallbackSuggestion?.TourVersionId == 301 &&
    !string.IsNullOrWhiteSpace(fbNoMatch.FallbackExplanation),
    "TC-018: NO_INTEREST_MATCH fallback suggests the most popular fitting tour with explanation");

Console.WriteLine("WF-02 unit & authorization checks passed (TC-014..TC-018, TC-024..TC-026).");

if (!args.Contains("--database"))
{
    return;
}

// =============================================================================
// 2. KIỂM THỬ TÍCH HỢP DATABASE & END-TO-END (TC-014..TC-026 đầy đủ trên PostgreSQL)
// =============================================================================

var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
var config = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(api, "appsettings.json"))
    .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
    .AddUserSecrets("smgs-api-local-development")
    .AddEnvironmentVariables()
    .Build();

var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION")
    ?? config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION.");

var schema = "smgs_tour_test_" + Guid.NewGuid().ToString("N");
var cs = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false, Timeout = 5 };
ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs.ConnectionString).Options);

var clock = new TestClock(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
var expOptions = Options.Create(new ExperienceOptions { ScanPoints = 3, CorrectAnswerPoints = 5 });

TourService Tours(ApplicationDbContext db) => new(db, clock);
VisitProgressService Progress(ApplicationDbContext db) => new(db, clock);
RecognitionService Recognition(ApplicationDbContext db) => new(
    db,
    Progress(db),
    new DailyQuizService(db, clock, expOptions),
    new GamificationService(db, clock, expOptions),
    clock);

async Task<T> Run<T>(Func<ApplicationDbContext, Task<T>> action)
{
    await using var db = Open();
    return await action(db);
}

await using var setup = Open();
var createSchemaSql = $"CREATE SCHEMA \"{schema}\"";
var dropSchemaSql = $"DROP SCHEMA \"{schema}\" CASCADE";
await setup.Database.ExecuteSqlRawAsync(createSchemaSql);

try
{
    await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());

    var staff = Guid.NewGuid();
    var curator = Guid.NewGuid();
    var foreignCurator = Guid.NewGuid();
    var visitorA = Guid.NewGuid();
    var visitorB = Guid.NewGuid();

    setup.Users.AddRange(new[] { staff, curator, foreignCurator, visitorA, visitorB }.Select(id =>
        new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedEmail = id + "@TEST" }));

    var museum = new Museum { Name = "Bảo tàng Lịch sử TP.HCM", IsActive = true };
    var otherMuseum = new Museum { Name = "Bảo tàng Mỹ thuật", IsActive = true };
    setup.Museums.AddRange(museum, otherMuseum);
    await setup.SaveChangesAsync();

    setup.StaffAssignments.AddRange(
        new StaffAssignment { MuseumId = museum.Id, UserId = staff },
        new StaffAssignment { MuseumId = museum.Id, UserId = curator },
        new StaffAssignment { MuseumId = otherMuseum.Id, UserId = foreignCurator });

    var topicCeramics = new Topic { MuseumId = museum.Id, Name = "Gốm sứ", IsActive = true };
    var topicDynasty = new Topic { MuseumId = museum.Id, Name = "Triều Nguyễn", IsActive = true };
    var topicWar = new Topic { MuseumId = museum.Id, Name = "Chiến tranh", IsActive = true };
    setup.Topics.AddRange(topicCeramics, topicDynasty, topicWar);

    var art1 = new Artifact { MuseumId = museum.Id, Name = "Bình gốm Chu Đậu", QrCode = "QR-ART-1", AccessLevel = "free", IsActive = true };
    var art2 = new Artifact { MuseumId = museum.Id, Name = "Ấn vàng Triều Nguyễn", QrCode = "QR-ART-2", AccessLevel = "free", IsActive = true };
    var art3 = new Artifact { MuseumId = museum.Id, Name = "Đĩa ngọc Hoàng cung", QrCode = "QR-ART-3", AccessLevel = "free", IsActive = true };
    setup.Artifacts.AddRange(art1, art2, art3);
    await setup.SaveChangesAsync();

    setup.ArtifactTopics.AddRange(
        new ArtifactTopic { ArtifactId = art1.Id, TopicId = topicCeramics.Id },
        new ArtifactTopic { ArtifactId = art2.Id, TopicId = topicDynasty.Id },
        new ArtifactTopic { ArtifactId = art3.Id, TopicId = topicCeramics.Id },
        new ArtifactTopic { ArtifactId = art3.Id, TopicId = topicDynasty.Id });
    await setup.SaveChangesAsync();

    // -------------------------------------------------------------------------
    // TC-014, TC-015, TC-016: Tạo tour -> v1 Draft -> Submit -> Curator Approve
    // -------------------------------------------------------------------------
    var createdTour = await Run(db => Tours(db).CreateAsync(
        museum.Id,
        new TourRequest("Hành trình Gốm & Cung đình v1", "Bản đầu tiên gồm 2 điểm dừng", 40, [art1.Id, art2.Id]),
        staff,
        false));

    var versions = await Run(db => Tours(db).ListVersionsAsync(createdTour.Id, staff, false));
    Check(versions.Count == 1 && versions[0].VersionNo == 1 && versions[0].Status == TourVersionStatuses.Draft, "TC-014: Creating tour creates Version 1 in Draft");
    var v1Id = versions[0].Id;

    // Khi v1 còn là Draft, khách chưa thấy được.
    await Reject(
        async () => await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id)),
        404,
        "TC-016: Draft version is invisible to visitors");

    // Staff gửi duyệt v1.
    var submittedV1 = await Run(db => Tours(db).SubmitVersionAsync(v1Id, staff, false));
    Check(submittedV1.Status == TourVersionStatuses.Submitted, "TC-014: Staff submits v1");

    // TC-025: Sau khi Submit, bản v1 là bất biến -> Staff cố sửa sẽ nhận 409.
    await Reject(
        async () => await Run(db => Tours(db).UpdateVersionDraftAsync(
            v1Id,
            new TourRequest("Sửa trái phép", "Mô tả", 40, [art1.Id]),
            staff,
            false)),
        409,
        "TC-025: Submitted version is immutable");

    // TC-015: Curator của bảo tàng khác không được duyệt; người gửi (staff) không được tự duyệt.
    await Reject(
        async () => await Run(db => Tours(db).ApproveVersionAsync(v1Id, new ApproveTourVersionRequest(null, null), foreignCurator, false)),
        403,
        "TC-015: Curator from another museum cannot approve");

    await Reject(
        async () => await Run(db => Tours(db).ApproveVersionAsync(v1Id, new ApproveTourVersionRequest(null, null), staff, false)),
        409,
        "TC-015: Submitter cannot self-approve");

    // Curator hợp lệ duyệt v1 với PublicAt = hiện tại.
    var approvedV1 = await Run(db => Tours(db).ApproveVersionAsync(
        v1Id,
        new ApproveTourVersionRequest(clock.Now.UtcDateTime, "Duyệt v1 công bố ngay"),
        curator,
        false));
    Check(approvedV1.Status == TourVersionStatuses.Approved && approvedV1.ReviewedBy == curator, "TC-014: Curator approves v1");

    var pubV1 = await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id));
    Check(pubV1.Id == v1Id && pubV1.VersionNo == 1, "TC-014: Approved v1 with PublicAt <= now is public");

    // -------------------------------------------------------------------------
    // TC-017, TC-018, TC-019: Gợi ý lộ trình, lưu SavedRoute và mở lại
    // -------------------------------------------------------------------------
    var recResult = await Run(db => Tours(db).RecommendAsync(
        museum.Id,
        new TourRecommendRequest([topicCeramics.Id, topicDynasty.Id], AvailableMinutes: 60),
        visitorA));
    Check(recResult.Recommendations.Count == 1 && recResult.Recommendations[0].TourVersionId == v1Id, "TC-017: Recommendation returns public v1");

    var savedRoute = await Run(db => Tours(db).SaveRouteAsync(
        visitorA,
        new SaveRouteRequest(v1Id, [topicCeramics.Id], AvailableMinutes: 60)));
    Check(savedRoute.TourVersionId == v1Id && savedRoute.VersionNo == 1, "TC-019: Visitor saves recommended route");

    // -------------------------------------------------------------------------
    // TC-020, TC-021, TC-022, TC-023, TC-026:
    // Khách A bắt đầu tham quan (gắn v1) -> Staff tạo v2 (bị Reject) -> tạo v3 (hẹn giờ tương lai)
    // -> đến giờ v3 công bố -> Khách B bắt đầu (nhận v3) trong khi Khách A vẫn giữ nguyên v1!
    // -------------------------------------------------------------------------
    var visitA = await Run(db => Progress(db).StartVisitAsync(
        visitorA,
        new StartVisitRequestDto { MuseumId = museum.Id, TourId = createdTour.Id }));
    Check(visitA.TourVersionId == v1Id && visitA.TourVersionNo == 1, "TC-020: Visitor A visit pins TourVersion v1");

    // Khách A quét điểm dừng 1/2 của v1.
    await Run(db => Recognition(db).RecordQrAsync(new ScanQrRequest(art1.QrCode!, visitA.Id, Guid.NewGuid()), visitorA));

    // Staff tạo v2 rồi bị Curator từ chối (TC-016).
    var v2 = await Run(db => Tours(db).CreateVersionDraftAsync(
        createdTour.Id,
        new CreateTourVersionRequest(CopyFromVersionId: v1Id, Content: null),
        staff,
        false));
    await Run(db => Tours(db).SubmitVersionAsync(v2.Id, staff, false));
    var rejectedV2 = await Run(db => Tours(db).RejectVersionAsync(v2.Id, new RejectTourVersionRequest("Cần bổ sung Đĩa ngọc"), curator, false));
    Check(rejectedV2.Status == TourVersionStatuses.Rejected, "TC-016: v2 is rejected");
    Check((await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id))).Id == v1Id, "TC-016: Rejected v2 is invisible; v1 remains public");

    // Staff tạo v3 (3 điểm dừng: art1, art2, art3), Curator duyệt nhưng hẹn giờ công bố sau 2 tiếng (TC-016, TC-026).
    var v3 = await Run(db => Tours(db).CreateVersionDraftAsync(
        createdTour.Id,
        new CreateTourVersionRequest(
            CopyFromVersionId: null,
            Content: new TourRequest("Hành trình Gốm & Cung đình v3", "Bản mở rộng 3 điểm dừng", 55, [art1.Id, art2.Id, art3.Id])),
        staff,
        false));
    await Run(db => Tours(db).SubmitVersionAsync(v3.Id, staff, false));
    var futureTime = clock.Now.UtcDateTime.AddHours(2);
    await Run(db => Tours(db).ApproveVersionAsync(v3.Id, new ApproveTourVersionRequest(futureTime, "Hẹn công bố sau 2h"), curator, false));

    // Kiểm tra thêm trường hợp duyệt công bố NGAY LẬP TỨC (PublicAt <= now) trong lúc khách đang có Visit_Item quét dở ở v1.
    var v4 = await Run(db => Tours(db).CreateVersionDraftAsync(
        createdTour.Id,
        new CreateTourVersionRequest(
            CopyFromVersionId: null,
            Content: new TourRequest("Hành trình Gốm & Cung đình v4", "Bản mở rộng công bố ngay", 55, [art1.Id, art2.Id, art3.Id])),
        staff,
        false));
    await Run(db => Tours(db).SubmitVersionAsync(v4.Id, staff, false));

    // Trước giờ PublicAt của v3: bản công khai vẫn là v1.
    Check((await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id))).Id == v1Id, "TC-016/TC-026: Future v3 is invisible before PublicAt");

    // Tua đồng hồ qua thời điểm PublicAt của v3 -> v3 tự động trở thành bản công khai hiện tại!
    clock.Now = clock.Now.AddHours(3);
    var currentPublic = await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id));
    Check(currentPublic.Id == v3.Id && currentPublic.VersionNo == 3, "TC-026: v3 automatically becomes public once clock passes PublicAt");

    // Duyệt luôn v4 công bố ngay lập tức khi đang có visit_items của v1 -> không bị lỗi khóa ngoại!
    await Run(db => Tours(db).ApproveVersionAsync(v4.Id, new ApproveTourVersionRequest(clock.Now.UtcDateTime, "Công bố ngay v4"), curator, false));
    currentPublic = await Run(db => Tours(db).GetPublicTourVersionAsync(createdTour.Id));
    Check(currentPublic.Id == v4.Id && currentPublic.VersionNo == 4, "Immediate approval while active visits have scanned items succeeds");

    // TC-019: Mở lại SavedRoute của Khách A vẫn trả về đúng v1 (2 điểm dừng), không bị nhảy sang v4.
    var reopened = await Run(db => Tours(db).GetSavedRouteAsync(visitorA, savedRoute.Id));
    Check(reopened.TourVersionId == v1Id && reopened.VersionNo == 1 && reopened.ArtifactIds.Count == 2, "TC-019: Reopened saved route retains pinned v1");

    // TC-022: Khách B bắt đầu tour mới lúc này sẽ nhận v4 (3 điểm dừng).
    var visitB = await Run(db => Progress(db).StartVisitAsync(
        visitorB,
        new StartVisitRequestDto { MuseumId = museum.Id, TourId = createdTour.Id }));
    Check(visitB.TourVersionId == v4.Id && visitB.TourVersionNo == 4, "TC-022: New Visitor B receives newly published v4");

    var progressB = await Run(db => Progress(db).GetProgressAsync(visitorB, visitB.Id));
    Check(progressB.TourVersionId == v4.Id && progressB.Stops.Count == 3, "TC-022: Visitor B has 3 stops from v4");

    // TC-020 & TC-021: Tiến độ của Khách A vẫn giữ nguyên ở v1 (2 điểm dừng, đã đi 1/2), không hề bị đổi thành 3 điểm dừng.
    var progressA = await Run(db => Progress(db).GetProgressAsync(visitorA, visitA.Id));
    Check(
        progressA.TourVersionId == v1Id &&
        progressA.TourVersionNo == 1 &&
        progressA.Stops.Count == 2 &&
        progressA.Stops.Count(s => s.LastVisitedAt != null) == 1 &&
        progressA.NextArtifactId == art2.Id,
        "TC-020/TC-021: v3 publication never alters Visitor A's v1 progress or stop list");

    // TC-023: Khách A quét nốt điểm dừng thứ 2 của v1 và hoàn thành tour thành công (không cần quét điểm thứ 3 của v3).
    await Run(db => Recognition(db).RecordQrAsync(new ScanQrRequest(art2.QrCode!, visitA.Id, Guid.NewGuid()), visitorA));
    var completedA = await Run(db => Progress(db).CompleteTourAsync(visitorA, visitA.Id));
    Check(
        completedA.Status == VisitStatus.Completed &&
        completedA.TourVersionId == v1Id &&
        completedA.TourVersionNo == 1,
        "TC-021/TC-023: Visitor A completes tour against pinned v1 stops");

    Console.WriteLine("WF-02 database & E2E checks passed (all TC-014..TC-026 verified against PostgreSQL).");
}
finally
{
    setup.ChangeTracker.Clear();
    await setup.Database.ExecuteSqlRawAsync(dropSchemaSql);
}

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
