using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.Interfaces;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    /// <summary>Lấy danh sách thông báo của người dùng đang đăng nhập (hỗ trợ lọc chưa đọc & phân trang).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await notificationService.GetMyNotificationsAsync(userId, unreadOnly, type, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Lấy số lượng thông báo chưa đọc để hiển thị badge trên icon chuông.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await notificationService.GetUnreadCountAsync(userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Đánh dấu một thông báo là đã đọc.</summary>
    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await notificationService.MarkAsReadAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Đánh dấu tất cả thông báo của tôi là đã đọc.</summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await notificationService.MarkAllAsReadAsync(userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Xóa một thông báo của tôi.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var response = await notificationService.DeleteAsync(id, userId);
        return StatusCode(response.StatusCode, response);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
