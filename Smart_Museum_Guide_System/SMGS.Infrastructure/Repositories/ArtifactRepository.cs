using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Repositories;

public class ArtifactRepository(ApplicationDbContext db) : IArtifactRepository
{
    public async Task<Artifact?> GetByIdAsync(int id)
    {
        return await db.Artifacts.FindAsync(id);
    }

    public async Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize)
    {
        var query = db.Artifacts.AsQueryable();

        if (museumId.HasValue)
            query = query.Where(a => a.MuseumId == museumId.Value);

        if (galleryId.HasValue)
            query = query.Where(a => a.GalleryId == galleryId.Value);

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(kw) ||
                (a.Code != null && a.Code.ToLower().Contains(kw)) ||
                (a.QrCode != null && a.QrCode.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool> ExistsByQrCodeAsync(string qrCode, int? excludeId = null)
    {
        var normalized = qrCode.Trim();
        var query = db.Artifacts.Where(a => a.QrCode == normalized);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByCodeAsync(int museumId, string code, int? excludeId = null)
    {
        var normalized = code.Trim().ToLower();
        var query = db.Artifacts.Where(a =>
            a.MuseumId == museumId &&
            a.Code != null &&
            a.Code.ToLower() == normalized);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<Artifact> CreateAsync(Artifact artifact)
    {
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();
        return artifact;
    }

    public async Task UpdateAsync(Artifact artifact)
    {
        db.Artifacts.Update(artifact);
        await db.SaveChangesAsync();
    }
}
