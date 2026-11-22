namespace SMGS.Application.DTOs.Notification;

public class NotificationResponseDto
{
    public long Id { get; set; }
    public Guid RecipientId { get; set; }
    public int? MuseumId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountResponseDto
{
    public int UnreadCount { get; set; }
}
