using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Complaint.Request;
using SMGS.Application.DTOs.Feedback.Request;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
}

static string? Roles(Type controller, string action) =>
    controller.GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

// ───────────────────────── 1. Phân quyền ─────────────────────────
foreach (var c in new[] { typeof(VisitsController), typeof(FeedbacksController), typeof(ComplaintsController) })
{
    Check(c.IsDefined(typeof(AuthorizeAttribute)), $"{c.Name} phải yêu cầu đăng nhập.");
    Check(!c.GetMethods().Any(m => m.IsDefined(typeof(AllowAnonymousAttribute))), $"{c.Name} không được có AllowAnonymous.");
}
Check(typeof(VisitsController).GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleNames.Visitor, "Visits chỉ dành cho Visitor.");
Check(Roles(typeof(FeedbacksController), "Create") == RoleNames.Visitor, "Chỉ Visitor gửi đánh giá.");
Check(Roles(typeof(FeedbacksController), "Update") == RoleNames.Visitor, "Chỉ Visitor sửa đánh giá.");
Check(Roles(typeof(FeedbacksController), "Respond") == RoleNames.MuseumStaff, "Chỉ Museum Staff phản hồi.");
Check(Roles(typeof(FeedbacksController), "Moderate") == $"{RoleNames.Staff},{RoleNames.Admin}", "Kiểm duyệt dành cho System Staff/Admin.");
Check(Roles(typeof(FeedbacksController), "GetAll") == $"{RoleNames.Staff},{RoleNames.Admin}", "Xem toàn bộ feedback dành cho System Staff/Admin.");
Check(Roles(typeof(ComplaintsController), "Create") == RoleNames.Visitor, "Chỉ Visitor gửi khiếu nại.");
Check(Roles(typeof(ComplaintsController), "UpdateStatus") == RoleNames.Staff, "Chỉ System Staff xử lý khiếu nại.");

// ───────────────────────── 2. Validation DTO ─────────────────────────
static bool Valid(object o) => Validator.TryValidateObject(o, new ValidationContext(o), new List<ValidationResult>(), true);
Check(!Valid(new CreateFeedbackRequestDto { VisitId = 1, OverallRating = 0 }), "Rating 0 phải bị từ chối.");
Check(!Valid(new CreateFeedbackRequestDto { VisitId = 1, OverallRating = 6 }), "Rating 6 phải bị từ chối.");
Check(!Valid(new CreateFeedbackRequestDto { VisitId = 1, OverallRating = 5, ChatbotRating = 9 }), "Rating phụ ngoài 1–5 phải bị từ chối.");
Check(!Valid(new CreateFeedbackRequestDto { VisitId = 1, OverallRating = 5, Comment = new string('x', 2001) }), "Comment > 2000 ký tự phải bị từ chối.");
Check(Valid(new CreateFeedbackRequestDto { VisitId = 1, OverallRating = 5 }), "Chỉ cần OverallRating là hợp lệ.");
Check(!Valid(new ModerateFeedbackRequestDto { ModerationStatus = "Deleted" }), "Trạng thái kiểm duyệt lạ phải bị từ chối.");
Check(!Valid(new UpdateComplaintStatusRequestDto { Status = "New" }), "Không cho đặt lại trạng thái New.");

// ───────────────────────── 3. Visit ─────────────────────────
var visits = new FakeVisits();
var museums = new FakeMuseums();
var visitService = new VisitService(visits, museums);
var visitor = Guid.NewGuid();
var other = Guid.NewGuid();

Check((await visitService.StartAsync(visitor, new() { MuseumId = 99 })).StatusCode == 404, "Bảo tàng không tồn tại → 404.");
museums.Item.IsActive = false;
Check((await visitService.StartAsync(visitor, new() { MuseumId = 1 })).StatusCode == 404, "Bảo tàng ngừng hoạt động → 404.");
museums.Item.IsActive = true;
var started = await visitService.StartAsync(visitor, new() { MuseumId = 1 });
Check(started.StatusCode == 201 && started.Data!.Status == VisitStatus.InProgress, "Bắt đầu lượt tham quan.");
Check((await visitService.StartAsync(visitor, new() { MuseumId = 1 })).StatusCode == 409, "Không cho 2 lượt đang diễn ra tại cùng bảo tàng.");
var visitId = started.Data.Id;
Check((await visitService.CompleteAsync(visitId, other)).StatusCode == 404, "Người khác không hoàn thành được lượt của mình.");

// ───────────────────────── 4. Feedback ─────────────────────────
var feedbacks = new FakeFeedbacks(visits);
var feedbackService = new FeedbackService(feedbacks, visits);
var good = new CreateFeedbackRequestDto { VisitId = visitId, OverallRating = 5, TourRating = 4, Comment = "  Rất hay  " };

Check((await feedbackService.CreateAsync(visitor, good)).StatusCode == 400, "[Q23] Chưa hoàn thành tour thì không được đánh giá.");
Check((await visitService.CompleteAsync(visitId, visitor)).StatusCode == 200, "Hoàn thành lượt tham quan.");
Check((await visitService.CompleteAsync(visitId, visitor)).StatusCode == 400, "Không hoàn thành lại lượt đã xong.");
Check((await feedbackService.CreateAsync(other, good)).StatusCode == 404, "Không đánh giá lượt của người khác.");
Check((await feedbackService.CreateAsync(visitor, new() { VisitId = 999, OverallRating = 5 })).StatusCode == 404, "Lượt không tồn tại → 404.");

var created = await feedbackService.CreateAsync(visitor, good);
Check(created.StatusCode == 201, "Gửi đánh giá thành công.");
Check(created.Data is { OverallRating: 5, TourRating: 4, Comment: "Rất hay", CanEdit: true, ModerationStatus: FeedbackModerationStatus.Visible }, "Đánh giá được chuẩn hóa và hiển thị.");
Check((await feedbackService.CreateAsync(visitor, good)).StatusCode == 409, "Mỗi lượt chỉ một đánh giá.");
var feedbackId = created.Data!.Id;

var updated = await feedbackService.UpdateAsync(feedbackId, visitor, new() { OverallRating = 3, Comment = "   " });
Check(updated.StatusCode == 200 && updated.Data is { OverallRating: 3, Comment: null } && updated.Data.UpdatedAt is not null, "Sửa trong thời hạn được phép.");
Check((await feedbackService.UpdateAsync(feedbackId, other, new() { OverallRating = 1 })).StatusCode == 404, "Người khác không sửa được.");

feedbacks.Items.Single(f => f.Id == feedbackId).CreatedAt = DateTime.UtcNow - FeedbackService.EditWindow - TimeSpan.FromMinutes(1);
Check((await feedbackService.UpdateAsync(feedbackId, visitor, new() { OverallRating = 1 })).StatusCode == 400, "Quá thời hạn thì không sửa được.");
Check((await feedbackService.GetByVisitAsync(visitId, visitor)).Data!.CanEdit == false, "CanEdit = false khi quá hạn.");

// Staff response
var staff = Guid.NewGuid();
Check((await feedbackService.RespondAsync(feedbackId, staff, new() { Content = "   " })).StatusCode == 400, "Phản hồi rỗng phải bị từ chối.");
var responded = await feedbackService.RespondAsync(feedbackId, staff, new() { Content = " Cảm ơn bạn " });
Check(responded.StatusCode == 200 && responded.Data!.StaffResponse is { Content: "Cảm ơn bạn" }, "Lưu phản hồi của nhân viên.");
var reResponded = await feedbackService.RespondAsync(feedbackId, staff, new() { Content = "Cảm ơn nhiều" });
Check(feedbacks.Responses.Count == 1 && reResponded.Data!.StaffResponse!.Content == "Cảm ơn nhiều" && reResponded.Data.StaffResponse.UpdatedAt is not null, "Phản hồi lần 2 là sửa, không tạo bản mới.");

// Moderation
var hidden = await feedbackService.ModerateAsync(feedbackId, new() { ModerationStatus = FeedbackModerationStatus.Hidden, ModerationNote = " Ngôn từ không phù hợp " });
Check(hidden.Data is { ModerationStatus: FeedbackModerationStatus.Hidden, ModerationNote: "Ngôn từ không phù hợp" }, "System Staff ẩn được feedback.");
Check((await feedbackService.GetByVisitAsync(visitId, visitor)).Data!.ModerationNote is null, "Ghi chú kiểm duyệt không lộ cho khách.");
Check((await feedbackService.RespondAsync(feedbackId, staff, new() { Content = "x" })).StatusCode == 400, "Không phản hồi feedback đã ẩn.");
Check((await feedbackService.GetByMuseumAsync(1, 1, 10)).Data!.TotalCount == 0, "Museum Staff không thấy feedback đã ẩn.");
Check((await feedbackService.GetAllAsync(FeedbackModerationStatus.Hidden, 1, 10)).Data!.TotalCount == 1, "System Staff lọc được feedback đã ẩn.");
Check((await feedbackService.GetAllAsync(null, -5, 1000)).Data is { Page: 1, PageSize: 100 }, "Phân trang có giới hạn.");

// ───────────────────────── 5. Complaint ─────────────────────────
var complaints = new FakeComplaints();
var complaintService = new ComplaintService(complaints);
Check((await complaintService.CreateAsync(visitor, new() { Subject = "  ", Body = "x" })).StatusCode == 400, "Tiêu đề rỗng phải bị từ chối.");
var complaint = await complaintService.CreateAsync(visitor, new() { Subject = " Thanh toán lỗi ", Body = " Bị trừ tiền 2 lần " });
Check(complaint.StatusCode == 201 && complaint.Data is { Status: ComplaintStatus.New, Subject: "Thanh toán lỗi" }, "Tạo khiếu nại ở trạng thái New.");
var cid = complaint.Data!.Id;
Check((await complaintService.GetMineByIdAsync(cid, other)).StatusCode == 404, "Khách khác không xem được khiếu nại.");
Check((await complaintService.UpdateStatusAsync(cid, staff, new() { Status = ComplaintStatus.Resolved })).StatusCode == 400, "New → Resolved không hợp lệ.");
var assigned = await complaintService.AssignToMeAsync(cid, staff);
Check(assigned.Data is { Status: ComplaintStatus.InProgress } && assigned.Data.AssignedTo == staff, "Nhận xử lý → InProgress.");
var resolved = await complaintService.UpdateStatusAsync(cid, staff, new() { Status = ComplaintStatus.Resolved, ResolutionNote = " Đã hoàn tiền " });
Check(resolved.Data is { Status: ComplaintStatus.Resolved, ResolutionNote: "Đã hoàn tiền" } && resolved.Data.ResolvedAt is not null, "Giải quyết lưu ghi chú và thời điểm.");
Check((await complaintService.AssignToMeAsync(cid, staff)).StatusCode == 400, "Không nhận lại khiếu nại đã xong.");
Check((await complaintService.UpdateStatusAsync(cid, staff, new() { Status = ComplaintStatus.InProgress })).StatusCode == 200, "Resolved → InProgress (mở lại).");
Check((await complaintService.UpdateStatusAsync(cid, staff, new() { Status = ComplaintStatus.Closed })).StatusCode == 200, "InProgress → Closed.");
Check((await complaintService.UpdateStatusAsync(cid, staff, new() { Status = ComplaintStatus.InProgress })).StatusCode == 400, "Closed là trạng thái cuối.");
Check((await complaintService.GetMineAsync(visitor, null, 1, 10)).Data!.TotalCount == 1, "Khách thấy khiếu nại của mình.");
Check((await complaintService.GetMineAsync(other, null, 1, 10)).Data!.TotalCount == 0, "Khách khác không thấy.");

Console.WriteLine("PASS: phân quyền, validation, visit, feedback (điều kiện hoàn thành, thời hạn sửa, phản hồi, kiểm duyệt) và complaint state machine.");
if (args.Contains("--database")) await DatabaseChecks.RunAsync();

// ───────────────────────── Fakes ─────────────────────────
sealed class FakeVisits : IVisitRepository
{
    public List<Visit> Items { get; } = new();
    public Task<Visit?> GetByIdAsync(int id) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id));
    public Task<Visit?> GetActiveVisitAsync(Guid visitorId, int museumId) =>
        Task.FromResult(Items.FirstOrDefault(v => v.VisitorId == visitorId && v.MuseumId == museumId && v.Status == VisitStatus.InProgress));
    public Task<(IList<Visit> Items, int TotalCount)> GetPagedAsync(Guid? visitorId, int? museumId, string? status, int page, int pageSize)
    {
        var q = Items.Where(v => (visitorId == null || v.VisitorId == visitorId) && (museumId == null || v.MuseumId == museumId) && (status == null || v.Status == status)).ToList();
        return Task.FromResult(((IList<Visit>)q, q.Count));
    }
    public Task<Visit> CreateAsync(Visit visit) { visit.Id = Items.Count + 1; Items.Add(visit); return Task.FromResult(visit); }
    public Task UpdateAsync(Visit visit) => Task.CompletedTask;
}

sealed class FakeFeedbacks(FakeVisits visits) : IFeedbackRepository
{
    public List<VisitFeedback> Items { get; } = new();
    public List<StaffResponse> Responses { get; } = new();
    VisitFeedback? Load(VisitFeedback? f)
    {
        if (f is null) return null;
        f.Visit = visits.Items.First(v => v.Id == f.VisitId);
        f.StaffResponse = Responses.FirstOrDefault(r => r.FeedbackId == f.Id);
        return f;
    }
    public Task<VisitFeedback?> GetByIdAsync(int id) => Task.FromResult(Load(Items.FirstOrDefault(f => f.Id == id)));
    public Task<VisitFeedback?> GetByVisitIdAsync(int visitId) => Task.FromResult(Load(Items.FirstOrDefault(f => f.VisitId == visitId)));
    (IList<VisitFeedback>, int) Page(IEnumerable<VisitFeedback> q, string? status)
    {
        var list = q.Select(f => Load(f)!).Where(f => status == null || f.ModerationStatus == status).ToList();
        return (list, list.Count);
    }
    public Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedByMuseumAsync(int museumId, string? status, int page, int pageSize) =>
        Task.FromResult(Page(Items.Where(f => visits.Items.First(v => v.Id == f.VisitId).MuseumId == museumId), status));
    public Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedAllAsync(string? status, int page, int pageSize) =>
        Task.FromResult(Page(Items, status));
    public Task<VisitFeedback> CreateAsync(VisitFeedback f) { f.Id = Items.Count + 1; Items.Add(f); return Task.FromResult(f); }
    public Task UpdateAsync(VisitFeedback f) => Task.CompletedTask;
    public Task<StaffResponse?> GetResponseByFeedbackIdAsync(int feedbackId) => Task.FromResult(Responses.FirstOrDefault(r => r.FeedbackId == feedbackId));
    public Task<StaffResponse> CreateResponseAsync(StaffResponse r) { r.Id = Responses.Count + 1; Responses.Add(r); return Task.FromResult(r); }
    public Task UpdateResponseAsync(StaffResponse r) => Task.CompletedTask;
}

sealed class FakeComplaints : IComplaintRepository
{
    readonly List<Complaint> items = new();
    public Task<Complaint?> GetByIdAsync(int id) => Task.FromResult(items.FirstOrDefault(c => c.Id == id));
    public Task<(IList<Complaint> Items, int TotalCount)> GetPagedAsync(Guid? visitorId, string? status, Guid? assignedTo, int page, int pageSize)
    {
        var q = items.Where(c => (visitorId == null || c.VisitorId == visitorId) && (status == null || c.Status == status) && (assignedTo == null || c.AssignedTo == assignedTo)).ToList();
        return Task.FromResult(((IList<Complaint>)q, q.Count));
    }
    public Task<Complaint> CreateAsync(Complaint c) { c.Id = items.Count + 1; items.Add(c); return Task.FromResult(c); }
    public Task UpdateAsync(Complaint c) => Task.CompletedTask;
}

sealed class FakeMuseums : IMuseumRepository
{
    public Museum Item { get; } = new() { Id = 1, Name = "Museum", IsActive = true };
    public Task<Museum?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(id == 1 ? Item : null);
    public Task<(IList<MuseumDiscoveryItem> Items, int TotalCount)> DiscoverAsync(MuseumDiscoveryQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(IList<Museum> Items, int TotalCount)> GetPagedAsync(string? keyword, bool? isActive, int page, int pageSize) => throw new NotSupportedException();
    public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) => throw new NotSupportedException();
    public Task<Museum> CreateAsync(Museum museum) => throw new NotSupportedException();
    public Task UpdateAsync(Museum museum) => throw new NotSupportedException();
}
