using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Repositories;

public class MuseumRepository(ApplicationDbContext db) : IMuseumRepository
{
    public async Task<(IList<MuseumDiscoveryItem> Items, int TotalCount)> DiscoverAsync(
        MuseumDiscoveryQuery request, CancellationToken cancellationToken = default)
    {
        var museums = db.Museums.AsNoTracking().AsQueryable();
        if (request.IsActive.HasValue)
            museums = museums.Where(m => m.IsActive == request.IsActive.Value);
        if (request.CityCode is not null)
            museums = museums.Where(m => m.CityCode == request.CityCode);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            museums = museums.Where(m => m.Name.ToLower().Contains(keyword)
                || (m.Address != null && m.Address.ToLower().Contains(keyword)));
        }

        var total = await museums.CountAsync(cancellationToken);
        const double radians = Math.PI / 180;
        var latitude = request.Latitude ?? 0;
        var longitude = request.Longitude ?? 0;
        var hasOrigin = request.Latitude.HasValue;
        // Clamp floating point rounding at identical/antipodal coordinates.
        var query = museums.Select(m => new
        {
            Museum = m,
            DistanceKm = hasOrigin && m.Latitude != null && m.Longitude != null
                && m.Latitude >= -90 && m.Latitude <= 90 && m.Longitude >= -180 && m.Longitude <= 180
                ? (double?)(12742 * Math.Asin(Math.Sqrt(Math.Min(1, Math.Max(0,
                    Math.Pow(Math.Sin(((double)m.Latitude.Value - latitude) * radians / 2), 2)
                    + Math.Cos(latitude * radians) * Math.Cos((double)m.Latitude.Value * radians)
                    * Math.Pow(Math.Sin(((double)m.Longitude.Value - longitude) * radians / 2), 2))))))
                : null
        });
        query = request.SortBy == "distance"
            ? query.OrderBy(m => m.DistanceKm == null).ThenBy(m => m.DistanceKm).ThenBy(m => m.Museum.Id)
            : query.OrderByDescending(m => m.Museum.CreatedAt).ThenBy(m => m.Museum.Id);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return (items.Select(m => new MuseumDiscoveryItem(m.Museum, m.DistanceKm)).ToList(), total);
    }

    public async Task<Museum?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Museums.FindAsync([id], cancellationToken);
    }

    public async Task<(IList<Museum> Items, int TotalCount)> GetPagedAsync(
        string? keyword, bool? isActive, int page, int pageSize)
    {
        var query = db.Museums.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(m => m.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(kw)
                                  || (m.Address != null && m.Address.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        var normalized = name.Trim().ToLower();
        var query = db.Museums.Where(m => m.Name.ToLower() == normalized);

        if (excludeId.HasValue)
            query = query.Where(m => m.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<Museum> CreateAsync(Museum museum)
    {
        db.Museums.Add(museum);
        await db.SaveChangesAsync();
        return museum;
    }

    public async Task UpdateAsync(Museum museum)
    {
        db.Museums.Update(museum);
        await db.SaveChangesAsync();
    }
}
