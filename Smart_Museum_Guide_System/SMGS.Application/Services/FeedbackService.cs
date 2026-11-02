using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Feedback.Request;
using SMGS.Application.DTOs.Feedback.Response;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class FeedbackService(IFeedbackRepository feedbackRepo, IVisitRepository visitRepo) : IFeedbackService
{
    /// <summary>
    /// Khoảng thời gian khách được phép sửa feedback kể từ lúc tạo.
    /// TODO: chốt giá trị chính thức với chủ dự án (đang để tạm 24 giờ).
    /// </summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    // ─────────────────────────── Visitor ───────────────────────────

    public async Task<ResponseDto<FeedbackResponseDto>> CreateAsync(Guid visitorId, CreateFeedbackRequestDto dto)
    {
        var visit = await visitRepo.GetByIdAsync(dto.VisitId);
        if (visit is null || visit.VisitorId != visitorId)
            return ResponseDto<FeedbackResponseDto>.Fail("Không tìm thấy lượt tham quan.", 404);

        // [Q23] Chỉ được đánh giá sau khi hoàn thành tour
        if (visit.Status != VisitStatus.Completed)
            return ResponseDto<FeedbackResponseDto>.Fail("Chỉ được đánh giá sau khi hoàn thành lượt tham quan.", 400);

        if (await feedbackRepo.GetByVisitIdAsync(visit.Id) is not null)
            return ResponseDto<FeedbackResponseDto>.Fail("Lượt tham quan này đã được đánh giá.", 409);

        var feedback = new VisitFeedback
        {
            VisitId = visit.Id,
            OverallRating = dto.OverallRating,
            ContentRating = dto.ContentRating,
            TourRating = dto.TourRating,
            ChatbotRating = dto.ChatbotRating,
            Comment = NormalizeComment(dto.Comment),
            ModerationStatus = FeedbackModerationStatus.Visible,
            CreatedAt = DateTime.UtcNow
        };

        var created = await feedbackRepo.CreateAsync(feedback);
        created.Visit = visit;
        return ResponseDto<FeedbackResponseDto>.Success(MapToDto(created, forVisitor: true), "Gửi đánh giá thành công.", 201);
    }

    public async Task<ResponseDto<FeedbackResponseDto>> UpdateAsync(int feedbackId, Guid visitorId, UpdateFeedbackRequestDto dto)
    {
        var feedback = await feedbackRepo.GetByIdAsync(feedbackId);
        if (feedback is null || feedback.Visit?.VisitorId != visitorId)
            return ResponseDto<FeedbackResponseDto>.Fail("Không tìm thấy đánh giá.", 404);

        if (!CanEdit(feedback))
            return ResponseDto<FeedbackResponseDto>.Fail(
                $"Đã quá thời hạn chỉnh sửa ({EditWindow.TotalHours:0} giờ) hoặc đánh giá đã bị ẩn.", 400);

        feedback.OverallRating = dto.OverallRating;
        feedback.ContentRating = dto.ContentRating;
        feedback.TourRating = dto.TourRating;
        feedback.ChatbotRating = dto.ChatbotRating;
        feedback.Comment = NormalizeComment(dto.Comment);
        feedback.UpdatedAt = DateTime.UtcNow;

        await feedbackRepo.UpdateAsync(feedback);
        return ResponseDto<FeedbackResponseDto>.Success(MapToDto(feedback, forVisitor: true), "Cập nhật đánh giá thành công.");
    }

    public async Task<ResponseDto<FeedbackResponseDto>> GetByVisitAsync(int visitId, Guid visitorId)
    {
        var feedback = await feedbackRepo.GetByVisitIdAsync(visitId);
        if (feedback is null || feedback.Visit?.VisitorId != visitorId)
            return ResponseDto<FeedbackResponseDto>.Fail("Không tìm thấy đánh giá.", 404);

        return ResponseDto<FeedbackResponseDto>.Success(MapToDto(feedback, forVisitor: true));
    }

    // ─────────────────────────── Museum Staff ───────────────────────────

    public async Task<ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>> GetByMuseumAsync(
        int museumId, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        // Museum Staff chỉ thấy feedback đang hiển thị (đã qua kiểm duyệt)
        var (items, total) = await feedbackRepo.GetPagedByMuseumAsync(
            museumId, FeedbackModerationStatus.Visible, page, pageSize);

        return ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>.Success(new()
        {
            Items = items.Select(f => MapToDto(f, forVisitor: false)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ResponseDto<FeedbackResponseDto>> RespondAsync(int feedbackId, Guid staffId, StaffResponseRequestDto dto)
    {
        var feedback = await feedbackRepo.GetByIdAsync(feedbackId);
        if (feedback is null)
            return ResponseDto<FeedbackResponseDto>.Fail("Không tìm thấy đánh giá.", 404);

        if (feedback.ModerationStatus == FeedbackModerationStatus.Hidden)
            return ResponseDto<FeedbackResponseDto>.Fail("Không thể phản hồi đánh giá đã bị ẩn.", 400);

        var content = dto.Content.Trim();
        if (content.Length == 0)
            return ResponseDto<FeedbackResponseDto>.Fail("Nội dung phản hồi không được để trống.", 400);

        var existing = await feedbackRepo.GetResponseByFeedbackIdAsync(feedbackId);
        if (existing is null)
        {
            feedback.StaffResponse = await feedbackRepo.CreateResponseAsync(new StaffResponse
            {
                FeedbackId = feedbackId,
                RespondedBy = staffId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Content = content;
            existing.RespondedBy = staffId;
            existing.UpdatedAt = DateTime.UtcNow;
            await feedbackRepo.UpdateResponseAsync(existing);
            feedback.StaffResponse = existing;
        }

        return ResponseDto<FeedbackResponseDto>.Success(MapToDto(feedback, forVisitor: false), "Đã lưu phản hồi.");
    }

    // ─────────────────────────── System Staff / Admin ───────────────────────────

    public async Task<ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>> GetAllAsync(
        string? moderationStatus, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var (items, total) = await feedbackRepo.GetPagedAllAsync(moderationStatus, page, pageSize);

        return ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>.Success(new()
        {
            Items = items.Select(f => MapToDto(f, forVisitor: false)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ResponseDto<FeedbackResponseDto>> ModerateAsync(int feedbackId, ModerateFeedbackRequestDto dto)
    {
        var feedback = await feedbackRepo.GetByIdAsync(feedbackId);
        if (feedback is null)
            return ResponseDto<FeedbackResponseDto>.Fail("Không tìm thấy đánh giá.", 404);

        feedback.ModerationStatus = dto.ModerationStatus;
        feedback.ModerationNote = string.IsNullOrWhiteSpace(dto.ModerationNote) ? null : dto.ModerationNote.Trim();
        await feedbackRepo.UpdateAsync(feedback);

        return ResponseDto<FeedbackResponseDto>.Success(MapToDto(feedback, forVisitor: false), "Đã cập nhật trạng thái kiểm duyệt.");
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static bool CanEdit(VisitFeedback f) =>
        f.ModerationStatus == FeedbackModerationStatus.Visible
        && DateTime.UtcNow - f.CreatedAt <= EditWindow;

    private static string? NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private static FeedbackResponseDto MapToDto(VisitFeedback f, bool forVisitor) => new()
    {
        Id = f.Id,
        VisitId = f.VisitId,
        MuseumId = f.Visit?.MuseumId ?? 0,
        TourId = f.Visit?.TourId,
        VisitorId = f.Visit?.VisitorId ?? Guid.Empty,
        OverallRating = f.OverallRating,
        ContentRating = f.ContentRating,
        TourRating = f.TourRating,
        ChatbotRating = f.ChatbotRating,
        Comment = f.Comment,
        ModerationStatus = f.ModerationStatus,
        // Ghi chú kiểm duyệt là nội bộ, không trả về cho khách
        ModerationNote = forVisitor ? null : f.ModerationNote,
        CanEdit = CanEdit(f),
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
        StaffResponse = f.StaffResponse is null ? null : new StaffResponseDto
        {
            Id = f.StaffResponse.Id,
            RespondedBy = f.StaffResponse.RespondedBy,
            Content = f.StaffResponse.Content,
            CreatedAt = f.StaffResponse.CreatedAt,
            UpdatedAt = f.StaffResponse.UpdatedAt
        }
    };
}
