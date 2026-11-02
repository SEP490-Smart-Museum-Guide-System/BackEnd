using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Feedback.Request;

public class CreateFeedbackRequestDto
{
    [Required]
    public int VisitId { get; set; }

    [Required, Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int? ContentRating { get; set; }

    [Range(1, 5)]
    public int? TourRating { get; set; }

    [Range(1, 5)]
    public int? ChatbotRating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}

public class UpdateFeedbackRequestDto
{
    [Required, Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int? ContentRating { get; set; }

    [Range(1, 5)]
    public int? TourRating { get; set; }

    [Range(1, 5)]
    public int? ChatbotRating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}

public class ModerateFeedbackRequestDto
{
    /// <summary>Visible | Hidden</summary>
    [Required, RegularExpression("^(Visible|Hidden)$")]
    public string ModerationStatus { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ModerationNote { get; set; }
}

public class StaffResponseRequestDto
{
    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
