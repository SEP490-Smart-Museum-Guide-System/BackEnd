using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SMGS.Application.DTOs.Notification;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

[Authorize]
public class NotificationHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public class SignalRNotificationDispatcher(IHubContext<NotificationHub> hubContext) : INotificationRealtimeDispatcher
{
    public async Task PushNotificationAsync(
        Guid recipientId,
        NotificationResponseDto notification,
        int unreadCount,
        CancellationToken ct = default)
    {
        var group = hubContext.Clients.Group(NotificationHub.UserGroup(recipientId));
        await group.SendAsync("ReceiveNotification", notification, ct);
        await group.SendAsync("UnreadCountChanged", new UnreadCountResponseDto { UnreadCount = unreadCount }, ct);
    }

    public Task PushUnreadCountAsync(Guid recipientId, int unreadCount, CancellationToken ct = default) =>
        hubContext.Clients.Group(NotificationHub.UserGroup(recipientId))
            .SendAsync("UnreadCountChanged", new UnreadCountResponseDto { UnreadCount = unreadCount }, ct);
}
