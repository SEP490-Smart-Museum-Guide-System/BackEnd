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

    /// <summary>
    /// Lấy ảnh QR code (PNG bytes) cho hiện vật theo ID.
    /// </summary>
    Task<ResponseDto<byte[]>> GetQrCodeImageAsync(int artifactId, string baseUrl);

    /// <summary>
    /// Quét QR code → trả về thông tin chi tiết hiện vật + nội dung đã xuất bản.
    /// </summary>
    Task<ResponseDto<ArtifactScanResponseDto>> ScanAsync(string qrCode, Guid? userId = null);

    /// <summary>
    /// Sinh/tái sinh QR code cho hiện vật (dành cho admin/staff khi cần regenerate).
    /// </summary>
    Task<ResponseDto<ArtifactResponseDto>> RegenerateQrCodeAsync(int artifactId);
}
