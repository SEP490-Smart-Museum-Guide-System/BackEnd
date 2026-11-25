using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Dashboard;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    /// <summary>Dashboard tổng quan toàn hệ thống dành cho Admin (doanh thu, bảo tàng, hiện vật, người dùng, AI, đánh giá).</summary>
    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(ResponseDto<AdminDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminDashboard(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var response = await dashboardService.GetAdminDashboardAsync(from, to, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Dashboard giám sát vận hành dành cho System Staff / Admin (khiếu nại, kiểm duyệt feedback, đối soát thanh toán, chất lượng AI).</summary>
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    [HttpGet("staff")]
    [ProducesResponseType(typeof(ResponseDto<SystemStaffDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemStaffDashboard(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var response = await dashboardService.GetSystemStaffDashboardAsync(from, to, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Dashboard thống kê chi tiết của một bảo tàng dành cho Museum Staff, Curator hoặc Admin.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.MuseumStaff},{RoleNames.Curator}")]
    [HttpGet("museums/{museumId:int}")]
    [ProducesResponseType(typeof(ResponseDto<MuseumDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMuseumDashboard(
        int museumId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var response = await dashboardService.GetMuseumDashboardAsync(museumId, userId, isAdmin, from, to, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
