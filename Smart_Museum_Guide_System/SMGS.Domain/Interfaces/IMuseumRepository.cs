using SMGS.Domain.Entities;

namespace SMGS.Domain.Interfaces;

public interface IMuseumRepository
{
    Task<(IList<MuseumDiscoveryItem> Items, int TotalCount)> DiscoverAsync(MuseumDiscoveryQuery query, CancellationToken cancellationToken = default);
    Task<Museum?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(IList<Museum> Items, int TotalCount)> GetPagedAsync(string? keyword, bool? isActive, int page, int pageSize);
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
    Task<Museum> CreateAsync(Museum museum);
    Task UpdateAsync(Museum museum);
}
