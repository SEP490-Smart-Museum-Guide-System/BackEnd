namespace SMGS.Domain.Entities;

/// <summary>
/// Một lượt tham quan tự do hoặc theo tour của khách tại bảo tàng.
/// Feedback chỉ được gửi sau khi Visit ở trạng thái Completed.
/// </summary>
public class Visit
{
    public int Id { get; set; }

    /// <summary>ID tài khoản khách thực hiện lượt tham quan.</summary>
    public Guid VisitorId { get; set; }

    /// <summary>
    /// FK tới Tour; null nếu khách tham quan tự do.
    /// </summary>
    public int? TourId { get; set; }

    /// <summary>Bảo tàng đang tham quan.</summary>
    public int MuseumId { get; set; }

    /// <summary>Thời điểm bắt đầu lượt tham quan.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm kết thúc / hoàn thành lượt tham quan.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Trạng thái lượt tham quan.
    /// InProgress | Completed | Abandoned
    /// </summary>
    public string Status { get; set; } = VisitStatus.InProgress;

    // Navigation
    public Museum? Museum { get; set; }
    public VisitFeedback? Feedback { get; set; }
}

public static class VisitStatus
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Abandoned = "Abandoned";
}
