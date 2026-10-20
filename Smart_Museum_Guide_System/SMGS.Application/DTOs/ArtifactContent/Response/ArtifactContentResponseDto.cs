namespace SMGS.Application.DTOs.ArtifactContent.Response;
public class ArtifactContentResponseDto
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public int MuseumId { get; set; }
    public string ArtifactName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
