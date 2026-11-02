using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Repositories;

public class ArtifactRepository(ApplicationDbContext db) : IArtifactRepository
{
    private IQueryable<Artifact> DetailedQuery => db.Artifacts
        .Include(a => a.Museum)
        .Include(a => a.Gallery)
        .Include(a => a.Model3D)
        .Include(a => a.MediaItems)
        .Include(a => a.ArtifactTopics)
            .ThenInclude(at => at.Topic);

    public async Task<Artifact?> GetByIdAsync(int id)
    {
        return await DetailedQuery.FirstOrDefaultAsync(a => a.Id == id);
    }

    public Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(
        int? museumId, int? galleryId, string? keyword, bool? isActive, int page, int pageSize)
        => GetPagedAsync(museumId, galleryId, null, keyword, isActive, page, pageSize);

    public async Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(
        int? museumId, int? galleryId, int? topicId, string? keyword, bool? isActive, int page, int pageSize)
    {
        var query = DetailedQuery.AsNoTracking().AsQueryable();

        if (museumId.HasValue)
            query = query.Where(a => a.MuseumId == museumId.Value);

        if (galleryId.HasValue)
            query = query.Where(a => a.GalleryId == galleryId.Value);

        if (topicId.HasValue)
            query = query.Where(a => a.ArtifactTopics.Any(at => at.TopicId == topicId.Value));

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

    public async Task<Artifact?> GetByQrCodeAsync(string qrCode)
    {
        var normalized = qrCode.Trim();
        return await DetailedQuery
            .FirstOrDefaultAsync(a => a.QrCode == normalized && a.IsActive);
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
