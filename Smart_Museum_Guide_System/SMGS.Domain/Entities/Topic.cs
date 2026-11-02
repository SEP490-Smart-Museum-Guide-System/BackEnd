namespace SMGS.Domain.Entities;

public class Topic
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Museum? Museum { get; set; }
    public ICollection<ArtifactTopic> ArtifactTopics { get; set; } = new List<ArtifactTopic>();
}

public class ArtifactTopic
{
    public int ArtifactId { get; set; }
    public int TopicId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Artifact Artifact { get; set; } = null!;
    public Topic Topic { get; set; } = null!;
}

