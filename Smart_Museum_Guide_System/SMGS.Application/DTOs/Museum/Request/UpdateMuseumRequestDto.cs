using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Museum.Request;

public class UpdateMuseumRequestDto
{
    [Required(ErrorMessage = "Tên bảo tàng là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên bảo tàng tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(2)]
    public string? CityCode { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude phải từ -90 đến 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude phải từ -180 đến 180.")]
    public decimal? Longitude { get; set; }

    [StringLength(500)]
    public string? OpeningHours { get; set; }

    [StringLength(1000)]
    public string? CoverImageUrl { get; set; }
}
