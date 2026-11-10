namespace SMGS.Domain.Constants;

/// <summary>
/// Các trạng thái của một phiên bản tour (TourVersion) trong luồng kiểm duyệt WF-02.
/// Lộ trình hợp lệ: Draft -> Submitted -> Approved / Rejected.
/// Phiên bản chỉ công khai với khách khi ở trạng thái Approved và PublicAt &lt;= thời điểm hiện tại.
/// </summary>
public static class TourVersionStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

