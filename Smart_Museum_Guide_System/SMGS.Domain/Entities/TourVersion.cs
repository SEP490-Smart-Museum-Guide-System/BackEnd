using SMGS.Domain.Constants;

namespace SMGS.Domain.Entities;

/// <summary>
/// Một phiên bản cụ thể của Tour (WF-02: F04, F06).
/// Sau khi chuyển sang Submitted, phiên bản là bất biến (immutable) để bảo toàn lịch sử kiểm duyệt
/// và tiến độ của các lượt tham quan (Visit) đã gắn với phiên bản đó.
/// </summary>
public class TourVersion
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int VersionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public string Status { get; set; } = TourVersionStatuses.Draft;

    /// <summary>Thời điểm hẹn công bố (UTC). Chỉ có ý nghĩa khi Status = Approved.</summary>
    public DateTime? PublicAt { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public ICollection<TourVersionItem> Items { get; set; } = new List<TourVersionItem>();

    /// <summary>
    /// Kiểm tra phiên bản có đang công khai với khách tại thời điểm <paramref name="nowUtc"/> hay không.
    /// Bản bị từ chối hoặc có PublicAt ở tương lai sẽ không hiển thị với khách.
    /// </summary>
    public bool IsPublicAt(DateTime nowUtc) =>
        Status == TourVersionStatuses.Approved && PublicAt.HasValue && PublicAt.Value <= nowUtc;

    /// <summary>Nhân viên gửi bản nháp lên chờ Curator duyệt (Draft -> Submitted).</summary>
    public void Submit(Guid staffId, DateTime nowUtc)
    {
        if (Status != TourVersionStatuses.Draft)
        {
            throw new InvalidOperationException("Chỉ có thể gửi duyệt phiên bản đang ở trạng thái nháp (draft).");
        }

        Status = TourVersionStatuses.Submitted;
        SubmittedBy = staffId;
        SubmittedAt = nowUtc;
    }

    /// <summary>Curator duyệt phiên bản và ấn định thời điểm công bố PublicAt (Submitted -> Approved).</summary>
    public void Approve(Guid curatorId, DateTime publicAtUtc, string? note, DateTime nowUtc)
    {
        if (Status != TourVersionStatuses.Submitted)
        {
            throw new InvalidOperationException("Chỉ có thể duyệt phiên bản đang ở trạng thái chờ duyệt (submitted).");
        }

        if (SubmittedBy == curatorId || CreatedBy == curatorId)
        {
            throw new InvalidOperationException("Người tạo hoặc gửi bản nháp không được tự duyệt phiên bản của chính mình.");
        }

        Status = TourVersionStatuses.Approved;
        PublicAt = publicAtUtc;
        ReviewedBy = curatorId;
        ReviewedAt = nowUtc;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Curator từ chối phiên bản kèm ghi chú bắt buộc (Submitted -> Rejected).</summary>
    public void Reject(Guid curatorId, string note, DateTime nowUtc)
    {
        if (Status != TourVersionStatuses.Submitted)
        {
            throw new InvalidOperationException("Chỉ có thể từ chối phiên bản đang ở trạng thái chờ duyệt (submitted).");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Ghi chú lý do từ chối là bắt buộc.", nameof(note));
        }

        Status = TourVersionStatuses.Rejected;
        PublicAt = null;
        ReviewedBy = curatorId;
        ReviewedAt = nowUtc;
        ReviewNote = note.Trim();
    }
}

/// <summary>Điểm dừng hiện vật thuộc một TourVersion cụ thể.</summary>
public class TourVersionItem
{
    public int Id { get; set; }
    public int TourVersionId { get; set; }
    public int ArtifactId { get; set; }
    public int OrderNo { get; set; }
}

