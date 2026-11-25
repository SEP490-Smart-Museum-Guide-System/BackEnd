using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Notification;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class NotificationService(
    ApplicationDbContext db,
    INotificationRealtimeDispatcher? realtime = null) : INotificationService, INotificationPublisher
{
    public async Task<ResponseDto<PaginatedResponseDto<NotificationResponseDto>>> GetMyNotificationsAsync(
        Guid userId, bool? unreadOnly, string? type, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToUpperInvariant();
            query = query.Where(n => n.Type == normalizedType);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ResponseDto<PaginatedResponseDto<NotificationResponseDto>>.Success(new()
        {
            Items = items.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ResponseDto<UnreadCountResponseDto>> GetUnreadCountAsync(Guid userId)
    {
        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientId == userId && !n.IsRead);
        return ResponseDto<UnreadCountResponseDto>.Success(new() { UnreadCount = count });
    }

    public async Task<ResponseDto<NotificationResponseDto>> MarkAsReadAsync(long notificationId, Guid userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);
        if (notification is null)
            return ResponseDto<NotificationResponseDto>.Fail("Không tìm thấy thông báo.", 404);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (realtime is not null)
            {
                var unread = await db.Notifications.AsNoTracking()
                    .CountAsync(n => n.RecipientId == userId && !n.IsRead);
                await SafeDispatchAsync(() => realtime.PushUnreadCountAsync(userId, unread));
            }
        }

        return ResponseDto<NotificationResponseDto>.Success(Map(notification), "Đã đánh dấu đã đọc.");
    }

    public async Task<ResponseDto<int>> MarkAllAsReadAsync(Guid userId)
    {
        var unreadList = await db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadList.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in unreadList)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            await db.SaveChangesAsync();

            if (realtime is not null)
            {
                await SafeDispatchAsync(() => realtime.PushUnreadCountAsync(userId, 0));
            }
        }

        return ResponseDto<int>.Success(unreadList.Count, $"Đã đánh dấu đã đọc {unreadList.Count} thông báo.");
    }

    public async Task<ResponseDto<object>> DeleteAsync(long notificationId, Guid userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);
        if (notification is null)
            return ResponseDto<object>.Fail("Không tìm thấy thông báo.", 404);

        var wasUnread = !notification.IsRead;
        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();

        if (wasUnread && realtime is not null)
        {
            var unread = await db.Notifications.AsNoTracking()
                .CountAsync(n => n.RecipientId == userId && !n.IsRead);
            await SafeDispatchAsync(() => realtime.PushUnreadCountAsync(userId, unread));
        }

        return ResponseDto<object>.Success(null!, "Đã xóa thông báo.");
    }

    // ─────────────────────────── INotificationPublisher ───────────────────────────

    public async Task NotifyUserAsync(
        Guid recipientId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null,
        int? museumId = null)
    {
        if (recipientId == Guid.Empty) return;
        await CreateAndDispatchAsync([recipientId], type, title, body, referenceType, referenceId, actionUrl, museumId);
    }

    public async Task NotifyArtifactCuratorsAsync(
        int artifactId,
        Guid excludeUserId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null)
    {
        var artifact = await db.Artifacts.AsNoTracking()
            .Where(a => a.Id == artifactId)
            .Select(a => new { a.Id, a.MuseumId, a.GalleryId })
            .FirstOrDefaultAsync();
        if (artifact is null) return;

        // 1. Tìm tất cả Curator được phân công phạm vi Bảo tàng / Phòng trưng bày / Hiện vật này
        var curatorIds = await (from ur in db.UserRoles.AsNoTracking()
                                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                join a in db.StaffAssignments.AsNoTracking() on u.Id equals a.UserId
                                where r.Name == RoleNames.Curator
                                      && u.IsActive
                                      && u.Id != excludeUserId
                                      && a.MuseumId == artifact.MuseumId
                                      && ((a.GalleryId == null && a.ArtifactId == null)
                                          || (artifact.GalleryId != null && a.GalleryId == artifact.GalleryId && a.ArtifactId == null)
                                          || a.ArtifactId == artifact.Id)
                                select u.Id).Distinct().ToListAsync();

        // 2. Fallback: Nếu Curator chưa được gán StaffAssignment cụ thể (ví dụ chỉ chạy --seed tạo tài khoản
        // hoặc Admin tạo Curator mới nhưng chưa gán scope), gửi cho tất cả Curator đang hoạt động trong hệ thống
        if (curatorIds.Count == 0)
        {
            curatorIds = await (from ur in db.UserRoles.AsNoTracking()
                                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                where r.Name == RoleNames.Curator
                                      && u.IsActive
                                      && u.Id != excludeUserId
                                select u.Id).Distinct().ToListAsync();
        }

        if (curatorIds.Count == 0) return;
        await CreateAndDispatchAsync(curatorIds, type, title, body, referenceType, referenceId, actionUrl, artifact.MuseumId);
    }

    public async Task NotifyMuseumCuratorsAsync(
        int museumId,
        Guid excludeUserId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null)
    {
        var curatorIds = await (from ur in db.UserRoles.AsNoTracking()
                                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                join a in db.StaffAssignments.AsNoTracking() on u.Id equals a.UserId
                                where r.Name == RoleNames.Curator
                                      && u.IsActive
                                      && u.Id != excludeUserId
                                      && a.MuseumId == museumId
                                      && a.GalleryId == null
                                      && a.ArtifactId == null
                                select u.Id).Distinct().ToListAsync();

        if (curatorIds.Count == 0)
        {
            curatorIds = await (from ur in db.UserRoles.AsNoTracking()
                                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                where r.Name == RoleNames.Curator
                                      && u.IsActive
                                      && u.Id != excludeUserId
                                select u.Id).Distinct().ToListAsync();
        }

        if (curatorIds.Count == 0) return;
        await CreateAndDispatchAsync(curatorIds, type, title, body, referenceType, referenceId, actionUrl, museumId);
    }

    public async Task NotifySystemStaffAsync(
        Guid excludeUserId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null)
    {
        var staffIds = await (from ur in db.UserRoles.AsNoTracking()
                              join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                              join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                              where (r.Name == RoleNames.Staff || r.Name == RoleNames.Admin)
                                    && u.IsActive
                                    && u.Id != excludeUserId
                              select u.Id).Distinct().ToListAsync();

        if (staffIds.Count == 0) return;
        await CreateAndDispatchAsync(staffIds, type, title, body, referenceType, referenceId, actionUrl, null);
    }

    private async Task CreateAndDispatchAsync(
        IReadOnlyCollection<Guid> recipientIds,
        string type,
        string title,
        string body,
        string? referenceType,
        string? referenceId,
        string? actionUrl,
        int? museumId)
    {
        var now = DateTime.UtcNow;
        var entities = recipientIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(id => new Notification
            {
                RecipientId = id,
                MuseumId = museumId,
                Type = type,
                Title = title.Length > 300 ? title[..300] : title,
                Body = body.Length > 1000 ? body[..1000] : body,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ActionUrl = actionUrl,
                IsRead = false,
                CreatedAt = now
            })
            .ToList();

        if (entities.Count == 0) return;

        db.Notifications.AddRange(entities);
        await db.SaveChangesAsync();

        if (realtime is not null)
        {
            foreach (var entity in entities)
            {
                var unreadCount = await db.Notifications.AsNoTracking()
                    .CountAsync(n => n.RecipientId == entity.RecipientId && !n.IsRead);
                var dto = Map(entity);
                await SafeDispatchAsync(() => realtime.PushNotificationAsync(entity.RecipientId, dto, unreadCount));
            }
        }
    }

    private static async Task SafeDispatchAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch
        {
            // Realtime push failures must never fail the primary business transaction.
        }
    }

    public static NotificationResponseDto Map(Notification n) => new()
    {
        Id = n.Id,
        RecipientId = n.RecipientId,
        MuseumId = n.MuseumId,
        Type = n.Type,
        Title = n.Title,
        Body = n.Body,
        ReferenceType = n.ReferenceType,
        ReferenceId = n.ReferenceId,
        ActionUrl = n.ActionUrl,
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt
    };
}
