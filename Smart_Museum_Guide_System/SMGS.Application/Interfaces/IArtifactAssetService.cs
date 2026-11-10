using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.Common;

namespace SMGS.Application.Interfaces;

public interface IArtifactAssetService
{
    Task<ResponseDto<List<ArtifactMediaResponseDto>>> GetMediaListAsync(
        int artifactId, string? mediaType = null, string? languageCode = null, Guid? userId = null);

    Task<ResponseDto<ArtifactMediaResponseDto>> UploadMediaAsync(
        int artifactId,
        byte[] bytes,
        string originalFileName,
        string? title,
        string? mediaType,
        string? languageCode,
        int sortOrder,
        Guid userId,
        bool isAdmin, CancellationToken cancellationToken = default);

    Task<ResponseDto<(Stream Content, string ContentType, string DownloadName)>> GetMediaFileAsync(
        int artifactId, int mediaId, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<ResponseDto<object>> DeleteMediaAsync(
        int artifactId, int mediaId, Guid userId, bool isAdmin);

    Task<ResponseDto<Artifact3DModelResponseDto>> Get3DModelAsync(
        int artifactId, Guid? userId = null);

    Task<ResponseDto<Artifact3DModelResponseDto>> UploadOrReplace3DModelAsync(
        int artifactId,
        Stream content,
        long length,
        string contentType,
        string originalFileName,
        Update3DModelSettingsRequestDto settings,
        Guid userId,
        bool isAdmin, CancellationToken cancellationToken = default);

    Task<ResponseDto<Artifact3DModelResponseDto>> LinkSketchfabModelAsync(
        int artifactId, string url, Guid userId, bool isAdmin);

    Task<ResponseDto<Artifact3DModelResponseDto>> Update3DModelSettingsAsync(
        int artifactId,
        Update3DModelSettingsRequestDto settings,
        Guid userId,
        bool isAdmin);

    Task<ResponseDto<(Stream Content, string ContentType, string DownloadName)>> Get3DModelFileAsync(
        int artifactId, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<ResponseDto<object>> Delete3DModelAsync(
        int artifactId, Guid userId, bool isAdmin);

    Task<ResponseDto<ArtifactViewerResponseDto>> GetViewerDataAsync(
        int artifactId, string? languageCode = null, Guid? userId = null);
}

