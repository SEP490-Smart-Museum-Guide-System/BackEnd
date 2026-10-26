namespace SMGS.Application.DTOs.Feedback.Response;

public class FeedbackResponseDto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int MuseumId { get; set; }
    public int? TourId { get; set; }
    public Guid VisitorId { get; set; }

    public int OverallRating { get; set; }
    public int? ContentRating { get; set; }
    public int? TourRating { get; set; }
    public int? ChatbotRating { get; set; }
    public string? Comment { get; set; }

    public string ModerationStatus { get; set; } = string.Empty;
    public string? ModerationNote { get; set; }

    /// <summary>Khách còn được sửa feedback này hay không.</summary>
    public bool CanEdit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public StaffResponseDto? StaffResponse { get; set; }
}

public class StaffResponseDto
{
    public int Id { get; set; }
    public Guid RespondedBy { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
