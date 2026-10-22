using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Museum.Request;
using SMGS.Application.DTOs.Museum.Response;

namespace SMGS.Application.Interfaces;

public interface IMuseumService
{
    Task<ResponseDto<PaginatedResponseDto<MuseumResponseDto>>> DiscoverAsync(DiscoverMuseumsRequestDto request, bool canViewInactive, CancellationToken cancellationToken = default);
    Task<ResponseDto<PaginatedResponseDto<MuseumResponseDto>>> GetAllAsync(string? keyword, bool? isActive, int page, int pageSize);
    Task<ResponseDto<MuseumResponseDto>> GetByIdAsync(int id);
    Task<ResponseDto<MuseumResponseDto>> CreateAsync(CreateMuseumRequestDto dto);
    Task<ResponseDto<MuseumResponseDto>> UpdateAsync(int id, UpdateMuseumRequestDto dto);
    Task<ResponseDto<object>> DeleteAsync(int id);
}
