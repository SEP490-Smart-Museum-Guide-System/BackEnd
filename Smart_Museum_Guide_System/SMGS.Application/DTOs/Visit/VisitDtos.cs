using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Visit;

public class StartVisitRequestDto
{
    [Required]
    public int MuseumId { get; set; }

    public int? TourId { get; set; }
}

public class VisitResponseDto
{
    public int Id { get; set; }
    public Guid VisitorId { get; set; }
    public int MuseumId { get; set; }
    public int? TourId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool HasFeedback { get; set; }
}
