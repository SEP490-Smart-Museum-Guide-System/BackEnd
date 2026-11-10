using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.Visit;
using SMGS.Domain.Entities;

namespace SMGS.Application.Interfaces;

public interface ITourService
{
    Task<Tour> CreateAsync(int museumId, TourRequest request, Guid user, bool admin);
    Task<Tour> UpdateStopsAsync(int id, UpdateTourRequest request, Guid user, bool admin);
    Task<Tour> PublishAsync(int id, int revision, Guid user, bool admin);
    Task<Tour> GetAsync(int id, Guid? user, bool admin);
    Task<IReadOnlyList<Tour>> ListAsync(int museumId, bool drafts, Guid? user, bool admin, int page, int pageSize);

    // WF-02 (F04): Quản lý phiên bản tour (TourVersion), gửi duyệt, Curator duyệt/từ chối và hẹn giờ công bố PublicAt.
    Task<TourVersion> CreateVersionDraftAsync(int tourId, CreateTourVersionRequest request, Guid user, bool admin);
    Task<TourVersion> UpdateVersionDraftAsync(int versionId, TourRequest request, Guid user, bool admin);
    Task<TourVersion> SubmitVersionAsync(int versionId, Guid user, bool admin);
    Task<TourVersion> ApproveVersionAsync(int versionId, ApproveTourVersionRequest request, Guid curator, bool admin);
    Task<TourVersion> RejectVersionAsync(int versionId, RejectTourVersionRequest request, Guid curator, bool admin);
    Task<TourVersion> GetPublicTourVersionAsync(int tourId);
    Task<IReadOnlyList<TourVersion>> ListVersionsAsync(int tourId, Guid user, bool admin);

    // WF-02 (F05): Gợi ý tour theo sở thích + thời gian rảnh, fallback có giải thích, và lưu lộ trình để mở lại.
    Task<TourRecommendationResultDto> RecommendAsync(int museumId, TourRecommendRequest request, Guid? visitor);
    Task<SavedRouteDto> SaveRouteAsync(Guid visitor, SaveRouteRequest request);
    Task<IReadOnlyList<SavedRouteDto>> ListSavedRoutesAsync(Guid visitor, int? museumId);
    Task<SavedRouteDto> GetSavedRouteAsync(Guid visitor, int savedRouteId);
}
public interface IRecognitionService
{
    Task<ScanResult> RecordQrAsync(ScanQrRequest request, Guid user);
    Task<IReadOnlyList<RecognitionHistory>> GetHistoryAsync(Guid user, int page, int pageSize);
}
public interface IVisitorNavigationService
{
    Task<VisitorLocation> LocateFromRecognitionAsync(Guid user, int visitId);
    Task<VisitorRoute> RouteToArtifactAsync(Guid user, int visitId, int mapId, int? artifactId, bool accessibleOnly);
}
public interface IVisitProgressService
{
    Task<VisitResponseDto> StartVisitAsync(Guid user, StartVisitRequestDto request);
    Task RecordQrVisitAsync(Guid user, int visitId, int artifactId, DateTime at);
    Task<VisitProgress> GetProgressAsync(Guid user, int visitId);
    Task<VisitResponseDto> CompleteTourAsync(Guid user, int visitId);
}
public interface IQuestionBankService
{
    Task<Question> CreateAsync(int artifactId, QuestionRequest request, Guid user, bool admin);
    Task<Question> UpdateAsync(int id, QuestionRequest request, Guid user, bool admin);
    Task<Question> ReviewQuestionAsync(int id, ReviewQuestionRequest request, Guid user, bool admin);
    Task<IReadOnlyList<Question>> ListAsync(int artifactId, Guid user, bool admin, int page, int pageSize);
    Task<Quiz> CreateQuizAsync(int artifactId, QuizRequest request, Guid user, bool admin);
    Task<Quiz> PublishQuizAsync(int id, Guid user, bool admin);
    Task<IReadOnlyList<Quiz>> ListQuizzesAsync(int artifactId, Guid user, bool admin);
}
public interface IDailyQuizService
{
    Task UnlockAsync(RecognitionHistory recognition);
    Task<IReadOnlyList<DailyQuizEntry>> GetTodayAsync(Guid user);
}
public interface IQuizAttemptService
{
    Task<AttemptResult> StartAsync(StartAttemptRequest request, Guid user);
    Task<AttemptResult> AnswerAsync(long id, AnswerRequest request, Guid user);
    Task<AttemptResult> ResetAsync(long id, Guid user);
    Task<AttemptResult> GetResultsAsync(long id, Guid user);
    Task<IReadOnlyList<QuizAttempt>> ListAsync(Guid user, int page, int pageSize);
}
public interface IGamificationService
{
    Task AwardScanPointsAsync(Guid user, int artifactId);
    Task<int> AwardQuizPointsAsync(Guid user, int questionId);
    Task EvaluateBadgesAsync(Guid user);
    Task<long> GetBalanceAsync(Guid user);
    Task<IReadOnlyList<PointTransaction>> GetTransactionsAsync(Guid user, int page, int pageSize);
    Task<IReadOnlyList<UserBadge>> GetBadgesAsync(Guid user);
    Task<Badge> CreateBadgeAsync(BadgeRequest request);
    Task<IReadOnlyList<Badge>> ListBadgesAsync();
}
