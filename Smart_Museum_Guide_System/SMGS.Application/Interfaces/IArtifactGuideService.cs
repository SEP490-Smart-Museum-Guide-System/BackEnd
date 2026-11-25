using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;

namespace SMGS.Application.Interfaces;

public interface IArtifactGuideService
{
    Task<ResponseDto<GuideAnswer>> AskAsync(int artifactId, Guid userId, GuideMessageRequest request,
        CancellationToken cancellationToken = default);
}

public interface IArtifactGuideModel
{
    Task<GuideModelAnswer> AnswerAsync(GuideContext context, string question, IReadOnlyList<GuideTurn> history,
        CancellationToken cancellationToken = default);
}

public interface IGuideHistoryStore
{
    Task RecordExchangeAsync(
        Guid userId,
        int artifactId,
        int museumId,
        string question,
        string answer,
        bool supported,
        GuideSource? source,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuideTurn>> GetRecentTurnsAsync(
        Guid userId,
        int artifactId,
        int maxExchanges = 4,
        CancellationToken cancellationToken = default);
}

public interface IGuideHistoryService : IGuideHistoryStore
{
    Task<ResponseDto<IReadOnlyList<GuideChatMessageDto>>> GetArtifactHistoryAsync(
        int artifactId,
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<int>> ClearArtifactHistoryAsync(
        int artifactId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>> GetMyConversationsAsync(
        Guid userId,
        int? museumId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>> GetMyMessagesFeedAsync(
        Guid userId,
        int? museumId = null,
        int? artifactId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>> GetLogsAsync(
        int? museumId,
        int? artifactId,
        bool? supported,
        int page,
        int pageSize,
        Guid callerId,
        bool isAdminOrSystemStaff,
        CancellationToken cancellationToken = default);
}
