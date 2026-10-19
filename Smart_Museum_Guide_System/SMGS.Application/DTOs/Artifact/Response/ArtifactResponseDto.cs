namespace SMGS.Application.DTOs.Artifact.Response;

public class ArtifactResponseDto
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public int? GalleryId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public int? MapNodeId { get; set; }
    public float? MapX { get; set; }
    public float? MapY { get; set; }
    public string? MapIcon { get; set; }
    public string AccessLevel { get; set; } = "free";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
