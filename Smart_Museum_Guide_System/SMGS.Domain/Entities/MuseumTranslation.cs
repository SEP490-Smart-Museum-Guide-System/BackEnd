namespace SMGS.Domain.Entities;

public class MuseumTranslation
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? OpeningHours { get; set; }
    public Museum Museum { get; set; } = null!;
}
