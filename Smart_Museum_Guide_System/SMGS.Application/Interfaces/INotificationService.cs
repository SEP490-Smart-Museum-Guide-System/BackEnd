using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Notification;

namespace SMGS.Application.Interfaces;

public interface INotificationService
{
    Task<ResponseDto<PaginatedResponseDto<NotificationResponseDto>>> GetMyNotificationsAsync(
        Guid userId, bool? unreadOnly, string? type, int page, int pageSize);
    Task<ResponseDto<UnreadCountResponseDto>> GetUnreadCountAsync(Guid userId);
    Task<ResponseDto<NotificationResponseDto>> MarkAsReadAsync(long notificationId, Guid userId);
    Task<ResponseDto<int>> MarkAllAsReadAsync(Guid userId);
    Task<ResponseDto<object>> DeleteAsync(long notificationId, Guid userId);
}

public interface INotificationPublisher
{
    Task NotifyUserAsync(
        Guid recipientId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null,
        int? museumId = null);

    Task NotifyArtifactCuratorsAsync(
        int artifactId,
        Guid excludeUserId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null);

    Task NotifySystemStaffAsync(
        Guid excludeUserId,
        string type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        string? actionUrl = null);
}

public interface INotificationRealtimeDispatcher
{
    Task PushNotificationAsync(Guid recipientId, NotificationResponseDto notification, int unreadCount, CancellationToken ct = default);
    Task PushUnreadCountAsync(Guid recipientId, int unreadCount, CancellationToken ct = default);
}
