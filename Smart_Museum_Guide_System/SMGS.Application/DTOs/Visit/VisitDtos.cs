using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Visit;

public class StartVisitRequestDto
{
    [Required]
    public int MuseumId { get; set; }

    public int? TourId { get; set; }

    /// <summary>
    /// Tùy chọn: nếu khách bắt đầu từ một lộ trình gợi ý đã lưu (SavedRoute),
    /// hệ thống sẽ gắn Visit vào đúng TourVersion đã lưu ở lộ trình đó.
    /// </summary>
    public int? SavedRouteId { get; set; }
    public Guid? PersonalizedTourId { get; set; }
}

public class VisitResponseDto
{
    public int Id { get; set; }
    public Guid VisitorId { get; set; }
    public int MuseumId { get; set; }
    public int? TourId { get; set; }
    public int? TourVersionId { get; set; }
    public int? TourVersionNo { get; set; }
    public Guid? PersonalizedTourId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool HasFeedback { get; set; }
}
