using System.ComponentModel.DataAnnotations;
namespace SMGS.Application.DTOs.ArtifactContent.Request;
public class SubmitArtifactContentRequestDto
{
    [Required, RegularExpression(@"^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8})*$"), MaxLength(35)]
    public string LanguageCode { get; set; } = "vi";
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    [Required, MaxLength(100000)]
    public string Description { get; set; } = string.Empty;
}
