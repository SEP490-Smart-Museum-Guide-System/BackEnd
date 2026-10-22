using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Museum.Request;
using SMGS.Application.DTOs.Museum.Response;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.Application.Services;

public class MuseumService(IMuseumRepository museumRepo) : IMuseumService
{
    public async Task<ResponseDto<PaginatedResponseDto<MuseumResponseDto>>> DiscoverAsync(
        DiscoverMuseumsRequestDto request, bool canViewInactive, CancellationToken cancellationToken = default)
    {
        if (!CityCatalog.IsValid(request.CityCode)
            || request.Latitude.HasValue != request.Longitude.HasValue
            || (request.Latitude.HasValue && (!double.IsFinite(request.Latitude.Value) || Math.Abs(request.Latitude.Value) > 90))
            || (request.Longitude.HasValue && (!double.IsFinite(request.Longitude.Value) || Math.Abs(request.Longitude.Value) > 180))
            || request.SortBy is not (null or "newest" or "distance")
            || (request.SortBy == "distance" && !request.Latitude.HasValue)
            || request.Page < 1 || request.Page > 1000000 || request.PageSize < 1 || request.PageSize > 50)
            return ResponseDto<PaginatedResponseDto<MuseumResponseDto>>.Fail("Bộ lọc hoặc tọa độ không hợp lệ.");

        var (items, total) = await museumRepo.DiscoverAsync(new MuseumDiscoveryQuery
        {
            Keyword = request.Keyword ?? request.Search, CityCode = request.CityCode,
            IsActive = canViewInactive ? request.IsActive : true,
            Latitude = request.Latitude, Longitude = request.Longitude, SortBy = request.SortBy,
            Page = request.Page, PageSize = request.PageSize
        }, cancellationToken);
        return ResponseDto<PaginatedResponseDto<MuseumResponseDto>>.Success(new()
        {
            Items = items.Select(item => { var dto = MapToDto(item.Museum); dto.DistanceKm = item.DistanceKm; return dto; }).ToList(),
            Page = request.Page, PageSize = request.PageSize, TotalCount = total
        });
    }

    public async Task<ResponseDto<PaginatedResponseDto<MuseumResponseDto>>> GetAllAsync(
        string? keyword, bool? isActive, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var (items, totalCount) = await museumRepo.GetPagedAsync(keyword, isActive, page, pageSize);

        var result = new PaginatedResponseDto<MuseumResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return ResponseDto<PaginatedResponseDto<MuseumResponseDto>>.Success(result);
    }

    public async Task<ResponseDto<MuseumResponseDto>> GetByIdAsync(int id)
    {
        var museum = await museumRepo.GetByIdAsync(id);
        if (museum is null)
            return ResponseDto<MuseumResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);

        return ResponseDto<MuseumResponseDto>.Success(MapToDto(museum));
    }

    public async Task<ResponseDto<MuseumResponseDto>> CreateAsync(CreateMuseumRequestDto dto)
    {
        if (!CityCatalog.IsValid(dto.CityCode) || dto.Latitude.HasValue != dto.Longitude.HasValue)
            return ResponseDto<MuseumResponseDto>.Fail("Mã tỉnh/thành hoặc cặp tọa độ không hợp lệ.");
        var name = dto.Name.Trim();

        if (await museumRepo.ExistsByNameAsync(name))
            return ResponseDto<MuseumResponseDto>.Fail("Tên bảo tàng đã tồn tại.", 400);

        var museum = new Museum
        {
            Name = name,
            Description = dto.Description?.Trim(),
            Address = dto.Address?.Trim(),
            CityCode = dto.CityCode,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            OpeningHours = dto.OpeningHours?.Trim(),
            CoverImageUrl = dto.CoverImageUrl?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await museumRepo.CreateAsync(museum);
        return ResponseDto<MuseumResponseDto>.Success(MapToDto(created), "Tạo bảo tàng thành công.", 201);
    }

    public async Task<ResponseDto<MuseumResponseDto>> UpdateAsync(int id, UpdateMuseumRequestDto dto)
    {
        if (!CityCatalog.IsValid(dto.CityCode) || dto.Latitude.HasValue != dto.Longitude.HasValue)
            return ResponseDto<MuseumResponseDto>.Fail("Mã tỉnh/thành hoặc cặp tọa độ không hợp lệ.");
        var museum = await museumRepo.GetByIdAsync(id);
        if (museum is null)
            return ResponseDto<MuseumResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);

        var name = dto.Name.Trim();

        if (await museumRepo.ExistsByNameAsync(name, excludeId: id))
            return ResponseDto<MuseumResponseDto>.Fail("Tên bảo tàng đã tồn tại.", 400);

        museum.Name = name;
        museum.Description = dto.Description?.Trim();
        museum.Address = dto.Address?.Trim();
        museum.CityCode = dto.CityCode ?? museum.CityCode;
        museum.Latitude = dto.Latitude;
        museum.Longitude = dto.Longitude;
        museum.OpeningHours = dto.OpeningHours?.Trim();
        museum.CoverImageUrl = dto.CoverImageUrl?.Trim();

        await museumRepo.UpdateAsync(museum);
        return ResponseDto<MuseumResponseDto>.Success(MapToDto(museum), "Cập nhật bảo tàng thành công.");
    }

    public async Task<ResponseDto<object>> DeleteAsync(int id)
    {
        var museum = await museumRepo.GetByIdAsync(id);
        if (museum is null)
            return ResponseDto<object>.Fail("Không tìm thấy bảo tàng.", 404);

        museum.IsActive = false;
        await museumRepo.UpdateAsync(museum);
        return ResponseDto<object>.Success(null!, "Đã vô hiệu hóa bảo tàng thành công.");
    }

    private static MuseumResponseDto MapToDto(Museum museum) => new()
    {
        Id = museum.Id,
        Name = museum.Name,
        Description = museum.Description,
        Address = museum.Address,
        CityCode = museum.CityCode,
        Latitude = museum.Latitude,
        Longitude = museum.Longitude,
        OpeningHours = museum.OpeningHours,
        CoverImageUrl = museum.CoverImageUrl,
        IsActive = museum.IsActive,
        CreatedAt = museum.CreatedAt
    };
}
