using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.DTOs.ArtifactContent.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
namespace SMGS.Application.Services;

public class ArtifactContentService(IArtifactContentRepository repository, IArtifactRepository artifacts, ICommerceService? commerce = null) : IArtifactContentService
{
    public async Task<ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>> GetAllAsync(
        int? museumId, int? artifactId, string? status, int page, int pageSize)
    {
        if (status is not null && status is not (ContentStatuses.PendingReview or ContentStatuses.Published or ContentStatuses.Rejected or ContentStatuses.Archived))
            return ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>.Fail("Trạng thái không hợp lệ.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, count) = await repository.GetPagedAsync(museumId, artifactId, status, page, pageSize);
        return ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>.Success(new()
        {
            Items = items.Select(Map).ToList(), Page = page, PageSize = pageSize, TotalCount = count
        });
    }
    public async Task<ResponseDto<ArtifactContentResponseDto>> GetByIdAsync(int id)
    {
        var version = await repository.GetByIdAsync(id);
        return version is null ? Missing() : ResponseDto<ArtifactContentResponseDto>.Success(Map(version));
    }
    public async Task<ResponseDto<ArtifactContentResponseDto>> GetPublishedAsync(int artifactId, string languageCode, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return ResponseDto<ArtifactContentResponseDto>.Fail("Mã ngôn ngữ không hợp lệ.");
        var artifact = await artifacts.GetByIdAsync(artifactId);
        if (artifact == null || !artifact.IsActive || artifact.Museum?.IsActive == false) return Missing();
        var allowed = commerce != null ? await commerce.CanReadArtifact(artifactId, userId) : artifact.AccessLevel == "free";
        if (!allowed) return ResponseDto<ArtifactContentResponseDto>.Fail("Cần pass còn hiệu lực cho nội dung premium này.", 403);
        var version = await repository.GetPublishedAsync(artifactId, languageCode.Trim().ToLowerInvariant());
        return version is null ? Missing() : ResponseDto<ArtifactContentResponseDto>.Success(Map(version));
    }
    public async Task<ResponseDto<List<ArtifactContentResponseDto>>> GetAllPublishedByArtifactAsync(int artifactId, Guid? userId = null)
    {
        var artifact = await artifacts.GetByIdAsync(artifactId);
        if (artifact == null || !artifact.IsActive || artifact.Museum?.IsActive == false)
            return ResponseDto<List<ArtifactContentResponseDto>>.Fail("Không tìm thấy hiện vật.", 404);
        var allowed = commerce != null ? await commerce.CanReadArtifact(artifactId, userId) : artifact.AccessLevel == "free";
        if (!allowed)
            return ResponseDto<List<ArtifactContentResponseDto>>.Fail("Cần pass còn hiệu lực cho nội dung premium này.", 403);
        var versions = await repository.GetAllPublishedByArtifactIdAsync(artifactId);
        return ResponseDto<List<ArtifactContentResponseDto>>.Success(versions.Select(Map).ToList());
    }
    public async Task<ResponseDto<List<ArtifactContentResponseDto>>> GetVersionHistoryAsync(int artifactId, string? languageCode = null)
    {
        var artifact = await artifacts.GetByIdAsync(artifactId);
        if (artifact == null)
            return ResponseDto<List<ArtifactContentResponseDto>>.Fail("Không tìm thấy hiện vật.", 404);
        var (items, _) = await repository.GetPagedAsync(null, artifactId, null, 1, 100);
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var lang = languageCode.Trim().ToLowerInvariant();
            items = items.Where(v => v.Content.LanguageCode == lang).ToList();
        }
        var ordered = items
            .OrderBy(v => v.Content.LanguageCode)
            .ThenByDescending(v => v.VersionNo)
            .Select(Map)
            .ToList();
        return ResponseDto<List<ArtifactContentResponseDto>>.Success(ordered);
    }
    public async Task<ResponseDto<ArtifactContentResponseDto>> SubmitAsync(int artifactId, SubmitArtifactContentRequestDto dto, Guid userId)
    {
        if (userId == Guid.Empty) return ResponseDto<ArtifactContentResponseDto>.Fail("Tài khoản không hợp lệ.", 401);
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Description) || string.IsNullOrWhiteSpace(dto.LanguageCode))
            return ResponseDto<ArtifactContentResponseDto>.Fail("Ngôn ngữ, tiêu đề và nội dung là bắt buộc.");
        var artifact = await artifacts.GetByIdAsync(artifactId);
        if (artifact is null) return ResponseDto<ArtifactContentResponseDto>.Fail("Không tìm thấy hiện vật.", 404);
        if (!artifact.IsActive) return ResponseDto<ArtifactContentResponseDto>.Fail("Hiện vật đã bị vô hiệu hóa.", 409);
        var version = new ArtifactContentVersion
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            NarrationText = string.IsNullOrWhiteSpace(dto.NarrationText) ? null : dto.NarrationText.Trim(),
            AudioUrl = string.IsNullOrWhiteSpace(dto.AudioUrl) ? null : dto.AudioUrl.Trim(),
            IsAiGenerated = dto.IsAiGenerated,
            CreatedBy = userId
        };
        if (!await repository.CreateAsync(artifactId, dto.LanguageCode.Trim().ToLowerInvariant(), version)) return Conflict();
        var message = dto.IsAiGenerated
            ? "Đã gửi nội dung AI chờ Curator duyệt."
            : "Đã lưu phiên bản nội dung & narration thủ công chờ Curator duyệt.";
        return ResponseDto<ArtifactContentResponseDto>.Success(Map(version), message, 201);
    }
    public async Task<ResponseDto<ArtifactContentResponseDto>> ReviewAsync(int id, ReviewArtifactContentRequestDto dto, Guid userId, bool approve)
    {
        if (userId == Guid.Empty) return ResponseDto<ArtifactContentResponseDto>.Fail("Tài khoản không hợp lệ.", 401);
        var version = await repository.GetByIdAsync(id);
        if (version is null) return Missing();
        if (version.Status != ContentStatuses.PendingReview) return Conflict();
        if (!version.Content.Artifact.IsActive) return ResponseDto<ArtifactContentResponseDto>.Fail("Hiện vật đã bị vô hiệu hóa.", 409);
        if (!approve && string.IsNullOrWhiteSpace(dto.Note)) return ResponseDto<ArtifactContentResponseDto>.Fail("Vui lòng nhập lý do từ chối.");
        if (!await repository.ReviewAsync(version, userId, approve, dto.Note?.Trim())) return Conflict();
        return ResponseDto<ArtifactContentResponseDto>.Success(Map(version), approve ? "Đã duyệt và xuất bản nội dung." : "Đã từ chối nội dung.");
    }
    private static ResponseDto<ArtifactContentResponseDto> Missing() => ResponseDto<ArtifactContentResponseDto>.Fail("Không tìm thấy nội dung.", 404);
    private static ResponseDto<ArtifactContentResponseDto> Conflict() => ResponseDto<ArtifactContentResponseDto>.Fail("Nội dung đã thay đổi hoặc không còn chờ duyệt. Vui lòng tải lại.", 409);
    private static ArtifactContentResponseDto Map(ArtifactContentVersion v) => new()
    {
        Id = v.Id, ArtifactId = v.Content.ArtifactId, MuseumId = v.Content.Artifact.MuseumId,
        ArtifactName = v.Content.Artifact.Name, LanguageCode = v.Content.LanguageCode,
        VersionNo = v.VersionNo, Title = v.Title, Description = v.Description,
        NarrationText = v.NarrationText, AudioUrl = v.AudioUrl,
        IsAiGenerated = v.IsAiGenerated, Status = v.Status, IsCurrent = v.IsCurrent,
        CreatedBy = v.CreatedBy, ReviewedBy = v.ReviewedBy, ReviewedAt = v.ReviewedAt,
        ReviewNote = v.ReviewNote, PublishedAt = v.PublishedAt, CreatedAt = v.CreatedAt
    };
}
