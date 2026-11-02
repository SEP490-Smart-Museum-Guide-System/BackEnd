using System.ComponentModel.DataAnnotations;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Topic;

namespace SMGS.Application.DTOs.Artifact;

public class ArtifactMediaResponseDto
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class Artifact3DModelResponseDto
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ModelUrl { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public bool AutoRotate { get; set; } = true;
    public string? CameraOrbit { get; set; }
    public string? FieldOfView { get; set; }
    public decimal Scale { get; set; } = 1.0m;
    public DateTime UpdatedAt { get; set; }
}

public class Update3DModelSettingsRequestDto
{
    [MaxLength(300)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? PosterUrl { get; set; }

    public bool AutoRotate { get; set; } = true;

    [MaxLength(100)]
    public string? CameraOrbit { get; set; }

    [MaxLength(50)]
    public string? FieldOfView { get; set; }

    [Range(0.01, 1000)]
    public decimal Scale { get; set; } = 1.0m;
}

public class ArtifactViewerResponseDto
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string MuseumName { get; set; } = string.Empty;
    public int? GalleryId { get; set; }
    public string? GalleryName { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string AccessLevel { get; set; } = "free";
    public List<ArtifactTopicTagDto> Topics { get; set; } = [];
    public Artifact3DModelResponseDto? Model3D { get; set; }
    public List<ArtifactMediaResponseDto> Media { get; set; } = [];
    public List<ArtifactScanContentDto> Contents { get; set; } = [];
}

