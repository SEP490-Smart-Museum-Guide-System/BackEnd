using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.DTOs.ArtifactContent.Response;
using SMGS.Application.DTOs.Common;
namespace SMGS.Application.Interfaces;
public interface IArtifactContentService
{
    Task<ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>> GetAllAsync(int? museumId, int? artifactId, string? status, int page, int pageSize);
    Task<ResponseDto<ArtifactContentResponseDto>> GetByIdAsync(int id);
    Task<ResponseDto<ArtifactContentResponseDto>> GetPublishedAsync(int artifactId, string languageCode);
    Task<ResponseDto<ArtifactContentResponseDto>> SubmitAsync(int artifactId, SubmitArtifactContentRequestDto dto, Guid userId);
    Task<ResponseDto<ArtifactContentResponseDto>> ReviewAsync(int id, ReviewArtifactContentRequestDto dto, Guid userId, bool approve);
}
