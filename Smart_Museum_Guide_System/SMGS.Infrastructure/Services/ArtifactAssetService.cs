using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Topic;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class ArtifactAssetService(
    ApplicationDbContext db,
    ArtifactAssetStore store,
    ICommerceService? commerce = null) : IArtifactAssetService
{
    private async Task DeleteUnreferenced(string key)
    {
        if (key.StartsWith("https://sketchfab.com/models/", StringComparison.OrdinalIgnoreCase)) return;
        try {
            if (!await db.ArtifactMedias.AnyAsync(m => m.StorageKey == key) && !await db.Artifact3DModels.AnyAsync(m => m.StorageKey == key))
                await store.DeleteIfExists(key);
        }
        catch { /* Database commit succeeded; retain orphan rather than undo a valid replacement. */ }
    }

    private async Task<bool> CanManageArtifactAsync(Guid userId, bool isAdmin, Artifact artifact)
    {
        if (isAdmin) return true;
        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == artifact.MuseumId &&
            (
                (a.GalleryId == null && a.ArtifactId == null) ||
                (artifact.GalleryId != null && a.GalleryId == artifact.GalleryId && a.ArtifactId == null) ||
                a.ArtifactId == artifact.Id
            ));
    }

    private async Task<bool> CanReadArtifactAssetsAsync(Artifact artifact, Guid? userId)
    {
        if (userId.HasValue) {
            var isAdmin = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == userId.Value && role.Name == RoleNames.Admin select ur).AnyAsync();
            if (await CanManageArtifactAsync(userId.Value, isAdmin, artifact)) return true;
        }
        if (commerce is not null)
            return await commerce.CanReadArtifact(artifact.Id, userId);
        return artifact.AccessLevel == "free";
    }

    public static ArtifactMediaResponseDto MapMedia(ArtifactMedia m) => new()
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
    };

    public static Artifact3DModelResponseDto Map3DModel(Artifact3DModel m) => new()
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

    public async Task<ResponseDto<List<ArtifactMediaResponseDto>>> GetMediaListAsync(
        int artifactId, string? mediaType = null, string? languageCode = null, Guid? userId = null)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<List<ArtifactMediaResponseDto>>.Fail("Không tìm thấy hiện vật.", 404);

        var query = db.ArtifactMedias.AsNoTracking().Where(m => m.ArtifactId == artifactId);

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            var type = mediaType.Trim().ToLowerInvariant();
            query = query.Where(m => m.MediaType == type);
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var lang = languageCode.Trim().ToLowerInvariant();
            query = query.Where(m => m.LanguageCode == null || m.LanguageCode == lang);
        }

        var items = await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .ToListAsync();

        return ResponseDto<List<ArtifactMediaResponseDto>>.Success(items.Select(MapMedia).ToList());
    }

    public async Task<ResponseDto<ArtifactMediaResponseDto>> UploadMediaAsync(
        int artifactId,
        byte[] bytes,
        string originalFileName,
        string? title,
        string? mediaType,
        string? languageCode,
        int sortOrder,
        Guid userId,
        bool isAdmin, CancellationToken cancellationToken = default)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<ArtifactMediaResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<ArtifactMediaResponseDto>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        (string StorageKey, string MediaType, string ContentType) saved;
        try
        {
            saved = await store.SaveMediaAsync(bytes, originalFileName, mediaType, artifact.MuseumId, artifactId, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return ResponseDto<ArtifactMediaResponseDto>.Fail(ex.Message, 400);
        }

        var safeFileName = Path.GetFileName(originalFileName);
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(safeFileName)
            : title.Trim();

        var media = new ArtifactMedia
        {
            ArtifactId = artifactId,
            MediaType = saved.MediaType,
            Title = resolvedTitle,
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim().ToLowerInvariant(),
            FileName = safeFileName,
            StorageKey = saved.StorageKey,
            ContentType = saved.ContentType,
            FileSize = bytes.LongLength,
            SortOrder = sortOrder,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            db.ArtifactMedias.Add(media);
            await db.SaveChangesAsync(cancellationToken);
            return ResponseDto<ArtifactMediaResponseDto>.Success(MapMedia(media), "Tải lên media thành công.", 201);
        }
        catch
        {
            await store.DeleteIfExists(saved.StorageKey);
            throw;
        }
    }

    public async Task<ResponseDto<(Stream Content, string ContentType, string DownloadName)>> GetMediaFileAsync(
        int artifactId, int mediaId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var media = await db.ArtifactMedias.AsNoTracking()
            .Include(m => m.Artifact)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.ArtifactId == artifactId);

        if (media is null)
            return ResponseDto<(Stream, string, string)>.Fail("Không tìm thấy media.", 404);

        if (!await CanReadArtifactAssetsAsync(media.Artifact, userId))
            return ResponseDto<(Stream, string, string)>.Fail("Cần pass còn hiệu lực cho nội dung premium này.", 403);

        try { return ResponseDto<(Stream, string, string)>.Success((await store.OpenReadAsync(media.StorageKey, cancellationToken), media.ContentType, media.FileName)); }
        catch (FileNotFoundException) { return ResponseDto<(Stream, string, string)>.Fail("Không tìm thấy file.", 404); }
    }

    public async Task<ResponseDto<object>> DeleteMediaAsync(
        int artifactId, int mediaId, Guid userId, bool isAdmin)
    {
        var media = await db.ArtifactMedias
            .Include(m => m.Artifact)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.ArtifactId == artifactId);

        if (media is null)
            return ResponseDto<object>.Fail("Không tìm thấy media.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, media.Artifact))
            return ResponseDto<object>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var key = media.StorageKey;
        db.ArtifactMedias.Remove(media);
        await db.SaveChangesAsync();
        await DeleteUnreferenced(key);

        return ResponseDto<object>.Success(null!, "Đã xóa media thành công.");
    }

    public async Task<ResponseDto<Artifact3DModelResponseDto>> Get3DModelAsync(
        int artifactId, Guid? userId = null)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        var model = await db.Artifact3DModels.AsNoTracking().FirstOrDefaultAsync(m => m.ArtifactId == artifactId);
        if (model is null)
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Hiện vật chưa có mô hình 3D.", 404);

        return ResponseDto<Artifact3DModelResponseDto>.Success(Map3DModel(model));
    }

    public async Task<ResponseDto<Artifact3DModelResponseDto>> UploadOrReplace3DModelAsync(
        int artifactId,
        Stream content,
        long length,
        string contentType,
        string originalFileName,
        Update3DModelSettingsRequestDto settings,
        Guid userId,
        bool isAdmin, CancellationToken cancellationToken = default)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var existing = await db.Artifact3DModels.FirstOrDefaultAsync(m => m.ArtifactId == artifactId);
        (string StorageKey, string Format, string ContentType) saved;
        try
        {
            saved = await store.Save3DModelAsync(content, length, originalFileName, contentType, artifact.MuseumId, artifactId, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return ResponseDto<Artifact3DModelResponseDto>.Fail(ex.Message, 400);
        }

        var safeFileName = Path.GetFileName(originalFileName);
        var modelName = string.IsNullOrWhiteSpace(settings.Name)
            ? artifact.Name
            : settings.Name.Trim();

        string? oldKey = existing?.StorageKey;
        var isNew = existing is null;

        try
        {
            if (existing is null)
            {
                existing = new Artifact3DModel
                {
                    ArtifactId = artifactId,
                    Name = modelName,
                    FileName = safeFileName,
                    StorageKey = saved.StorageKey,
                    FileFormat = saved.Format,
                    ContentType = saved.ContentType,
                    FileSize = length,
                    PosterUrl = string.IsNullOrWhiteSpace(settings.PosterUrl) ? null : settings.PosterUrl.Trim(),
                    AutoRotate = settings.AutoRotate,
                    CameraOrbit = string.IsNullOrWhiteSpace(settings.CameraOrbit) ? null : settings.CameraOrbit.Trim(),
                    FieldOfView = string.IsNullOrWhiteSpace(settings.FieldOfView) ? null : settings.FieldOfView.Trim(),
                    Scale = settings.Scale <= 0 ? 1.0m : settings.Scale,
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Artifact3DModels.Add(existing);
            }
            else
            {
                existing.Name = modelName;
                existing.FileName = safeFileName;
                existing.StorageKey = saved.StorageKey;
                existing.FileFormat = saved.Format;
                existing.ContentType = saved.ContentType;
                existing.FileSize = length;
                existing.PosterUrl = string.IsNullOrWhiteSpace(settings.PosterUrl) ? existing.PosterUrl : settings.PosterUrl.Trim();
                existing.AutoRotate = settings.AutoRotate;
                existing.CameraOrbit = string.IsNullOrWhiteSpace(settings.CameraOrbit) ? existing.CameraOrbit : settings.CameraOrbit.Trim();
                existing.FieldOfView = string.IsNullOrWhiteSpace(settings.FieldOfView) ? existing.FieldOfView : settings.FieldOfView.Trim();
                existing.Scale = settings.Scale <= 0 ? 1.0m : settings.Scale;
                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (oldKey is not null && oldKey != saved.StorageKey)
                await DeleteUnreferenced(oldKey);

            return ResponseDto<Artifact3DModelResponseDto>.Success(
                Map3DModel(existing),
                isNew ? "Tải lên mô hình 3D thành công." : "Thay thế mô hình 3D thành công.",
                isNew ? 201 : 200);
        }
        catch
        {
            await store.DeleteIfExists(saved.StorageKey);
            throw;
        }
    }

    public async Task<ResponseDto<Artifact3DModelResponseDto>> LinkSketchfabModelAsync(
        int artifactId, string url, Guid userId, bool isAdmin)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host != "sketchfab.com" || uri.Port != 443 || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Link Sketchfab không hợp lệ.", 400);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        var id = parts.Length == 3 && parts[0] == "models" && parts[2] == "embed" ? parts[1]
            : parts.Length == 2 && parts[0] == "models" ? parts[1]
            : parts.Length == 2 && parts[0] == "3d-models" ? parts[1].Split('-').Last() : "";
        if (id.Length != 32 || !id.All(Uri.IsHexDigit))
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Link Sketchfab không hợp lệ.", 400);
        var embedUrl = $"https://sketchfab.com/models/{id.ToLowerInvariant()}/embed";
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null) return ResponseDto<Artifact3DModelResponseDto>.Fail("Không tìm thấy hiện vật.", 404);
        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);
        var model = await db.Artifact3DModels.FirstOrDefaultAsync(m => m.ArtifactId == artifactId);
        var oldKey = model?.StorageKey;
        if (model is null) {
            model = new Artifact3DModel { ArtifactId = artifactId };
            db.Artifact3DModels.Add(model);
        }
        model.Name = artifact.Name;
        model.FileName = "";
        model.StorageKey = embedUrl;
        model.FileFormat = "sketchfab";
        model.ContentType = "text/html";
        model.FileSize = 0;
        model.UpdatedBy = userId;
        model.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (!string.IsNullOrEmpty(oldKey) && oldKey != embedUrl) await DeleteUnreferenced(oldKey);
        return ResponseDto<Artifact3DModelResponseDto>.Success(Map3DModel(model));
    }

    public async Task<ResponseDto<Artifact3DModelResponseDto>> Update3DModelSettingsAsync(
        int artifactId,
        Update3DModelSettingsRequestDto settings,
        Guid userId,
        bool isAdmin)
    {
        var model = await db.Artifact3DModels
            .Include(m => m.Artifact)
            .FirstOrDefaultAsync(m => m.ArtifactId == artifactId);

        if (model is null)
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Hiện vật chưa có mô hình 3D.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, model.Artifact))
            return ResponseDto<Artifact3DModelResponseDto>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        if (!string.IsNullOrWhiteSpace(settings.Name))
            model.Name = settings.Name.Trim();

        model.PosterUrl = string.IsNullOrWhiteSpace(settings.PosterUrl) ? null : settings.PosterUrl.Trim();
        model.AutoRotate = settings.AutoRotate;
        model.CameraOrbit = string.IsNullOrWhiteSpace(settings.CameraOrbit) ? null : settings.CameraOrbit.Trim();
        model.FieldOfView = string.IsNullOrWhiteSpace(settings.FieldOfView) ? null : settings.FieldOfView.Trim();
        model.Scale = settings.Scale <= 0 ? 1.0m : settings.Scale;
        model.UpdatedBy = userId;
        model.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ResponseDto<Artifact3DModelResponseDto>.Success(Map3DModel(model), "Cập nhật cấu hình mô hình 3D thành công.");
    }

    public async Task<ResponseDto<(Stream Content, string ContentType, string DownloadName)>> Get3DModelFileAsync(
        int artifactId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var model = await db.Artifact3DModels.AsNoTracking()
            .Include(m => m.Artifact)
            .FirstOrDefaultAsync(m => m.ArtifactId == artifactId);

        if (model is null)
            return ResponseDto<(Stream, string, string)>.Fail("Hiện vật chưa có mô hình 3D.", 404);

        if (!await CanReadArtifactAssetsAsync(model.Artifact, userId))
            return ResponseDto<(Stream, string, string)>.Fail("Cần pass còn hiệu lực cho nội dung premium này.", 403);

        if (model.FileFormat == "sketchfab")
            return ResponseDto<(Stream, string, string)>.Fail("Mô hình Sketchfab chỉ xem qua link nhúng.", 404);

        try { return ResponseDto<(Stream, string, string)>.Success((await store.OpenReadAsync(model.StorageKey, cancellationToken), model.ContentType, model.FileName)); }
        catch (FileNotFoundException) { return ResponseDto<(Stream, string, string)>.Fail("Không tìm thấy file.", 404); }
    }

    public async Task<ResponseDto<object>> Delete3DModelAsync(
        int artifactId, Guid userId, bool isAdmin)
    {
        var model = await db.Artifact3DModels
            .Include(m => m.Artifact)
            .FirstOrDefaultAsync(m => m.ArtifactId == artifactId);

        if (model is null)
            return ResponseDto<object>.Fail("Hiện vật chưa có mô hình 3D.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, model.Artifact))
            return ResponseDto<object>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var key = model.StorageKey;
        db.Artifact3DModels.Remove(model);
        await db.SaveChangesAsync();
        await DeleteUnreferenced(key);

        return ResponseDto<object>.Success(null!, "Đã xóa mô hình 3D thành công.");
    }

    public async Task<ResponseDto<ArtifactViewerResponseDto>> GetViewerDataAsync(
        int artifactId, string? languageCode = null, Guid? userId = null)
    {
        var artifact = await db.Artifacts.AsNoTracking()
            .Include(a => a.Museum)
            .Include(a => a.Gallery)
            .Include(a => a.Model3D)
            .Include(a => a.MediaItems)
            .Include(a => a.ArtifactTopics)
                .ThenInclude(at => at.Topic)
            .FirstOrDefaultAsync(a => a.Id == artifactId && a.IsActive);

        if (artifact is null)
            return ResponseDto<ArtifactViewerResponseDto>.Fail("Không tìm thấy hiện vật.", 404);

        var allowed = await CanReadArtifactAssetsAsync(artifact, userId);
        if (!allowed)
            return ResponseDto<ArtifactViewerResponseDto>.Fail("Cần pass còn hiệu lực cho nội dung premium này.", 403);

        var contentsQuery = db.ArtifactContentVersions.AsNoTracking()
            .Include(v => v.Content)
            .Where(v => v.Content.ArtifactId == artifactId && v.IsCurrent && v.Status == ContentStatuses.Published);

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var lang = languageCode.Trim().ToLowerInvariant();
            contentsQuery = contentsQuery.Where(v => v.Content.LanguageCode == lang);
        }

        var publishedVersions = await contentsQuery.OrderBy(v => v.Content.LanguageCode).ToListAsync();

        var mediaQuery = artifact.MediaItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var lang = languageCode.Trim().ToLowerInvariant();
            mediaQuery = mediaQuery.Where(m => m.LanguageCode == null || m.LanguageCode == lang);
        }

        var viewer = new ArtifactViewerResponseDto
        {
            Id = artifact.Id,
            MuseumId = artifact.MuseumId,
            MuseumName = artifact.Museum?.Name ?? string.Empty,
            GalleryId = artifact.GalleryId,
            GalleryName = artifact.Gallery?.Name,
            Code = artifact.Code,
            Name = artifact.Name,
            QrCode = artifact.QrCode,
            AccessLevel = artifact.AccessLevel,
            Topics = artifact.ArtifactTopics
                .Where(at => at.Topic.IsActive)
                .OrderBy(at => at.Topic.Name)
                .Select(at => new ArtifactTopicTagDto
                {
                    Id = at.Topic.Id,
                    Name = at.Topic.Name,
                    Description = at.Topic.Description
                }).ToList(),
            Model3D = artifact.Model3D is not null ? Map3DModel(artifact.Model3D) : null,
            Media = mediaQuery
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.Id)
                .Select(MapMedia)
                .ToList(),
            Contents = publishedVersions.Select(v => new ArtifactScanContentDto
            {
                LanguageCode = v.Content.LanguageCode,
                Title = v.Title,
                Description = v.Description,
                NarrationText = v.NarrationText,
                AudioUrl = v.AudioUrl
            }).ToList()
        };

        return ResponseDto<ArtifactViewerResponseDto>.Success(viewer);
    }
}

