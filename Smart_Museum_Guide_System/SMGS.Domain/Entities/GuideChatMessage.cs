namespace SMGS.Domain.Entities;

public class GuideChatMessage
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int ArtifactId { get; set; }
    public int MuseumId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool Supported { get; set; }
    public int? ContentVersionId { get; set; }
    public int? ContentVersionNo { get; set; }
    public string? ContentTitle { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
