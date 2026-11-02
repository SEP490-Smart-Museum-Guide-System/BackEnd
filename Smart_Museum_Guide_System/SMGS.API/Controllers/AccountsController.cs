using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Account;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Admin)]
public class AccountsController(IAccountAdminService accountService) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet("roles")]
    public IActionResult GetRoles() => Ok(ResponseDto<string[]>.Success(
    [
        RoleNames.Admin,
        RoleNames.Curator,
        RoleNames.Staff,
        RoleNames.MuseumStaff,
        RoleNames.Visitor
    ]));

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int? museumId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await accountService.GetAccountsAsync(keyword, role, isActive, museumId, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("accounts/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await accountService.GetAccountByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffAccountRequestDto dto)
    {
        var response = await accountService.CreateStaffAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequestDto dto)
    {
        var response = await accountService.UpdateAccountAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPatch("accounts/{id:guid}/status")]
    [HttpPut("accounts/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetAccountStatusRequestDto dto)
    {
        var response = await accountService.SetAccountStatusAsync(id, dto.IsActive, ActorId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("accounts/{id:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequestDto dto)
    {
        var response = await accountService.AssignRolesAsync(id, dto, ActorId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("accounts/{id:guid}/assignments")]
    public async Task<IActionResult> GetUserAssignments(Guid id)
    {
        var response = await accountService.GetAssignmentsAsync(id, null, null, null);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("accounts/{id:guid}/assignments")]
    public async Task<IActionResult> AssignScope(Guid id, [FromBody] ScopeAssignmentRequestDto dto)
    {
        var response = await accountService.AssignScopeAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("accounts/{id:guid}/assignments/{assignmentId:int}")]
    public async Task<IActionResult> RevokeUserScope(Guid id, int assignmentId)
    {
        var response = await accountService.RevokeScopeAsync(assignmentId, id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? userId,
        [FromQuery] int? museumId,
        [FromQuery] int? galleryId,
        [FromQuery] int? artifactId)
    {
        var response = await accountService.GetAssignmentsAsync(userId, museumId, galleryId, artifactId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("assignments/{assignmentId:int}")]
    public async Task<IActionResult> RevokeScope(int assignmentId)
    {
        var response = await accountService.RevokeScopeAsync(assignmentId);
        return StatusCode(response.StatusCode, response);
    }
}

