using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.Artifact.Request;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Topic;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class ArtifactService(
    IArtifactRepository artifactRepo,
    IArtifactContentRepository contentRepo,
    IQrCodeService qrCodeService, ICommerceService? commerce = null) : IArtifactService
{
    public async Task<ResponseDto<PaginatedResponseDto<ArtifactResponseDto>>> GetAllAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize, int? topicId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await artifactRepo.GetPagedAsync(museumId, galleryId, topicId, keyword, isActive, page, pageSize);

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
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();
            if (await artifactRepo.ExistsByCodeAsync(dto.MuseumId, code))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã hiện vật đã tồn tại trong bảo tàng này.", 400);
        }

        var qrCode = dto.QrCode?.Trim();
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            qrCode = GenerateUniqueQrCode();
        }
        else
        {
            if (await artifactRepo.ExistsByQrCodeAsync(qrCode))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã QR đã được sử dụng bởi hiện vật khác.", 400);
        }

        var artifact = new Artifact
        {
            MuseumId = dto.MuseumId,
            GalleryId = dto.GalleryId,
            Code = dto.Code?.Trim(),
            Name = dto.Name.Trim(),
            QrCode = qrCode,
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

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();
            if (await artifactRepo.ExistsByCodeAsync(artifact.MuseumId, code, excludeId: id))
                return ResponseDto<ArtifactResponseDto>.Fail("Mã hiện vật đã tồn tại trong bảo tàng này.", 400);
        }

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

    public async Task<ResponseDto<byte[]>> GetQrCodeImageAsync(int artifactId, string baseUrl)
    {
        var artifact = await artifactRepo.GetByIdAsync(artifactId);
        if (artifact is null)
            return ResponseDto<byte[]>.Fail("Không tìm thấy hiện vật.", 404);

        if (string.IsNullOrWhiteSpace(artifact.QrCode))
            return ResponseDto<byte[]>.Fail("Hiện vật chưa có mã QR.", 400);

        var scanUrl = $"{baseUrl.TrimEnd('/')}/api/artifacts/scan/{artifact.QrCode}";
        var pngBytes = qrCodeService.GeneratePng(scanUrl);

        return ResponseDto<byte[]>.Success(pngBytes);
    }

    public async Task<ResponseDto<ArtifactScanResponseDto>> ScanAsync(string qrCode, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return ResponseDto<ArtifactScanResponseDto>.Fail("Mã QR không hợp lệ.", 400);

        var artifact = await artifactRepo.GetByQrCodeAsync(qrCode.Trim());
        if (artifact is null)
            return ResponseDto<ArtifactScanResponseDto>.Fail("Không tìm thấy hiện vật với mã QR này.", 404);

        var allowed = commerce != null ? await commerce.CanReadArtifact(artifact.Id, userId) : artifact.AccessLevel == "free";
        var publishedVersions = allowed ? await contentRepo.GetAllPublishedByArtifactIdAsync(artifact.Id) : [];

        var scanDto = new ArtifactScanResponseDto
        {
            Id = artifact.Id,
            MuseumId = artifact.MuseumId,
            MuseumName = artifact.Museum?.Name ?? string.Empty,
            GalleryId = artifact.GalleryId,
            Code = artifact.Code,
            Name = artifact.Name,
            QrCode = artifact.QrCode,
            AccessLevel = artifact.AccessLevel,
            MapX = artifact.MapX,
            MapY = artifact.MapY,
            Topics = MapTopics(artifact),
            Model3D = allowed && artifact.Model3D is not null ? Map3D(artifact.Model3D) : null,
            Media = allowed ? MapMediaList(artifact) : [],
            Contents = publishedVersions.Select(v => new ArtifactScanContentDto
            {
                LanguageCode = v.Content.LanguageCode,
                Title = v.Title,
                Description = v.Description,
                NarrationText = v.NarrationText,
                AudioUrl = v.AudioUrl
            }).ToList()
        };

        return ResponseDto<ArtifactScanResponseDto>.Success(scanDto);
    }

    public async Task<ResponseDto<ArtifactResponseDto>> RegenerateQrCodeAsync(int artifactId)
    {
        var artifact = await artifactRepo.GetByIdAsync(artifactId);
        if (artifact is null)
            return ResponseDto<ArtifactResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        artifact.QrCode = GenerateUniqueQrCode();
        await artifactRepo.UpdateAsync(artifact);

        return ResponseDto<ArtifactResponseDto>.Success(MapToDto(artifact), "Đã tạo lại mã QR thành công.");
    }

    private static string GenerateUniqueQrCode()
    {
        return $"SMGS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    }

    private static List<ArtifactTopicTagDto> MapTopics(Artifact artifact) =>
        artifact.ArtifactTopics?
            .Where(at => at.Topic is { IsActive: true })
            .OrderBy(at => at.Topic.Name)
            .Select(at => new ArtifactTopicTagDto
            {
                Id = at.Topic.Id,
                Name = at.Topic.Name,
                Description = at.Topic.Description
            }).ToList() ?? [];

    private static Artifact3DModelResponseDto Map3D(Artifact3DModel m) => new()
    {
        Id = m.Id,
        ArtifactId = m.ArtifactId,
        Name = m.Name,
        FileName = m.FileName,
        FileFormat = m.FileFormat,
        ContentType = m.ContentType,
        FileSize = m.FileSize,
        ModelUrl = m.FileFormat == "sketchfab" ? m.StorageKey : $"/api/artifacts/{m.ArtifactId}/model-3d/file",
        PosterUrl = m.PosterUrl,
        AutoRotate = m.AutoRotate,
        CameraOrbit = m.CameraOrbit,
        FieldOfView = m.FieldOfView,
        Scale = m.Scale,
        UpdatedAt = m.UpdatedAt
    };

    private static List<ArtifactMediaResponseDto> MapMediaList(Artifact artifact) =>
        artifact.MediaItems?
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .Select(m => new ArtifactMediaResponseDto
            {
                Id = m.Id,
                ArtifactId = m.ArtifactId,
                MediaType = m.MediaType,
                Title = m.Title,
                LanguageCode = m.LanguageCode,
                FileName = m.FileName,
                ContentType = m.ContentType,
                FileSize = m.FileSize,
                SortOrder = m.SortOrder,
                FileUrl = $"/api/artifacts/{m.ArtifactId}/media/{m.Id}/file",
                CreatedAt = m.CreatedAt
            }).ToList() ?? [];

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
        CreatedAt = artifact.CreatedAt,
        Topics = MapTopics(artifact),
        Model3D = artifact.Model3D is not null ? Map3D(artifact.Model3D) : null,
        Media = MapMediaList(artifact)
    };
}
