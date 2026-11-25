using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/artifacts/{artifactId:int}/guide/messages")]
[Authorize(Roles = RoleNames.Visitor)]
[EnableRateLimiting("artifact-guide")]
public class ArtifactGuideController(
    IArtifactGuideService guide,
    IGuideHistoryService? history = null) : ControllerBase
{
    [HttpPost, RequestSizeLimit(144000)]
    public async Task<IActionResult> Ask(int artifactId, GuideMessageRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập để hỏi trợ lý.", 401));
        var result = await guide.AskAsync(artifactId, userId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Lấy lịch sử hội thoại hỏi đáp của khách tham quan tại hiện vật này.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(int artifactId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập để xem lịch sử hỏi đáp.", 401));
        if (history is null)
            return Ok(ResponseDto<IReadOnlyList<GuideChatMessageDto>>.Success([]));
        var result = await history.GetArtifactHistoryAsync(artifactId, userId, limit, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Xóa lịch sử hội thoại của khách tham quan tại hiện vật này để bắt đầu cuộc trò chuyện mới.</summary>
    [HttpDelete]
    public async Task<IActionResult> ClearHistory(int artifactId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        if (history is null)
            return Ok(ResponseDto<int>.Success(0));
        var result = await history.ClearArtifactHistoryAsync(artifactId, userId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}

[ApiController]
[Route("api/guide")]
[Produces("application/json")]
[Authorize]
public class GuideConversationsController(IGuideHistoryService history) : ControllerBase
{
    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    /// <summary>Danh sách các cuộc hội thoại theo hiện vật của khách tham quan (Q15).</summary>
    [Authorize(Roles = RoleNames.Visitor)]
    [HttpGet("conversations/me")]
    public async Task<IActionResult> GetMyConversations(
        [FromQuery] int? museumId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var result = await history.GetMyConversationsAsync(userId, museumId, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Dòng lịch sử toàn bộ tin nhắn hỏi đáp xuyên suốt nhiều hiện vật của khách tham quan (Q15).</summary>
    [Authorize(Roles = RoleNames.Visitor)]
    [HttpGet("messages/me")]
    public async Task<IActionResult> GetMyMessagesFeed(
        [FromQuery] int? museumId = null,
        [FromQuery] int? artifactId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var result = await history.GetMyMessagesFeedAsync(userId, museumId, artifactId, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Nhật ký hỏi đáp AI để System Staff / Admin / Museum Staff giám sát chất lượng câu trả lời (QB).</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff},{RoleNames.MuseumStaff}")]
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? museumId = null,
        [FromQuery] int? artifactId = null,
        [FromQuery] bool? supported = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var isAdminOrSystemStaff = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Staff);
        var result = await history.GetLogsAsync(museumId, artifactId, supported, page, pageSize, userId, isAdminOrSystemStaff, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
