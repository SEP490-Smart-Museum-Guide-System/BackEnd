using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SMGS.Application.DTOs.Guide;

public class StaffAiRequest
{
    [Required, RegularExpression("^(narration|quiz)$")] public string Mode { get; set; } = "narration";
    [StringLength(6000)] public string SourceText { get; set; } = "";
    [RegularExpression("^(children|general|scholar)$")] public string Audience { get; set; } = "general";
    [RegularExpression("^(vivid|inspiring|academic)$")] public string Tone { get; set; } = "vivid";
    [RegularExpression("^(easy|medium|hard)$")] public string Difficulty { get; set; } = "medium";
    [MaxLength(5)] public string[] ExcludeQuestions { get; set; } = [];
}

public record StaffAiSource(int ArtifactId, string ArtifactName, int? ContentVersionId, int? VersionNo, string Text);
public record StaffAiResult(int ArtifactId, string Mode, string LanguageCode, bool IsDraft, StaffAiSource Source, JsonElement Draft);
public record StaffAiModelRequest(int ArtifactId, string ArtifactName, string SourceText, string Mode,
    string Audience, string Tone, string Difficulty, string[] ExcludeQuestions);
