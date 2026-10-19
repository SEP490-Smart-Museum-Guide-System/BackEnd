using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Artifact.Request;

public class UpdateArtifactRequestDto
{
    public int? GalleryId { get; set; }

    [StringLength(100, ErrorMessage = "Mã nội bộ tối đa 100 ký tự.")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Tên hiện vật là bắt buộc.")]
    [StringLength(300, ErrorMessage = "Tên hiện vật tối đa 300 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Mã QR tối đa 200 ký tự.")]
    public string? QrCode { get; set; }

    public int? MapNodeId { get; set; }

    public float? MapX { get; set; }

    public float? MapY { get; set; }

    [StringLength(500, ErrorMessage = "Map icon tối đa 500 ký tự.")]
    public string? MapIcon { get; set; }

    /// <summary>free hoặc premium.</summary>
    [RegularExpression("^(free|premium)$", ErrorMessage = "AccessLevel phải là 'free' hoặc 'premium'.")]
    public string AccessLevel { get; set; } = "free";
}
