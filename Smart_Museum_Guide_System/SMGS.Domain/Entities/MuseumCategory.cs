namespace SMGS.Domain.Entities;

public class MuseumCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MuseumCategoryAssignment> Assignments { get; set; } = [];
}

public class MuseumCategoryAssignment
{
    public int MuseumId { get; set; }
    public Museum Museum { get; set; } = null!;
    public int CategoryId { get; set; }
    public MuseumCategory Category { get; set; } = null!;
}
