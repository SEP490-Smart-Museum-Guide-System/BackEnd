using Microsoft.EntityFrameworkCore;
using Npgsql;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;
namespace SMGS.Infrastructure.Repositories;

public class ArtifactContentRepository(ApplicationDbContext db) : IArtifactContentRepository
{
    private IQueryable<ArtifactContentVersion> Query => db.ArtifactContentVersions.Include(v => v.Content).ThenInclude(c => c.Artifact);
    public async Task<(List<ArtifactContentVersion> Items, int TotalCount)> GetPagedAsync(int? museumId, int? artifactId, string? status, int page, int pageSize)
    {
        var query = Query.AsNoTracking();
        if (museumId.HasValue) query = query.Where(v => v.Content.Artifact.MuseumId == museumId);
        if (artifactId.HasValue) query = query.Where(v => v.Content.ArtifactId == artifactId);
        if (status is not null) query = query.Where(v => v.Status == status);
        var count = await query.CountAsync();
        return (await query.OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id).Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync(), count);
    }
    public Task<ArtifactContentVersion?> GetByIdAsync(int id) => Query.FirstOrDefaultAsync(v => v.Id == id);
    public Task<ArtifactContentVersion?> GetPublishedAsync(int artifactId, string languageCode) => Query.AsNoTracking()
        .FirstOrDefaultAsync(v => v.Content.ArtifactId == artifactId && v.Content.LanguageCode == languageCode
            && v.IsCurrent && v.Status == ContentStatuses.Published && v.Content.Artifact.IsActive);
    public async Task<bool> CreateAsync(int artifactId, string languageCode, ArtifactContentVersion version)
    {
        var content = await db.ArtifactContents.Include(c => c.Artifact).Include(c => c.Versions)
            .FirstOrDefaultAsync(c => c.ArtifactId == artifactId && c.LanguageCode == languageCode);
        if (content is null)
        {
            content = new ArtifactContent { ArtifactId = artifactId, LanguageCode = languageCode,
                Artifact = await db.Artifacts.SingleAsync(a => a.Id == artifactId) };
            db.ArtifactContents.Add(content);
        }
        version.VersionNo = content.Versions.Select(v => v.VersionNo).DefaultIfEmpty(0).Max() + 1;
        version.Content = content;
        content.Revision = Guid.NewGuid();
        db.ArtifactContentVersions.Add(version);
        return await SaveAsync();
    }
    public async Task<bool> ReviewAsync(ArtifactContentVersion version, Guid reviewerId, bool approve, string? note)
    {
        if (version.Status != ContentStatuses.PendingReview) return false;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            version.Content.Revision = Guid.NewGuid();
            if (approve)
            {
                var current = await db.ArtifactContentVersions.Where(v => v.ArtifactContentId == version.ArtifactContentId && v.IsCurrent).ToListAsync();
                foreach (var old in current) { old.IsCurrent = false; old.Status = ContentStatuses.Archived; }
            }
            // Release the unique current-version slot before publishing the replacement.
            // Both saves are atomic, and the parent revision detects concurrent reviews.
            await db.SaveChangesAsync();
            version.Status = approve ? ContentStatuses.Published : ContentStatuses.Rejected;
            version.IsCurrent = approve;
            version.ReviewedBy = reviewerId;
            version.ReviewedAt = DateTime.UtcNow;
            version.ReviewNote = note;
            version.PublishedAt = approve ? version.ReviewedAt : null;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return false; }
    }
    public async Task<List<ArtifactContentVersion>> GetAllPublishedByArtifactIdAsync(int artifactId)
    {
        return await Query.AsNoTracking()
            .Where(v => v.Content.ArtifactId == artifactId
                && v.IsCurrent
                && v.Status == ContentStatuses.Published
                && v.Content.Artifact.IsActive)
            .OrderBy(v => v.Content.LanguageCode)
            .ToListAsync();
    }

    private async Task<bool> SaveAsync()
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return false; }
    }
}
