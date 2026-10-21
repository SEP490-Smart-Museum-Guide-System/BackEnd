namespace SMGS.Application.DTOs.Artifact.Response;

/// <summary>
/// DTO trả về khi quét QR code — bao gồm thông tin hiện vật + nội dung đã xuất bản.
/// </summary>
public class ArtifactScanResponseDto
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string MuseumName { get; set; } = string.Empty;
    public int? GalleryId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string AccessLevel { get; set; } = "free";
    public float? MapX { get; set; }
    public float? MapY { get; set; }

    /// <summary>Danh sách nội dung đã xuất bản theo các ngôn ngữ.</summary>
    public List<ArtifactScanContentDto> Contents { get; set; } = [];
}

/// <summary>
/// Nội dung hiện vật đã xuất bản cho một ngôn ngữ cụ thể.
/// </summary>
public class ArtifactScanContentDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
