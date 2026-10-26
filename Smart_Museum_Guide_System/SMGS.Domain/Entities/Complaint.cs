namespace SMGS.Domain.Entities;

/// <summary>
/// Khiếu nại / yêu cầu hỗ trợ của khách gửi tới System Staff.
/// Tách biệt với VisitFeedback: khiếu nại có flow xử lý riêng.
/// </summary>
public class Complaint
{
    public int Id { get; set; }

    /// <summary>Khách gửi khiếu nại.</summary>
    public Guid VisitorId { get; set; }

    /// <summary>Tiêu đề khiếu nại.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Nội dung chi tiết khiếu nại.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái xử lý.
    /// New | InProgress | Resolved | Closed
    /// </summary>
    public string Status { get; set; } = ComplaintStatus.New;

    /// <summary>System Staff đang xử lý khiếu nại này.</summary>
    public Guid? AssignedTo { get; set; }

    /// <summary>Ghi chú nội bộ của System Staff.</summary>
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public static class ComplaintStatus
{
    public const string New = "New";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
}
