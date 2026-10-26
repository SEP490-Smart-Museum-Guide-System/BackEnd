using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Complaint.Request;
using SMGS.Application.DTOs.Complaint.Response;
using SMGS.Application.DTOs.Feedback.Request;
using SMGS.Application.DTOs.Feedback.Response;
using SMGS.Application.DTOs.Visit;

namespace SMGS.Application.Interfaces;

public interface IVisitService
{
    Task<ResponseDto<VisitResponseDto>> StartAsync(Guid visitorId, StartVisitRequestDto dto);
    Task<ResponseDto<VisitResponseDto>> CompleteAsync(int visitId, Guid visitorId);
    Task<ResponseDto<VisitResponseDto>> GetByIdAsync(int visitId, Guid visitorId);
    Task<ResponseDto<PaginatedResponseDto<VisitResponseDto>>> GetMineAsync(
        Guid visitorId, string? status, int page, int pageSize);
}

public interface IFeedbackService
{
    // Visitor
    Task<ResponseDto<FeedbackResponseDto>> CreateAsync(Guid visitorId, CreateFeedbackRequestDto dto);
    Task<ResponseDto<FeedbackResponseDto>> UpdateAsync(int feedbackId, Guid visitorId, UpdateFeedbackRequestDto dto);
    Task<ResponseDto<FeedbackResponseDto>> GetByVisitAsync(int visitId, Guid visitorId);

    // Museum Staff
    Task<ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>> GetByMuseumAsync(
        int museumId, int page, int pageSize);
    Task<ResponseDto<FeedbackResponseDto>> RespondAsync(int feedbackId, Guid staffId, StaffResponseRequestDto dto);

    // System Staff / Admin
    Task<ResponseDto<PaginatedResponseDto<FeedbackResponseDto>>> GetAllAsync(
        string? moderationStatus, int page, int pageSize);
    Task<ResponseDto<FeedbackResponseDto>> ModerateAsync(int feedbackId, ModerateFeedbackRequestDto dto);
}

public interface IComplaintService
{
    // Visitor
    Task<ResponseDto<ComplaintResponseDto>> CreateAsync(Guid visitorId, CreateComplaintRequestDto dto);
    Task<ResponseDto<PaginatedResponseDto<ComplaintResponseDto>>> GetMineAsync(
        Guid visitorId, string? status, int page, int pageSize);
    Task<ResponseDto<ComplaintResponseDto>> GetMineByIdAsync(int id, Guid visitorId);

    // System Staff
    Task<ResponseDto<PaginatedResponseDto<ComplaintResponseDto>>> GetAllAsync(
        string? status, Guid? assignedTo, int page, int pageSize);
    Task<ResponseDto<ComplaintResponseDto>> GetByIdAsync(int id);
    Task<ResponseDto<ComplaintResponseDto>> AssignToMeAsync(int id, Guid staffId);
    Task<ResponseDto<ComplaintResponseDto>> UpdateStatusAsync(int id, Guid staffId, UpdateComplaintStatusRequestDto dto);
}
