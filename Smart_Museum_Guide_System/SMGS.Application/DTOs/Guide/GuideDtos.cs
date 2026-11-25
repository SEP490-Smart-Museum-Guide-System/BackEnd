using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Guide;

public class GuideMessageRequest
{
    [Required, StringLength(1000)] public string Question { get; set; } = "";
    [MaxLength(8)] public GuideTurn[] History { get; set; } = [];
    public bool UseSavedHistory { get; set; }
}

public record GuideTurn(
    [Required, RegularExpression("^(user|assistant)$")] string Role,
    [Required, StringLength(4000)] string Text);

public record GuideSource(int ContentVersionId, int VersionNo, string Title, string LanguageCode);
public record GuideAnswer(int ArtifactId, string Answer, bool Supported, IReadOnlyList<GuideSource> Sources);
public record GuideContext(int ArtifactId, string ArtifactName, string Title, string Description, string NarrationText);
public record GuideModelAnswer(string Answer, bool Supported);

public record GuideChatMessageDto(
    long Id,
    int ArtifactId,
    string ArtifactName,
    int MuseumId,
    string MuseumName,
    string Question,
    string Answer,
    bool Supported,
    IReadOnlyList<GuideSource> Sources,
    DateTime CreatedAt);

public record GuideConversationSummaryDto(
    int ArtifactId,
    string ArtifactName,
    int MuseumId,
    string MuseumName,
    int ExchangeCount,
    string LastQuestion,
    string LastAnswer,
    bool LastSupported,
    DateTime LastAskedAt);

public record GuideAdminLogDto(
    long Id,
    Guid UserId,
    int ArtifactId,
    string ArtifactName,
    int MuseumId,
    string MuseumName,
    string Question,
    string Answer,
    bool Supported,
    string LanguageCode,
    int? ContentVersionNo,
    DateTime CreatedAt);

public class GuideException(string message, int statusCode = 503) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
