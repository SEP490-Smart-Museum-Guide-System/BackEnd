using SMGS.Application.DTOs.Artifact.Request;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class ArtifactService(IArtifactRepository artifactRepo) : IArtifactService
{
    public async Task<ResponseDto<PaginatedResponseDto<ArtifactResponseDto>>> GetAllAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await artifactRepo.GetPagedAsync(museumId, galleryId, keyword, isActive, page, pageSize);

        var result = new PaginatedResponseDto<ArtifactResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return ResponseDto<PaginatedResponseDto<ArtifactResponseDto>>.Success(result);
    }

    public async Task<ResponseDto<ArtifactResponseDto>> GetByIdAsync(int id)
    {
        var artifact = await artifactRepo.GetByIdAsync(id);
        if (artifact is null)
            return ResponseDto<ArtifactResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        return ResponseDto<ArtifactResponseDto>.Success(MapToDto(artifact));
    }

    public async Task<ResponseDto<ArtifactResponseDto>> CreateAsync(CreateArtifactRequestDto dto)
    {
        // Kiểm tra mã nội bộ trùng trong cùng bảo tàng
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();
            if (await artifactRepo.ExistsByCodeAsync(dto.MuseumId, code))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã hiện vật đã tồn tại trong bảo tàng này.", 400);
        }

        // Kiểm tra QR code trùng (unique toàn hệ thống)
        if (!string.IsNullOrWhiteSpace(dto.QrCode))
        {
            var qr = dto.QrCode.Trim();
            if (await artifactRepo.ExistsByQrCodeAsync(qr))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã QR đã được sử dụng bởi hiện vật khác.", 400);
        }

        var artifact = new Artifact
        {
            MuseumId = dto.MuseumId,
            GalleryId = dto.GalleryId,
            Code = dto.Code?.Trim(),
            Name = dto.Name.Trim(),
            QrCode = dto.QrCode?.Trim(),
            MapNodeId = dto.MapNodeId,
            MapX = dto.MapX,
            MapY = dto.MapY,
            MapIcon = dto.MapIcon?.Trim(),
            AccessLevel = dto.AccessLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await artifactRepo.CreateAsync(artifact);
        return ResponseDto<ArtifactResponseDto>.Success(MapToDto(created), "Tạo hiện vật thành công.", 201);
    }

    public async Task<ResponseDto<ArtifactResponseDto>> UpdateAsync(int id, UpdateArtifactRequestDto dto)
    {
        var artifact = await artifactRepo.GetByIdAsync(id);
        if (artifact is null)
            return ResponseDto<ArtifactResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        // Kiểm tra mã nội bộ trùng trong cùng bảo tàng (trừ bản ghi hiện tại)
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();
            if (await artifactRepo.ExistsByCodeAsync(artifact.MuseumId, code, excludeId: id))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã hiện vật đã tồn tại trong bảo tàng này.", 400);
        }

        // Kiểm tra QR code trùng toàn hệ thống (trừ bản ghi hiện tại)
        if (!string.IsNullOrWhiteSpace(dto.QrCode))
        {
            var qr = dto.QrCode.Trim();
            if (await artifactRepo.ExistsByQrCodeAsync(qr, excludeId: id))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã QR đã được sử dụng bởi hiện vật khác.", 400);
        }

        artifact.GalleryId = dto.GalleryId;
        artifact.Code = dto.Code?.Trim();
        artifact.Name = dto.Name.Trim();
        artifact.QrCode = dto.QrCode?.Trim();
        artifact.MapNodeId = dto.MapNodeId;
        artifact.MapX = dto.MapX;
        artifact.MapY = dto.MapY;
        artifact.MapIcon = dto.MapIcon?.Trim();
        artifact.AccessLevel = dto.AccessLevel;

        await artifactRepo.UpdateAsync(artifact);
        return ResponseDto<ArtifactResponseDto>.Success(MapToDto(artifact), "Cập nhật hiện vật thành công.");
    }

    public async Task<ResponseDto<object>> DeleteAsync(int id)
    {
        var artifact = await artifactRepo.GetByIdAsync(id);
        if (artifact is null)
            return ResponseDto<object>.Fail("Không tìm thấy hiện vật.", 404);

        artifact.IsActive = false;
        await artifactRepo.UpdateAsync(artifact);
        return ResponseDto<object>.Success(null!, "Đã vô hiệu hóa hiện vật thành công.");
    }

    private static ArtifactResponseDto MapToDto(Artifact artifact) => new()
    {
        Id = artifact.Id,
        MuseumId = artifact.MuseumId,
        GalleryId = artifact.GalleryId,
        Code = artifact.Code,
        Name = artifact.Name,
        QrCode = artifact.QrCode,
        MapNodeId = artifact.MapNodeId,
        MapX = artifact.MapX,
        MapY = artifact.MapY,
        MapIcon = artifact.MapIcon,
        AccessLevel = artifact.AccessLevel,
        IsActive = artifact.IsActive,
        CreatedAt = artifact.CreatedAt
    };
}
