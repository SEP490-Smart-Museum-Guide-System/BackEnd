using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Topic;

public class CreateTopicRequestDto
{
    [Range(1, int.MaxValue)]
    public int MuseumId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
}

public class UpdateTopicRequestDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AssignTopicsRequestDto
{
    [Required]
    public List<int> TopicIds { get; set; } = [];
}

public class TopicResponseDto
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ArtifactCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ArtifactTopicTagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

