using System.ComponentModel.DataAnnotations;
using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Application.DTOs.Experience;

public record TourRequest([Required, StringLength(200)] string Title, [StringLength(4000)] string Description,
    [Range(1, 1440)] int EstimatedMinutes, [Required, MinLength(1), MaxLength(100)] int[] ArtifactIds);
public record UpdateTourRequest([Range(1, int.MaxValue)] int Revision, [Required] TourRequest Tour);
public record CreateTourVersionRequest(int? CopyFromVersionId, TourRequest? Content);
public record ApproveTourVersionRequest(DateTime? PublicAt, [StringLength(1000)] string? Note);
public record RejectTourVersionRequest([Required, StringLength(1000)] string Note);
public record TourRecommendRequest(int[]? TopicIds, [Range(1, 1440)] int AvailableMinutes);
public record SaveRouteRequest([Range(1, int.MaxValue)] int TourVersionId, int[]? TopicIds, [Range(1, 1440)] int AvailableMinutes);
public record RecommendedTourDto(
    int TourId,
    int TourVersionId,
    int VersionNo,
    string Title,
    string Description,
    int EstimatedMinutes,
    IReadOnlyList<int> ArtifactIds,
    IReadOnlyList<int> MatchedTopicIds,
    double InterestScore,
    double TimeFitScore,
    double NoveltyScore,
    double TotalScore,
    string Explanation);
public record TourRecommendationResultDto(
    IReadOnlyList<RecommendedTourDto> Recommendations,
    string? FallbackCode,
    string? FallbackExplanation,
    RecommendedTourDto? FallbackSuggestion);
public record SavedRouteDto(
    int Id,
    int MuseumId,
    int TourId,
    int TourVersionId,
    int VersionNo,
    string Title,
    string Description,
    int EstimatedMinutes,
    IReadOnlyList<int> ArtifactIds,
    IReadOnlyList<int> TopicIds,
    int AvailableMinutes,
    double Score,
    string Explanation,
    DateTime SavedAt);
public record ScanQrRequest([Required, StringLength(200)] string QrCode, int? VisitId, Guid RequestId);
public record ScanResult(long RecognitionId, int ArtifactId, int? VisitId, DateTime RecognizedAt);
public record ProgressStop(int ArtifactId, int OrderNo, DateTime? LastVisitedAt);
public record VisitProgress(int VisitId, int? TourId, string Status, IReadOnlyList<ProgressStop> Stops, int? NextArtifactId, int? TourVersionId = null, int? TourVersionNo = null)
{
    public Guid? PersonalizedTourId { get; init; }
}
public record VisitorLocation(long RecognitionId, int ArtifactId, DateTime RecognizedAt);
public record VisitorRoute(int MapId, int Revision, int FromArtifactId, int ToArtifactId, RouteResult Route);
public record QuestionOptionRequest([Required, StringLength(1000)] string Content, bool IsCorrect);
public record QuestionRequest([Required, StringLength(4000)] string Content, [StringLength(4000)] string Explanation,
    [Required, StringLength(35)] string LanguageCode, [Required, MinLength(2), MaxLength(6)] QuestionOptionRequest[] Options);
public record ReviewQuestionRequest(bool Approve, [StringLength(1000)] string? Note);
public record QuizRequest([Required, StringLength(200)] string Title,
    [Required, MinLength(1), MaxLength(100)] int[] QuestionIds, [Range(1, 20)] int QuestionsPerAttempt);
public record StartAttemptRequest([Range(1, long.MaxValue)] long DailyEntryId, [RegularExpression("^(immediate|later)$")] string Mode = "later");
public record AnswerRequest([Range(1, int.MaxValue)] int QuestionId, [Range(1, int.MaxValue)] int OptionId);
public record PublicOption(int Id, string Content);
public record AttemptQuestion(int Id, string Content, IReadOnlyList<PublicOption> Options, int? SelectedOptionId,
    bool? IsCorrect, string? Explanation);
public record AttemptResult(long Id, long DailyEntryId, string Status, int TotalQuestions, int CorrectCount, int Score,
    DateTime StartedAt, DateTime? CompletedAt, IReadOnlyList<AttemptQuestion> Questions);
public record BadgeRequest([Required, StringLength(50)] string Code, [Required, StringLength(200)] string Name,
    int? MuseumId, [RegularExpression("^(scan_artifacts|quiz_correct)$")] string Metric, [Range(1, 100000)] int Threshold);
public record BadgeRule(string Metric, int Threshold);
public class ExperienceException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
public class ExperienceOptions
{
    [Range(0, 10000)] public int ScanPoints { get; set; }
    [Range(0, 10000)] public int CorrectAnswerPoints { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
}
