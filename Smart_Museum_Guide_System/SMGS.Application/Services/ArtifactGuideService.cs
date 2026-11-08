using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.Application.Services;

public class ArtifactGuideService(IArtifactContentService contents, IArtifactGuideModel model) : IArtifactGuideService
{
    public const string InsufficientEvidence = "Nội dung đã xuất bản của hiện vật chưa có thông tin để trả lời câu hỏi này. Bạn có thể hỏi thêm nhân viên bảo tàng.";

    public async Task<ResponseDto<GuideAnswer>> AskAsync(int artifactId, Guid userId, GuideMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return ResponseDto<GuideAnswer>.Fail("Vui lòng đăng nhập để hỏi trợ lý.", 401);
        if (artifactId <= 0 || request is null || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 1000
            || request.History is null || request.History.Length > 8
            || request.History.Any(t => t is null || t.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(t.Text) || t.Text.Length > 4000)
            || request.History.Select((t, i) => t.Role != (i % 2 == 0 ? "user" : "assistant")).Any(invalid => invalid)
            || request.History.Length % 2 != 0)
            return ResponseDto<GuideAnswer>.Fail("Câu hỏi hoặc lịch sử hội thoại không hợp lệ.");

        cancellationToken.ThrowIfCancellationRequested();
        // The same service checks free/premium access for the current authenticated visitor on EVERY turn.
        // Never accept content, language, museum IDs or claimed permissions supplied by the client.
        var published = await contents.GetPublishedAsync(artifactId, "vi", userId);
        if (published.StatusCode != 200)
            return ResponseDto<GuideAnswer>.Fail(published.Message, published.StatusCode);
        var c = published.Data;
        if (c is null || c.ArtifactId != artifactId || c.Status != ContentStatuses.Published || !c.IsCurrent || c.LanguageCode != "vi")
            return ResponseDto<GuideAnswer>.Fail("Hiện vật chưa có nội dung tiếng Việt đã xuất bản.", 404);
        if (string.IsNullOrWhiteSpace(c.Description) && string.IsNullOrWhiteSpace(c.NarrationText))
            return ResponseDto<GuideAnswer>.Fail("Hiện vật chưa có nội dung để hỏi đáp.", 404);
        // Fail explicitly instead of silently cutting facts out of an oversized source.
        if ((long)c.ArtifactName.Length + c.Title.Length + c.Description.Length + (c.NarrationText?.Length ?? 0) > 24000)
            return ResponseDto<GuideAnswer>.Fail("Nội dung hiện vật quá dài để hỏi đáp lúc này. Vui lòng xem bản thuyết minh.", 422);
        try
        {
            var answer = await model.AnswerAsync(new(artifactId, c.ArtifactName, c.Title, c.Description, c.NarrationText ?? ""),
                request.Question.Trim(), request.History, cancellationToken);
            if (answer.Supported && (string.IsNullOrWhiteSpace(answer.Answer) || answer.Answer.Length > 4000))
                return ResponseDto<GuideAnswer>.Fail("Trợ lý trả về nội dung không hợp lệ. Vui lòng thử lại.", 502);
            // Source identity is built from authorized DB content, never a model-invented link or ID.
            return ResponseDto<GuideAnswer>.Success(new(artifactId,
                answer.Supported ? answer.Answer.Trim() : InsufficientEvidence, answer.Supported,
                answer.Supported ? [new(c.Id, c.VersionNo, c.Title, c.LanguageCode)] : []));
        }
        catch (GuideException e) { return ResponseDto<GuideAnswer>.Fail(e.Message, e.StatusCode); }
    }
}
