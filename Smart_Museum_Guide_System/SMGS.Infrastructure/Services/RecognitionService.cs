using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class RecognitionService(ApplicationDbContext db, IVisitProgressService progress, IDailyQuizService daily,
    IGamificationService rewards, TimeProvider clock) : IRecognitionService
{
    public async Task<ArtifactCollectionDto> GetCollectionAsync(Guid user, int? museumId, int page, int pageSize)
    {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        // Group verified history so repeat/concurrent scans always produce one collection entry.
        var discoveries = db.Set<RecognitionHistory>().AsNoTracking()
            .Where(h => h.UserId == user && h.IsSuccess && (h.Method == "qr" || h.Method == "image") && h.ArtifactId != null)
            .GroupBy(h => h.ArtifactId)
            .Select(g => new { ArtifactId = g.Key, FirstDiscoveredAt = g.Min(h => h.RecognizedAt) });
        var collection = from discovery in discoveries
                         join artifact in db.Artifacts.AsNoTracking() on discovery.ArtifactId equals artifact.Id
                         select new { Artifact = artifact, discovery.FirstDiscoveredAt };
        var totalCollected = await collection.CountAsync();
        var museumRows = await collection.Select(x => new { Id = x.Artifact.MuseumId, x.Artifact.Museum!.Name })
            .Distinct().OrderBy(m => m.Name).ThenBy(m => m.Id).ToListAsync();
        var museums = museumRows.Select(m => new CollectionMuseum(m.Id, m.Name)).ToList();
        if (museumId.HasValue) collection = collection.Where(x => x.Artifact.MuseumId == museumId.Value);
        var totalCount = await collection.CountAsync();
        var rows = await collection.OrderByDescending(x => x.FirstDiscoveredAt).ThenBy(x => x.Artifact.Id)
            .Skip(skip).Take(take).Select(x => new {
                x.Artifact.Id, x.Artifact.Name, x.Artifact.MuseumId, MuseumName = x.Artifact.Museum!.Name,
                Available = x.Artifact.IsActive && x.Artifact.Museum.IsActive, x.FirstDiscoveredAt,
                ImageId = x.Artifact.MediaItems.Where(m => m.MediaType == "image")
                    .OrderBy(m => m.SortOrder).ThenBy(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault()
            }).ToListAsync();
        var items = rows.Select(x => new CollectedArtifact(x.Id, x.Name, x.MuseumId, x.MuseumName,
            x.Available, x.Available && x.ImageId.HasValue ? $"/api/artifacts/{x.Id}/media/{x.ImageId}/file" : null,
            x.FirstDiscoveredAt)).ToList();
        return new(items, totalCount, totalCollected, museums, page, (int)Math.Ceiling(totalCount / (double)take));
    }

    public async Task<ScanResult> RecordQrAsync(ScanQrRequest request, Guid user)
    {
        if (request.Method is not (null or "qr" or "image"))
            throw new ExperienceException("Phương thức nhận diện không hợp lệ.");
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.QrCode) || request.QrCode.Length > 200)
            throw new ExperienceException("Cần QR hợp lệ và RequestId UUID để chống gửi trùng.");
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var old = await db.Set<RecognitionHistory>().SingleOrDefaultAsync(x => x.UserId == user && x.RequestId == request.RequestId);
        if (old is not null) {
            var oldCode = await db.Artifacts.Where(a => a.Id == old.ArtifactId).Select(a => a.QrCode).SingleOrDefaultAsync();
            if (old.Method != (request.Method ?? "qr") || old.VisitId != request.VisitId || oldCode != request.QrCode.Trim()) throw new ExperienceException("RequestId đã được dùng cho lần quét khác.", 409);
            return new(old.Id, old.ArtifactId!.Value, old.VisitId, old.RecognizedAt);
        }
        var artifact = await db.Artifacts.SingleOrDefaultAsync(a => a.QrCode == request.QrCode.Trim() && a.IsActive && a.Museum!.IsActive)
            ?? throw new ExperienceException("QR không hợp lệ hoặc hiện vật không khả dụng.", 404);
        var now = clock.GetUtcNow().UtcDateTime;
        var effectiveVisitId = request.VisitId ?? await db.Visits
            .Where(v => v.VisitorId == user && v.MuseumId == artifact.MuseumId && v.Status == VisitStatus.InProgress)
            .OrderByDescending(v => v.StartedAt)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync();

        var isTourStop = false;
        if (effectiveVisitId is int vid)
        {
            await progress.RecordQrVisitAsync(user, vid, artifact.Id, now);
            var activeVisit = await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vid);
            if (activeVisit is not null)
            {
                if (activeVisit.TourVersionId is int tvId)
                {
                    isTourStop = await db.Set<TourVersionItem>().AnyAsync(x => x.TourVersionId == tvId && x.ArtifactId == artifact.Id);
                }
                else if (activeVisit.TourId is int tId)
                {
                    isTourStop = await db.Set<TourItem>().AnyAsync(x => x.TourId == tId && x.ArtifactId == artifact.Id);
                }
                else if (activeVisit.PersonalizedTourId is not null)
                {
                    isTourStop = true;
                }
            }
        }

        var history = new RecognitionHistory { UserId = user, VisitId = effectiveVisitId, ArtifactId = artifact.Id, RecognizedAt = now,
            Method = request.Method ?? "qr", ConfirmedQrScan = request.Method == "qr",
            IsSuccess = true, RequestId = request.RequestId };
        db.Add(history); await db.SaveChangesAsync();
        await daily.UnlockAsync(history);
        var points = isTourStop ? await rewards.AwardScanPointsAsync(user, artifact.Id) : 0;
        await db.SaveChangesAsync(); var badges = await rewards.EvaluateBadgesAsync(user); await db.SaveChangesAsync(); await tx.CommitAsync();
        return new(history.Id, artifact.Id, history.VisitId, now) { PointsAwarded = points, NewBadges = badges.Select(b => new EarnedBadge(b.Id, b.Name)).ToList() };
    }
    public async Task<IReadOnlyList<RecognitionHistory>> GetHistoryAsync(Guid user, int page, int pageSize)
    {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<RecognitionHistory>().AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync();
    }
}
