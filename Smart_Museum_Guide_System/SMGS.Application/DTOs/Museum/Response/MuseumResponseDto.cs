namespace SMGS.Application.DTOs.Museum.Response;

public record MuseumCategorySummary(int Id, string Code, string Name);

public class MuseumResponseDto
{
    public IReadOnlyList<MuseumCategorySummary> Categories { get; set; } = [];
    public int Id { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? CityCode { get; set; }
    public double? DistanceKm { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? OpeningHours { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
