using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Visit;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

/// <summary>
/// Quản lý lượt tham quan ở mức tối thiểu (bắt đầu / hoàn thành) để phục vụ luồng đánh giá.
/// Logic tiến độ tour chi tiết sẽ được bổ sung khi module Tour hoàn thành.
/// </summary>
public class VisitService(IVisitRepository visitRepo, IMuseumRepository museumRepo) : IVisitService
{
    public async Task<ResponseDto<VisitResponseDto>> StartAsync(Guid visitorId, StartVisitRequestDto dto)
    {
        var museum = await museumRepo.GetByIdAsync(dto.MuseumId);
        if (museum is null || !museum.IsActive)
            return ResponseDto<VisitResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);

        var active = await visitRepo.GetActiveVisitAsync(visitorId, dto.MuseumId);
        if (active is not null)
            return ResponseDto<VisitResponseDto>.Fail("Bạn đang có một lượt tham quan chưa kết thúc tại bảo tàng này.", 409);

        var visit = new Visit
        {
            VisitorId = visitorId,
            MuseumId = dto.MuseumId,
            TourId = dto.TourId,
            Status = VisitStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        var created = await visitRepo.CreateAsync(visit);
        return ResponseDto<VisitResponseDto>.Success(MapToDto(created), "Bắt đầu lượt tham quan.", 201);
    }

    public async Task<ResponseDto<VisitResponseDto>> CompleteAsync(int visitId, Guid visitorId)
    {
        var visit = await visitRepo.GetByIdAsync(visitId);
        if (visit is null || visit.VisitorId != visitorId)
            return ResponseDto<VisitResponseDto>.Fail("Không tìm thấy lượt tham quan.", 404);

        if (visit.Status != VisitStatus.InProgress)
            return ResponseDto<VisitResponseDto>.Fail("Lượt tham quan không ở trạng thái đang diễn ra.", 400);

        visit.Status = VisitStatus.Completed;
        visit.CompletedAt = DateTime.UtcNow;
        await visitRepo.UpdateAsync(visit);

        return ResponseDto<VisitResponseDto>.Success(MapToDto(visit), "Đã hoàn thành lượt tham quan.");
    }

    public async Task<ResponseDto<VisitResponseDto>> GetByIdAsync(int visitId, Guid visitorId)
    {
        var visit = await visitRepo.GetByIdAsync(visitId);
        if (visit is null || visit.VisitorId != visitorId)
            return ResponseDto<VisitResponseDto>.Fail("Không tìm thấy lượt tham quan.", 404);

        return ResponseDto<VisitResponseDto>.Success(MapToDto(visit));
    }

    public async Task<ResponseDto<PaginatedResponseDto<VisitResponseDto>>> GetMineAsync(
        Guid visitorId, string? status, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var (items, total) = await visitRepo.GetPagedAsync(visitorId, null, status, page, pageSize);

        return ResponseDto<PaginatedResponseDto<VisitResponseDto>>.Success(new()
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    private static VisitResponseDto MapToDto(Visit v) => new()
    {
        Id = v.Id,
        VisitorId = v.VisitorId,
        MuseumId = v.MuseumId,
        TourId = v.TourId,
        Status = v.Status,
        StartedAt = v.StartedAt,
        CompletedAt = v.CompletedAt,
        HasFeedback = v.Feedback is not null
    };
}

internal static class Paging
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        return (page, pageSize);
    }
}
