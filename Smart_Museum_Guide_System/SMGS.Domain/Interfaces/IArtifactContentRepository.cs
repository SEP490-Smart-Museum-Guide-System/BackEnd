using SMGS.Domain.Entities;
namespace SMGS.Domain.Interfaces;
public interface IArtifactContentRepository
{
    Task<(List<ArtifactContentVersion> Items, int TotalCount)> GetPagedAsync(int? museumId, int? artifactId, string? status, int page, int pageSize);
    Task<ArtifactContentVersion?> GetByIdAsync(int id);
    Task<ArtifactContentVersion?> GetPublishedAsync(int artifactId, string languageCode);
    Task<bool> CreateAsync(int artifactId, string languageCode, ArtifactContentVersion version);
    Task<bool> ReviewAsync(ArtifactContentVersion version, Guid reviewerId, bool approve, string? note);

    /// <summary>
    /// Lấy tất cả nội dung đã xuất bản cho một hiện vật (tất cả ngôn ngữ).
    /// </summary>
    Task<List<ArtifactContentVersion>> GetAllPublishedByArtifactIdAsync(int artifactId);
}
