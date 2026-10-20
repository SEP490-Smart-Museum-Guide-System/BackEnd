namespace SMGS.Domain.Entities;
public class ArtifactContent
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    // Shared concurrency token protects version numbering and publication.
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Artifact Artifact { get; set; } = null!;
    public ICollection<ArtifactContentVersion> Versions { get; set; } = new List<ArtifactContentVersion>();
}
