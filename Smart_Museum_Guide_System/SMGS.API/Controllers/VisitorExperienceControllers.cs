using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

public class ExperienceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext c)
    {
        var (status, message) = c.Exception switch {
            ExperienceException e => (e.StatusCode, e.Message),
            IndoorMapException e => (e.StatusCode, e.Message),
            DbUpdateConcurrencyException => (409, "Dữ liệu đã thay đổi; hãy tải lại."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" } } => (409, "Dữ liệu trùng hoặc đang được tham chiếu; hãy tải lại."),
            _ => (0, "")
        };
        if (status == 0) return;
        c.Result = new ObjectResult(ResponseDto<object>.Fail(message, status)) { StatusCode = status }; c.ExceptionHandled = true;
    }
}
[ApiController]
[TypeFilter(typeof(ExperienceExceptionFilter))]
public abstract class ExperienceControllerBase : ControllerBase
{
    protected Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ExperienceException("Vui lòng đăng nhập.", 401);
    protected bool Admin => User.IsInRole(RoleNames.Admin);
    protected bool Manager => Admin || User.IsInRole(RoleNames.MuseumStaff) || User.IsInRole(RoleNames.Curator);
    protected IActionResult Result<T>(T data, int status = 200) => StatusCode(status, ResponseDto<T>.Success(data, statusCode: status));
}

[Route("api/tours")]
public class TourController(ITourService service) : ExperienceControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] int museumId, int page = 1, int pageSize = 20) => Result(await service.ListAsync(museumId, false, null, false, page, pageSize));
    [HttpGet("manage"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff + "," + RoleNames.Curator)]
    public async Task<IActionResult> Manage([FromQuery] int museumId, int page = 1, int pageSize = 20) => Result(await service.ListAsync(museumId, true, UserId, Admin, page, pageSize));
    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<IActionResult> Get(int id) => Result(await service.GetAsync(id, Manager ? UserId : null, Admin));
    [HttpPost, Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> Create([FromQuery] int museumId, TourRequest request) => Result(await service.CreateAsync(museumId, request, UserId, Admin), 201);
    [HttpPut("{id:int}"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> Update(int id, UpdateTourRequest request) => Result(await service.UpdateStopsAsync(id, request, UserId, Admin));
    [HttpPost("{id:int}/publish"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> Publish(int id, RevisionRequest request) => Result(await service.PublishAsync(id, request.Revision, UserId, Admin));

    // WF-02 (F04): Phiên bản TourVersion (Draft -> Submitted -> Approved/Rejected + PublicAt)
    [HttpGet("{tourId:int}/public-version"), AllowAnonymous]
    public async Task<IActionResult> GetPublicVersion(int tourId) => Result(await service.GetPublicTourVersionAsync(tourId));
    [HttpGet("{tourId:int}/versions"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff + "," + RoleNames.Curator)]
    public async Task<IActionResult> ListVersions(int tourId) => Result(await service.ListVersionsAsync(tourId, UserId, Admin));
    [HttpPost("{tourId:int}/versions"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> CreateVersionDraft(int tourId, CreateTourVersionRequest request) => Result(await service.CreateVersionDraftAsync(tourId, request, UserId, Admin), 201);
    [HttpPut("versions/{versionId:int}"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> UpdateVersionDraft(int versionId, TourRequest request) => Result(await service.UpdateVersionDraftAsync(versionId, request, UserId, Admin));
    [HttpPost("versions/{versionId:int}/submit"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> SubmitVersion(int versionId) => Result(await service.SubmitVersionAsync(versionId, UserId, Admin));
    [HttpPost("versions/{versionId:int}/approve"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.Curator)]
    public async Task<IActionResult> ApproveVersion(int versionId, ApproveTourVersionRequest request) => Result(await service.ApproveVersionAsync(versionId, request, UserId, Admin));
    [HttpPost("versions/{versionId:int}/reject"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.Curator)]
    public async Task<IActionResult> RejectVersion(int versionId, RejectTourVersionRequest request) => Result(await service.RejectVersionAsync(versionId, request, UserId, Admin));

    // WF-02 (F05): Gợi ý tour theo sở thích + thời gian & lưu lộ trình (SavedRoute)
    [HttpPost("recommendations"), AllowAnonymous]
    public async Task<IActionResult> Recommend([FromQuery] int museumId, TourRecommendRequest request)
    {
        Guid? visitor = User.Identity?.IsAuthenticated == true && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
            ? uid
            : null;
        return Result(await service.RecommendAsync(museumId, request, visitor));
    }

    [HttpPost("saved-routes"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> SaveRoute(SaveRouteRequest request) => Result(await service.SaveRouteAsync(UserId, request), 201);
    [HttpGet("saved-routes"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> ListSavedRoutes([FromQuery] int? museumId) => Result(await service.ListSavedRoutesAsync(UserId, museumId));
    [HttpGet("saved-routes/{id:int}"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> GetSavedRoute(int id) => Result(await service.GetSavedRouteAsync(UserId, id));
}
[Route("api/recognitions"), Authorize(Roles = RoleNames.Visitor)]
public class RecognitionController(IRecognitionService service) : ExperienceControllerBase
{
    [HttpPost("qr")]
    public async Task<IActionResult> Scan(ScanQrRequest request) => Result(await service.RecordQrAsync(request, UserId));
    [HttpGet("me")]
    public async Task<IActionResult> History(int page = 1, int pageSize = 20) => Result(await service.GetHistoryAsync(UserId, page, pageSize));
}
[Route("api/visits/{visitId:int}"), Authorize(Roles = RoleNames.Visitor)]
public class VisitProgressController(IVisitProgressService service) : ExperienceControllerBase
{
    [HttpGet("progress")]
    public async Task<IActionResult> Get(int visitId) => Result(await service.GetProgressAsync(UserId, visitId));
}
[Route("api/visitor-navigation"), Authorize(Roles = RoleNames.Visitor)]
public class VisitorNavigationController(IVisitorNavigationService service) : ExperienceControllerBase
{
    [HttpGet("location")]
    public async Task<IActionResult> Location(int visitId) => Result(await service.LocateFromRecognitionAsync(UserId, visitId));
    [HttpGet("route")]
    public async Task<IActionResult> Route(int visitId, int mapId, int? artifactId, bool accessibleOnly = false) => Result(await service.RouteToArtifactAsync(UserId, visitId, mapId, artifactId, accessibleOnly));
}
[Route("api/question-bank"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff + "," + RoleNames.Curator)]
public class QuestionBankController(IQuestionBankService service) : ExperienceControllerBase
{
    [HttpGet("questions")]
    public async Task<IActionResult> List(int artifactId, int page = 1, int pageSize = 20) => Result(await service.ListAsync(artifactId, UserId, Admin, page, pageSize));
    [HttpPost("questions")]
    public async Task<IActionResult> Create([FromQuery] int artifactId, QuestionRequest request) => Result(await service.CreateAsync(artifactId, request, UserId, Admin), 201);
    [HttpPut("questions/{id:int}")]
    public async Task<IActionResult> Update(int id, QuestionRequest request) => Result(await service.UpdateAsync(id, request, UserId, Admin));
    [HttpPost("questions/{id:int}/review"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.Curator)]
    public async Task<IActionResult> Review(int id, ReviewQuestionRequest request) => Result(await service.ReviewQuestionAsync(id, request, UserId, Admin));
    [HttpGet("quizzes")]
    public async Task<IActionResult> Quizzes(int artifactId) => Result(await service.ListQuizzesAsync(artifactId, UserId, Admin));
    [HttpPost("quizzes")]
    public async Task<IActionResult> CreateQuiz([FromQuery] int artifactId, QuizRequest request) => Result(await service.CreateQuizAsync(artifactId, request, UserId, Admin), 201);
    [HttpPost("quizzes/{id:int}/publish"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.Curator)]
    public async Task<IActionResult> Publish(int id) => Result(await service.PublishQuizAsync(id, UserId, Admin));
}
[Route("api/daily-quizzes"), Authorize(Roles = RoleNames.Visitor)]
public class DailyQuizController(IDailyQuizService service) : ExperienceControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> Today() => Result(await service.GetTodayAsync(UserId));
    [HttpPut("{id:long}/deferred")]
    public async Task<IActionResult> Defer(long id, [FromQuery] bool deferred = true)
    {
        await service.SetDeferredAsync(id, deferred, UserId);
        return Result(new { Saved = deferred });
    }
}
[Route("api/quiz-attempts"), Authorize(Roles = RoleNames.Visitor)]
public class QuizAttemptController(IQuizAttemptService service) : ExperienceControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start(StartAttemptRequest request) => Result(await service.StartAsync(request, UserId));
    [HttpPost("{id:long}/answers")]
    public async Task<IActionResult> Answer(long id, AnswerRequest request) => Result(await service.AnswerAsync(id, request, UserId));
    [HttpPost("{id:long}/reset")]
    public async Task<IActionResult> Reset(long id) => Result(await service.ResetAsync(id, UserId));
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Result(await service.GetResultsAsync(id, UserId));
    [HttpGet("me")]
    public async Task<IActionResult> Mine(int page = 1, int pageSize = 20) => Result(await service.ListAsync(UserId, page, pageSize));
}
[Route("api/gamification"), Authorize]
public class GamificationController(IGamificationService service) : ExperienceControllerBase
{
    [HttpGet("me/points"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> Balance() => Result(new { Points = await service.GetBalanceAsync(UserId) });
    [HttpGet("me/transactions"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> Transactions(int page = 1, int pageSize = 20) => Result(await service.GetTransactionsAsync(UserId, page, pageSize));
    [HttpGet("me/badges"), Authorize(Roles = RoleNames.Visitor)]
    public async Task<IActionResult> Mine() => Result(await service.GetBadgesAsync(UserId));
    [HttpGet("badges")]
    public async Task<IActionResult> Badges() => Result(await service.ListBadgesAsync());
    [HttpPost("badges"), Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(BadgeRequest request) => Result(await service.CreateBadgeAsync(request), 201);
}
