using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Complaint.Request;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/complaints")]
[Produces("application/json")]
[Authorize]
public class ComplaintsController(IComplaintService complaintService) : ControllerBase
{
    // ─────────────────────────── Visitor ───────────────────────────

    [Authorize(Roles = RoleNames.Visitor)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await complaintService.CreateAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Visitor)]
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await complaintService.GetMineAsync(userId, status, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Visitor)]
    [HttpGet("me/{id:int}")]
    public async Task<IActionResult> GetMineById(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await complaintService.GetMineByIdAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    // ─────────────────────────── System Staff ───────────────────────────

    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] Guid? assignedTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var response = await complaintService.GetAllAsync(status, assignedTo, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await complaintService.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Staff)]
    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> AssignToMe(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await complaintService.AssignToMeAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Staff)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateComplaintStatusRequestDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await complaintService.UpdateStatusAsync(id, userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
