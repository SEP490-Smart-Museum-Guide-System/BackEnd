using SMGS.Application.DTOs.Artifact.Request;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Common;

namespace SMGS.Application.Interfaces;

public interface IArtifactService
{
    Task<ResponseDto<PaginatedResponseDto<ArtifactResponseDto>>> GetAllAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize);

    Task<ResponseDto<ArtifactResponseDto>> GetByIdAsync(int id);
    Task<ResponseDto<ArtifactResponseDto>> CreateAsync(CreateArtifactRequestDto dto);
    Task<ResponseDto<ArtifactResponseDto>> UpdateAsync(int id, UpdateArtifactRequestDto dto);
    Task<ResponseDto<object>> DeleteAsync(int id);
}
