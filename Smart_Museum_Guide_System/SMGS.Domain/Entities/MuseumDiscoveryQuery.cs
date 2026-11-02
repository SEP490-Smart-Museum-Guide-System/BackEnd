namespace SMGS.Domain.Entities;

public class MuseumDiscoveryQuery
{
    public string? Keyword { get; set; }
    public string? CityCode { get; set; }
    public bool? IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? SortBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record MuseumDiscoveryItem(Museum Museum, double? DistanceKm);
