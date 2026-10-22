using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Navigation;

public class CoordinateDto
{
    [Required, Range(-90, 90)] public double? Latitude { get; set; }
    [Required, Range(-180, 180)] public double? Longitude { get; set; }
    public bool IsValid() => Latitude is double lat && Longitude is double lon
        && double.IsFinite(lat) && double.IsFinite(lon) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
}

public class OutdoorRouteRequestDto
{
    [Range(1, int.MaxValue)] public int MuseumId { get; set; }
    [Required] public CoordinateDto? Origin { get; set; }
    [Required] public string TravelMode { get; set; } = "DRIVE";
}

public class OutdoorRouteResponseDto
{
    public int MuseumId { get; set; }
    public CoordinateDto Destination { get; set; } = new();
    public string TravelMode { get; set; } = "DRIVE";
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public List<CoordinateDto> RoutePoints { get; set; } = [];
}
