using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.Visit;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class VisitProgressService(ApplicationDbContext db, TimeProvider clock) : IVisitProgressService
{
    public async Task<VisitResponseDto> StartVisitAsync(Guid user, StartVisitRequestDto request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        if (!await db.Museums.AnyAsync(m => m.Id == request.MuseumId && m.IsActive)) throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        if (await db.Visits.AnyAsync(v => v.VisitorId == user && v.MuseumId == request.MuseumId && v.Status == VisitStatus.InProgress))
            throw new ExperienceException("Bạn đang có lượt tham quan tại bảo tàng này.", 409);
        if (request.TourId is int tourId) {
            var tour = await db.Set<Tour>().FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {tourId} FOR UPDATE").SingleOrDefaultAsync();
            if (tour is null || tour.MuseumId != request.MuseumId || tour.Status != "published") throw new ExperienceException("Tour chưa xuất bản hoặc không thuộc bảo tàng.", 400);
            var ids = await db.Set<TourItem>().Where(i => i.TourId == tourId).Select(i => i.ArtifactId).ToListAsync();
            if (ids.Count == 0 || await db.Artifacts.CountAsync(a => ids.Contains(a.Id) && a.IsActive && a.MuseumId == request.MuseumId) != ids.Count)
                throw new ExperienceException("Tour có hiện vật không còn khả dụng.", 409);
        }
        var visit = new Visit { VisitorId = user, MuseumId = request.MuseumId, TourId = request.TourId, Status = VisitStatus.InProgress, StartedAt = clock.GetUtcNow().UtcDateTime };
        db.Visits.Add(visit); await db.SaveChangesAsync(); await tx.CommitAsync(); return Map(visit);
    }
    // Called by RecognitionService inside its user-locked transaction; never exposed as a client-supplied visit mark.
    public async Task RecordQrVisitAsync(Guid user, int visitId, int artifactId, DateTime at)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("QR progress requires the recognition transaction.");
        var visit = await ExperienceAccess.Visit(db, visitId, user, true);
        if (!await db.Artifacts.AnyAsync(a => a.Id == artifactId && a.MuseumId == visit.MuseumId && a.IsActive)) throw new ExperienceException("Hiện vật không thuộc lượt tham quan.");
        var item = await db.Set<VisitItem>().SingleOrDefaultAsync(x => x.VisitId == visitId && x.ArtifactId == artifactId);
        if (item is null) {
            var tourItem = visit.TourId is int tid ? await db.Set<TourItem>().SingleOrDefaultAsync(x => x.TourId == tid && x.ArtifactId == artifactId) : null;
            item = new VisitItem { VisitId = visitId, ArtifactId = artifactId, TourItemId = tourItem?.Id }; db.Add(item);
        }
        item.LastVisitedAt = at;
    }
    public async Task<VisitProgress> GetProgressAsync(Guid user, int visitId)
    {
        var visit = await ExperienceAccess.Visit(db, visitId, user);
        var visited = await db.Set<VisitItem>().Where(x => x.VisitId == visitId).ToListAsync();
        List<ProgressStop> stops;
        if (visit.TourId is int tid) {
            var items = await db.Set<TourItem>().Where(x => x.TourId == tid).OrderBy(x => x.OrderNo).ToListAsync();
            stops = items.Select(i => new ProgressStop(i.ArtifactId, i.OrderNo, visited.SingleOrDefault(v => v.ArtifactId == i.ArtifactId)?.LastVisitedAt)).ToList();
        } else stops = visited.OrderBy(v => v.LastVisitedAt).Select((v, i) => new ProgressStop(v.ArtifactId, i + 1, v.LastVisitedAt)).ToList();
        return new(visitId, visit.TourId, visit.Status, stops, visit.TourId is null ? null : stops.FirstOrDefault(x => x.LastVisitedAt is null)?.ArtifactId);
    }
    public async Task<VisitResponseDto> CompleteTourAsync(Guid user, int visitId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var visit = await ExperienceAccess.Visit(db, visitId, user);
        if (visit.Status != VisitStatus.InProgress) throw new ExperienceException("Lượt tham quan không ở trạng thái đang diễn ra.");
        if (visit.TourId is int tid && await db.Set<TourItem>().AnyAsync(i => i.TourId == tid && !db.Set<VisitItem>().Any(v => v.VisitId == visitId && v.ArtifactId == i.ArtifactId)))
            throw new ExperienceException("Hãy quét đủ điểm dừng trước khi hoàn thành tour.", 409);
        visit.Status = VisitStatus.Completed; visit.CompletedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Map(visit);
    }
    private static VisitResponseDto Map(Visit v) => new() { Id = v.Id, VisitorId = v.VisitorId, MuseumId = v.MuseumId, TourId = v.TourId,
        Status = v.Status, StartedAt = v.StartedAt, CompletedAt = v.CompletedAt, HasFeedback = v.Feedback is not null };
}
