using SMGS.Domain.Entities;

namespace SMGS.Domain.Interfaces;

public interface IArtifactRepository
{
    Task<Artifact?> GetByIdAsync(int id);
    Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize);
    Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(
        int? museumId, int? galleryId, int? topicId, string? keyword, bool? isActive, int page, int pageSize)
        => GetPagedAsync(museumId, galleryId, keyword, isActive, page, pageSize);
    Task<bool> ExistsByQrCodeAsync(string qrCode, int? excludeId = null);
    Task<bool> ExistsByCodeAsync(int museumId, string code, int? excludeId = null);
    Task<Artifact?> GetByQrCodeAsync(string qrCode) => Task.FromResult<Artifact?>(null);
    Task<Artifact> CreateAsync(Artifact artifact);
    Task UpdateAsync(Artifact artifact);
}
