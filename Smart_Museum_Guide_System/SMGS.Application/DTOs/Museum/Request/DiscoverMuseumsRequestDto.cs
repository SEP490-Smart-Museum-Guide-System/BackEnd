using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Museum.Request;

public class DiscoverMuseumsRequestDto
{
    [StringLength(200)] public string? Keyword { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [StringLength(2)] public string? CityCode { get; set; }
    public bool? IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? SortBy { get; set; }
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 50)] public int PageSize { get; set; } = 20;
}
