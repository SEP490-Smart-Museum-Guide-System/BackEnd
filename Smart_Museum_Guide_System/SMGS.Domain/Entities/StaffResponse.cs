namespace SMGS.Domain.Entities;

/// <summary>
/// Phản hồi chính thức của Museum Staff đối với một feedback của khách.
/// Mỗi VisitFeedback có tối đa một StaffResponse.
/// </summary>
public class StaffResponse
{
    public int Id { get; set; }

    /// <summary>Feedback được phản hồi.</summary>
    public int FeedbackId { get; set; }

    /// <summary>Nhân viên đã phản hồi (Museum Staff).</summary>
    public Guid RespondedBy { get; set; }

    /// <summary>Nội dung phản hồi.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public VisitFeedback? Feedback { get; set; }
}
