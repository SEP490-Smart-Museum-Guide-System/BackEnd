namespace SMGS.Domain.Entities;

public class Floor
{
    public int Id { get; set; }

    public int BuildingId { get; set; }

    public int LevelNo { get; set; }

    public string? Name { get; set; }

    public string? MapImageUrl { get; set; }

    // Navigation properties
    public ICollection<Gallery> Galleries { get; set; } = new List<Gallery>();
}
