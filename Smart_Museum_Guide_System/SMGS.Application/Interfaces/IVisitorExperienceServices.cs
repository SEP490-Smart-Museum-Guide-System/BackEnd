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
