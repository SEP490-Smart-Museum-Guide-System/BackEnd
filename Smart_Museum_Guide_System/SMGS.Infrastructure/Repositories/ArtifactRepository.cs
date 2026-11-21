using System.Globalization;
using System.Numerics;
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

    private async Task<bool> ExistsByCodeAsync(int museumId, string code, int? excludeId = null)
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

    public async Task<(Artifact? Created, string? Error)> CreateAsync(
        Artifact artifact, IReadOnlyCollection<int> topicIds)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await LockMuseumAsync(artifact.MuseumId) is null)
            return (null, "Không tìm thấy bảo tàng.");

        if (artifact.GalleryId is int galleryId &&
            !await (from gallery in db.Galleries
                    join floor in db.Floors on gallery.FloorId equals floor.Id
                    join building in db.Buildings on floor.BuildingId equals building.Id
                    where gallery.Id == galleryId && building.MuseumId == artifact.MuseumId
                    select gallery.Id).AnyAsync())
            return (null, "Phòng trưng bày không thuộc bảo tàng của hiện vật.");

        var ids = topicIds.Distinct().ToArray();
        if (ids.Length > 0)
        {
            var topics = await db.Topics
                .Where(t => ids.Contains(t.Id) && t.MuseumId == artifact.MuseumId && t.IsActive)
                .ToListAsync();
            if (topics.Count != ids.Length)
                return (null, "Một hoặc nhiều chủ đề không thuộc bảo tàng của hiện vật hoặc đã ngừng hoạt động.");

            foreach (var topic in topics)
                artifact.ArtifactTopics.Add(new ArtifactTopic { Topic = topic, AssignedAt = artifact.CreatedAt });
        }

        if (string.IsNullOrWhiteSpace(artifact.Code))
        {
            var codes = await db.Artifacts
                .Where(a => a.MuseumId == artifact.MuseumId && a.Code != null && a.Code.ToUpper().StartsWith("HV"))
                .Select(a => a.Code!)
                .ToListAsync();

            var largest = BigInteger.Zero;
            foreach (var code in codes)
                if (BigInteger.TryParse(code.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    largest = BigInteger.Max(largest, number);

            artifact.Code = "HV" + (largest + 1).ToString("D3", CultureInfo.InvariantCulture);
            if (artifact.Code.Length > 100)
                return (null, "Đã hết mã hiện vật tự động cho bảo tàng này.");
        }
        else if (await ExistsByCodeAsync(artifact.MuseumId, artifact.Code))
        {
            return (null, "Mã hiện vật đã tồn tại trong bảo tàng này.");
        }

        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (artifact, null);
    }

    public async Task<bool> UpdateAsync(Artifact artifact, bool checkCode = false)
    {
        await using var transaction = checkCode ? await db.Database.BeginTransactionAsync() : null;
        if (checkCode)
        {
            await LockMuseumAsync(artifact.MuseumId);
            if (!string.IsNullOrWhiteSpace(artifact.Code) &&
                await ExistsByCodeAsync(artifact.MuseumId, artifact.Code, artifact.Id))
                return false;
        }

        if (db.Entry(artifact).State == EntityState.Detached)
            db.Artifacts.Update(artifact);
        await db.SaveChangesAsync();
        if (transaction is not null)
            await transaction.CommitAsync();
        return true;
    }

    private Task<Museum?> LockMuseumAsync(int id) => db.Museums
        .FromSqlInterpolated($"SELECT * FROM museums WHERE \"Id\" = {id} FOR UPDATE")
        .AsNoTracking().SingleOrDefaultAsync();
}
