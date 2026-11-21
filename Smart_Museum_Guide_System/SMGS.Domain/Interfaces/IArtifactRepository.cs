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
    Task<Artifact?> GetByQrCodeAsync(string qrCode) => Task.FromResult<Artifact?>(null);
    Task<(Artifact? Created, string? Error)> CreateAsync(Artifact artifact, IReadOnlyCollection<int> topicIds);
    Task<bool> UpdateAsync(Artifact artifact, bool checkCode = false);
}
