using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Complaint.Request;

public class CreateComplaintRequestDto
{
    [Required, MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Body { get; set; } = string.Empty;
}

public class UpdateComplaintStatusRequestDto
{
    /// <summary>InProgress | Resolved | Closed</summary>
    [Required, RegularExpression("^(InProgress|Resolved|Closed)$")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ResolutionNote { get; set; }
}
