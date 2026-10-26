using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Visit;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

/// <summary>
/// Lượt tham quan (tối thiểu) — phục vụ điều kiện "đánh giá sau khi hoàn thành tour".
/// </summary>
[ApiController]
[Route("api/visits")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Visitor)]
public class VisitsController(IVisitService visitService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartVisitRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await visitService.StartAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await visitService.CompleteAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await visitService.GetByIdAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await visitService.GetMineAsync(userId, status, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
