namespace SMGS.Domain.Entities;

/// <summary>
/// Lộ trình gợi ý được khách lưu lại để mở lại sau (WF-02: F05).
/// Lưu cứng <see cref="TourVersionId"/> và tham số đầu vào để khi mở lại
/// luôn trả về đúng phiên bản đã được gợi ý lúc lưu.
/// </summary>
public class SavedRoute
{
    public int Id { get; set; }
    public Guid VisitorId { get; set; }
    public int MuseumId { get; set; }
    public int TourId { get; set; }
    public int TourVersionId { get; set; }

    /// <summary>Danh sách TopicId sở thích lúc khách yêu cầu gợi ý (JSON mảng số nguyên).</summary>
    public string TopicIdsJson { get; set; } = "[]";

    public int AvailableMinutes { get; set; }
    public double Score { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

