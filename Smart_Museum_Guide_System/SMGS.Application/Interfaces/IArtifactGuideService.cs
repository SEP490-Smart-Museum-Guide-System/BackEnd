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
