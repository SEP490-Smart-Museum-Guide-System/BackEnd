using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Repositories;

public class MuseumRepository(ApplicationDbContext db) : IMuseumRepository
{
    public async Task<Museum?> GetByIdAsync(int id)
    {
        return await db.Museums.FindAsync(id);
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
