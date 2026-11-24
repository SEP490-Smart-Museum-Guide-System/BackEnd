namespace SMGS.Domain.Entities;

/// <summary>Localized display name. Long description and narration use the reviewed ArtifactContent workflow.</summary>
public class ArtifactTranslation
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Artifact Artifact { get; set; } = null!;
}
