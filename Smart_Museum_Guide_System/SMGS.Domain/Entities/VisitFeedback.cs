namespace SMGS.Domain.Entities;

/// <summary>
/// Đánh giá tổng hợp của khách sau khi hoàn thành một lượt tham quan.
/// Mỗi Visit chỉ có một VisitFeedback (one-to-one).
/// Thang điểm: 1–5 sao, kèm bình luận văn bản tự do.
/// Khách được phép chỉnh sửa trong vòng EditWindowHours kể từ lúc tạo.
/// </summary>
public class VisitFeedback
{
    public int Id { get; set; }

    /// <summary>Lượt tham quan tương ứng.</summary>
    public int VisitId { get; set; }

    // ── Điểm đánh giá (1–5) ──────────────────────────────────────

    /// <summary>Điểm tổng thể cho trải nghiệm chung.</summary>
    public int OverallRating { get; set; }

    /// <summary>Điểm cho chất lượng nội dung / thuyết minh.</summary>
    public int? ContentRating { get; set; }

    /// <summary>Điểm cho tour (tour mẫu hoặc tour AI gợi ý).</summary>
    public int? TourRating { get; set; }

    /// <summary>Điểm cho chatbot AI.</summary>
    public int? ChatbotRating { get; set; }

    // ── Bình luận ────────────────────────────────────────────────

    /// <summary>Nội dung bình luận văn bản tự do của khách.</summary>
    public string? Comment { get; set; }

    // ── Kiểm duyệt ───────────────────────────────────────────────

    /// <summary>
    /// Trạng thái hiển thị của feedback.
    /// Visible | Hidden
    /// </summary>
    public string ModerationStatus { get; set; } = FeedbackModerationStatus.Visible;

    /// <summary>Ghi chú nội bộ của System Staff khi ẩn/xử lý.</summary>
    public string? ModerationNote { get; set; }

    // ── Thời gian ────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────
    public Visit? Visit { get; set; }
    public StaffResponse? StaffResponse { get; set; }
}

public static class FeedbackModerationStatus
{
    public const string Visible = "Visible";
    public const string Hidden = "Hidden";
}
