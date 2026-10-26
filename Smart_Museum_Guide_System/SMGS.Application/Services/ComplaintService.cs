using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Complaint.Request;
using SMGS.Application.DTOs.Complaint.Response;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class ComplaintService(IComplaintRepository complaintRepo) : IComplaintService
{
    // ─────────────────────────── Visitor ───────────────────────────

    public async Task<ResponseDto<ComplaintResponseDto>> CreateAsync(Guid visitorId, CreateComplaintRequestDto dto)
    {
        var subject = dto.Subject.Trim();
        var body = dto.Body.Trim();
        if (subject.Length == 0 || body.Length == 0)
            return ResponseDto<ComplaintResponseDto>.Fail("Tiêu đề và nội dung không được để trống.", 400);

        var created = await complaintRepo.CreateAsync(new Complaint
        {
            VisitorId = visitorId,
            Subject = subject,
            Body = body,
            Status = ComplaintStatus.New,
            CreatedAt = DateTime.UtcNow
        });

        return ResponseDto<ComplaintResponseDto>.Success(MapToDto(created), "Đã gửi yêu cầu hỗ trợ.", 201);
    }

    public async Task<ResponseDto<PaginatedResponseDto<ComplaintResponseDto>>> GetMineAsync(
        Guid visitorId, string? status, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var (items, total) = await complaintRepo.GetPagedAsync(visitorId, status, null, page, pageSize);
        return Paged(items, total, page, pageSize);
    }

    public async Task<ResponseDto<ComplaintResponseDto>> GetMineByIdAsync(int id, Guid visitorId)
    {
        var complaint = await complaintRepo.GetByIdAsync(id);
        if (complaint is null || complaint.VisitorId != visitorId)
            return ResponseDto<ComplaintResponseDto>.Fail("Không tìm thấy yêu cầu hỗ trợ.", 404);

        return ResponseDto<ComplaintResponseDto>.Success(MapToDto(complaint));
    }

    // ─────────────────────────── System Staff ───────────────────────────

    public async Task<ResponseDto<PaginatedResponseDto<ComplaintResponseDto>>> GetAllAsync(
        string? status, Guid? assignedTo, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var (items, total) = await complaintRepo.GetPagedAsync(null, status, assignedTo, page, pageSize);
        return Paged(items, total, page, pageSize);
    }

    public async Task<ResponseDto<ComplaintResponseDto>> GetByIdAsync(int id)
    {
        var complaint = await complaintRepo.GetByIdAsync(id);
        return complaint is null
            ? ResponseDto<ComplaintResponseDto>.Fail("Không tìm thấy yêu cầu hỗ trợ.", 404)
            : ResponseDto<ComplaintResponseDto>.Success(MapToDto(complaint));
    }

    public async Task<ResponseDto<ComplaintResponseDto>> AssignToMeAsync(int id, Guid staffId)
    {
        var complaint = await complaintRepo.GetByIdAsync(id);
        if (complaint is null)
            return ResponseDto<ComplaintResponseDto>.Fail("Không tìm thấy yêu cầu hỗ trợ.", 404);

        if (complaint.Status is ComplaintStatus.Resolved or ComplaintStatus.Closed)
            return ResponseDto<ComplaintResponseDto>.Fail("Yêu cầu đã được xử lý xong.", 400);

        complaint.AssignedTo = staffId;
        if (complaint.Status == ComplaintStatus.New)
            complaint.Status = ComplaintStatus.InProgress;
        complaint.UpdatedAt = DateTime.UtcNow;

        await complaintRepo.UpdateAsync(complaint);
        return ResponseDto<ComplaintResponseDto>.Success(MapToDto(complaint), "Đã nhận xử lý yêu cầu.");
    }

    public async Task<ResponseDto<ComplaintResponseDto>> UpdateStatusAsync(
        int id, Guid staffId, UpdateComplaintStatusRequestDto dto)
    {
        var complaint = await complaintRepo.GetByIdAsync(id);
        if (complaint is null)
            return ResponseDto<ComplaintResponseDto>.Fail("Không tìm thấy yêu cầu hỗ trợ.", 404);

        if (!IsValidTransition(complaint.Status, dto.Status))
            return ResponseDto<ComplaintResponseDto>.Fail(
                $"Không thể chuyển trạng thái từ {complaint.Status} sang {dto.Status}.", 400);

        complaint.Status = dto.Status;
        complaint.AssignedTo ??= staffId;
        if (!string.IsNullOrWhiteSpace(dto.ResolutionNote))
            complaint.ResolutionNote = dto.ResolutionNote.Trim();
        complaint.UpdatedAt = DateTime.UtcNow;
        if (dto.Status is ComplaintStatus.Resolved or ComplaintStatus.Closed)
            complaint.ResolvedAt ??= DateTime.UtcNow;

        await complaintRepo.UpdateAsync(complaint);
        return ResponseDto<ComplaintResponseDto>.Success(MapToDto(complaint), "Đã cập nhật trạng thái.");
    }

    // ─────────────────────────── Helpers ───────────────────────────

    /// <summary>
    /// New → InProgress | Closed
    /// InProgress → Resolved | Closed
    /// Resolved → Closed | InProgress (mở lại)
    /// Closed → (kết thúc)
    /// </summary>
    private static bool IsValidTransition(string from, string to) => (from, to) switch
    {
        (ComplaintStatus.New, ComplaintStatus.InProgress) => true,
        (ComplaintStatus.New, ComplaintStatus.Closed) => true,
        (ComplaintStatus.InProgress, ComplaintStatus.Resolved) => true,
        (ComplaintStatus.InProgress, ComplaintStatus.Closed) => true,
        (ComplaintStatus.Resolved, ComplaintStatus.Closed) => true,
        (ComplaintStatus.Resolved, ComplaintStatus.InProgress) => true,
        _ => false
    };

    private static ResponseDto<PaginatedResponseDto<ComplaintResponseDto>> Paged(
        IList<Complaint> items, int total, int page, int pageSize) =>
        ResponseDto<PaginatedResponseDto<ComplaintResponseDto>>.Success(new()
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });

    private static ComplaintResponseDto MapToDto(Complaint c) => new()
    {
        Id = c.Id,
        VisitorId = c.VisitorId,
        Subject = c.Subject,
        Body = c.Body,
        Status = c.Status,
        AssignedTo = c.AssignedTo,
        ResolutionNote = c.ResolutionNote,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        ResolvedAt = c.ResolvedAt
    };
}
