namespace SMGS.Domain.Entities;

public class ArtifactMedia
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string MediaType { get; set; } = "image"; // image, audio, video
    public string Title { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Artifact Artifact { get; set; } = null!;
}

public class Artifact3DModel
{
    public int Id { get; set; }
    public int ArtifactId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileFormat { get; set; } = "glb"; // glb, gltf
    public string ContentType { get; set; } = "model/gltf-binary";
    public long FileSize { get; set; }
    public string? PosterUrl { get; set; }
    public bool AutoRotate { get; set; } = true;
    public string? CameraOrbit { get; set; }
    public string? FieldOfView { get; set; }
    public decimal Scale { get; set; } = 1.0m;
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Artifact Artifact { get; set; } = null!;
}

