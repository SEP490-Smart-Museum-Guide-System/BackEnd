using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Feedback.Request;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/feedbacks")]
[Produces("application/json")]
[Authorize]
public class FeedbacksController(IFeedbackService feedbackService) : ControllerBase
{
    // ─────────────────────────── Visitor ───────────────────────────

    /// <summary>Gửi đánh giá cho lượt tham quan đã hoàn thành.</summary>
    [Authorize(Roles = RoleNames.Visitor)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await feedbackService.CreateAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Sửa đánh giá (trong thời hạn cho phép).</summary>
    [Authorize(Roles = RoleNames.Visitor)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFeedbackRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await feedbackService.UpdateAsync(id, userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Xem đánh giá của mình theo lượt tham quan.</summary>
    [Authorize(Roles = RoleNames.Visitor)]
    [HttpGet("visit/{visitId:int}")]
    public async Task<IActionResult> GetByVisit(int visitId)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await feedbackService.GetByVisitAsync(visitId, userId);
        return StatusCode(response.StatusCode, response);
    }

    // ─────────────────────────── Museum Staff ───────────────────────────

    /// <summary>
    /// Museum Staff xem feedback của bảo tàng.
    /// TODO: kiểm tra staff có được phân công bảo tàng này khi có Staff Assignment.
    /// </summary>
    [Authorize(Roles = $"{RoleNames.MuseumStaff},{RoleNames.Admin}")]
    [HttpGet("museum/{museumId:int}")]
    public async Task<IActionResult> GetByMuseum(int museumId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var response = await feedbackService.GetByMuseumAsync(museumId, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Museum Staff phản hồi (hoặc sửa phản hồi) một feedback.</summary>
    [Authorize(Roles = RoleNames.MuseumStaff)]
    [HttpPut("{id:int}/response")]
    public async Task<IActionResult> Respond(int id, [FromBody] StaffResponseRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await feedbackService.RespondAsync(id, userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    // ─────────────────────────── System Staff / Admin ───────────────────────────

    /// <summary>Danh sách toàn bộ feedback, lọc theo trạng thái kiểm duyệt.</summary>
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? moderationStatus, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var response = await feedbackService.GetAllAsync(moderationStatus, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Ẩn / hiện feedback không phù hợp.</summary>
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    [HttpPatch("{id:int}/moderation")]
    public async Task<IActionResult> Moderate(int id, [FromBody] ModerateFeedbackRequestDto dto)
    {
        var response = await feedbackService.ModerateAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
