namespace SMGS.Domain.Entities;

public class Gallery
{
    public int Id { get; set; }

    public int FloorId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navigation property
    public Floor? Floor { get; set; }
    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
