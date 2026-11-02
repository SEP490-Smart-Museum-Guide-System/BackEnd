namespace SMGS.Application.DTOs.Complaint.Response;

public class ComplaintResponseDto
{
    public int Id { get; set; }
    public Guid VisitorId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssignedTo { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
