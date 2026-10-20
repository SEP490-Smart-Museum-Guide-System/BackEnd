using SMGS.Domain.Constants;
namespace SMGS.Domain.Entities;
public class ArtifactContentVersion
{
    public int Id { get; set; }
    public int ArtifactContentId { get; set; }
    public int VersionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; } = true;
    public string Status { get; set; } = ContentStatuses.PendingReview;
    public bool IsCurrent { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ArtifactContent Content { get; set; } = null!;
}
