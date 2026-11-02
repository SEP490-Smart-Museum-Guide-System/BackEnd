using System.ComponentModel.DataAnnotations;
namespace SMGS.Application.DTOs.ArtifactContent.Request;
public class ReviewArtifactContentRequestDto
{
    [MaxLength(2000)]
    public string? Note { get; set; }
}
