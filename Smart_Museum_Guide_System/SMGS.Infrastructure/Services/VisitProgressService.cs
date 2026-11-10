using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.Visit;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

/// <summary>
/// Quản lý bắt đầu lượt tham quan (Visit), ghi nhận quét QR điểm dừng và hoàn thành tour (WF-02: F06).
/// Khi khách bắt đầu tham quan theo tour, hệ thống gắn cố định <see cref="Visit.TourVersionId"/>
/// vào phiên bản công khai hiện tại (hoặc phiên bản của SavedRoute).
/// Việc xuất bản phiên bản mới (v2) tuyệt đối không làm thay đổi tiến độ hay lịch sử của lượt tham quan ở v1.
/// </summary>
public class VisitProgressService(ApplicationDbContext db, TimeProvider clock) : IVisitProgressService
{
    public async Task<VisitResponseDto> StartVisitAsync(Guid user, StartVisitRequestDto request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await ExperienceAccess.LockUser(db, user);

        if (!await db.Museums.AnyAsync(m => m.Id == request.MuseumId && m.IsActive))
        {
            throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        }

        if (await db.Visits.AnyAsync(v => v.VisitorId == user && v.MuseumId == request.MuseumId && v.Status == VisitStatus.InProgress))
        {
            throw new ExperienceException("Bạn đang có lượt tham quan tại bảo tàng này.", 409);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        int? resolvedTourId = request.TourId;
        int? pinnedVersionId = null;
        int? pinnedVersionNo = null;

        if (request.SavedRouteId is int savedRouteId)
        {
            var saved = await db.Set<SavedRoute>()
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == savedRouteId && r.VisitorId == user)
                ?? throw new ExperienceException("Không tìm thấy lộ trình đã lưu.", 404);

            if (saved.MuseumId != request.MuseumId || (request.TourId.HasValue && request.TourId.Value != saved.TourId))
            {
                throw new ExperienceException("Lộ trình đã lưu không thuộc bảo tàng hoặc tour đã chọn.", 400);
            }

            var savedVersion = await db.Set<TourVersion>()
                .FromSqlInterpolated($"SELECT * FROM tour_versions WHERE \"Id\" = {saved.TourVersionId} FOR UPDATE")
                .SingleOrDefaultAsync()
                ?? throw new ExperienceException("Phiên bản tour đã lưu không tồn tại.", 404);

            if (!savedVersion.IsPublicAt(now))
            {
                throw new ExperienceException("Phiên bản tour đã lưu hiện không còn ở trạng thái công khai.", 400);
            }

            await ValidateActiveArtifactsForVersionAsync(request.MuseumId, savedVersion.Id);
            resolvedTourId = saved.TourId;
            pinnedVersionId = savedVersion.Id;
            pinnedVersionNo = savedVersion.VersionNo;
        }
        else if (request.TourId is int tourId)
        {
            var tour = await db.Set<Tour>()
                .FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {tourId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (tour is null || tour.MuseumId != request.MuseumId)
            {
                throw new ExperienceException("Tour chưa xuất bản hoặc không thuộc bảo tàng.", 400);
            }

            // Tìm phiên bản công khai mới nhất (Approved và PublicAt <= now).
            var publicVersion = await db.Set<TourVersion>()
                .Where(v => v.TourId == tourId && v.Status == TourVersionStatuses.Approved && v.PublicAt <= now)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefaultAsync();

            if (publicVersion is not null)
            {
                await ValidateActiveArtifactsForVersionAsync(request.MuseumId, publicVersion.Id);
                pinnedVersionId = publicVersion.Id;
                pinnedVersionNo = publicVersion.VersionNo;
            }
            else if (tour.Status == "published")
            {
                // Tương thích ngược với dữ liệu cũ chỉ có bảng Tour/TourItem.
                var ids = await db.Set<TourItem>().Where(i => i.TourId == tourId).Select(i => i.ArtifactId).ToListAsync();
                if (ids.Count == 0 || await db.Artifacts.CountAsync(a => ids.Contains(a.Id) && a.IsActive && a.MuseumId == request.MuseumId) != ids.Count)
                {
                    throw new ExperienceException("Tour có hiện vật không còn khả dụng.", 409);
                }
            }
            else
            {
                throw new ExperienceException("Tour chưa xuất bản hoặc không thuộc bảo tàng.", 400);
            }
        }

        var visit = new Visit
        {
            VisitorId = user,
            MuseumId = request.MuseumId,
            TourId = resolvedTourId,
            TourVersionId = pinnedVersionId,
            Status = VisitStatus.InProgress,
            StartedAt = now
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Map(visit, pinnedVersionNo);
    }

    // Called by RecognitionService inside its user-locked transaction; never exposed as a client-supplied visit mark.
    public async Task RecordQrVisitAsync(Guid user, int visitId, int artifactId, DateTime at)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("QR progress requires the recognition transaction.");
        }

        var visit = await ExperienceAccess.Visit(db, visitId, user, true);

        if (!await db.Artifacts.AnyAsync(a => a.Id == artifactId && a.MuseumId == visit.MuseumId && a.IsActive))
        {
            throw new ExperienceException("Hiện vật không thuộc lượt tham quan.");
        }

        var item = await db.Set<VisitItem>().SingleOrDefaultAsync(x => x.VisitId == visitId && x.ArtifactId == artifactId);
        if (item is null)
        {
            var versionItem = visit.TourVersionId is int vid
                ? await db.Set<TourVersionItem>().SingleOrDefaultAsync(x => x.TourVersionId == vid && x.ArtifactId == artifactId)
                : null;

            var tourItem = visit.TourVersionId is null && visit.TourId is int tid
                ? await db.Set<TourItem>().SingleOrDefaultAsync(x => x.TourId == tid && x.ArtifactId == artifactId)
                : null;

            item = new VisitItem
            {
                VisitId = visitId,
                ArtifactId = artifactId,
                TourItemId = tourItem?.Id,
                TourVersionItemId = versionItem?.Id
            };
            db.Add(item);
        }

        item.LastVisitedAt = at;
    }

    public async Task<VisitProgress> GetProgressAsync(Guid user, int visitId)
    {
        var visit = await ExperienceAccess.Visit(db, visitId, user);
        var visited = await db.Set<VisitItem>().Where(x => x.VisitId == visitId).ToListAsync();
        List<ProgressStop> stops;
        int? versionNo = null;

        if (visit.TourVersionId is int vid)
        {
            versionNo = await db.Set<TourVersion>()
                .AsNoTracking()
                .Where(v => v.Id == vid)
                .Select(v => (int?)v.VersionNo)
                .SingleOrDefaultAsync();

            var items = await db.Set<TourVersionItem>()
                .AsNoTracking()
                .Where(x => x.TourVersionId == vid)
                .OrderBy(x => x.OrderNo)
                .ToListAsync();

            stops = items
                .Select(i => new ProgressStop(
                    i.ArtifactId,
                    i.OrderNo,
                    visited.SingleOrDefault(v => v.ArtifactId == i.ArtifactId)?.LastVisitedAt))
                .ToList();
        }
        else if (visit.TourId is int tid)
        {
            var items = await db.Set<TourItem>()
                .AsNoTracking()
                .Where(x => x.TourId == tid)
                .OrderBy(x => x.OrderNo)
                .ToListAsync();

            stops = items
                .Select(i => new ProgressStop(
                    i.ArtifactId,
                    i.OrderNo,
                    visited.SingleOrDefault(v => v.ArtifactId == i.ArtifactId)?.LastVisitedAt))
                .ToList();
        }
        else
        {
            stops = visited
                .OrderBy(v => v.LastVisitedAt)
                .Select((v, i) => new ProgressStop(v.ArtifactId, i + 1, v.LastVisitedAt))
                .ToList();
        }

        var nextStop = visit.TourId is null && visit.TourVersionId is null
            ? null
            : stops.FirstOrDefault(x => x.LastVisitedAt is null)?.ArtifactId;

        return new VisitProgress(
            visitId,
            visit.TourId,
            visit.Status,
            stops,
            nextStop,
            visit.TourVersionId,
            versionNo);
    }

    public async Task<VisitResponseDto> CompleteTourAsync(Guid user, int visitId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await ExperienceAccess.LockUser(db, user);
        var visit = await ExperienceAccess.Visit(db, visitId, user);

        if (visit.Status != VisitStatus.InProgress)
        {
            throw new ExperienceException("Lượt tham quan không ở trạng thái đang diễn ra.");
        }

        int? versionNo = null;
        if (visit.TourVersionId is int vid)
        {
            versionNo = await db.Set<TourVersion>()
                .AsNoTracking()
                .Where(v => v.Id == vid)
                .Select(v => (int?)v.VersionNo)
                .SingleOrDefaultAsync();

            var hasUnvisitedStop = await db.Set<TourVersionItem>()
                .AnyAsync(i => i.TourVersionId == vid &&
                               !db.Set<VisitItem>().Any(v => v.VisitId == visitId && v.ArtifactId == i.ArtifactId));

            if (hasUnvisitedStop)
            {
                throw new ExperienceException("Hãy quét đủ điểm dừng trước khi hoàn thành tour.", 409);
            }
        }
        else if (visit.TourId is int tid)
        {
            var hasUnvisitedStop = await db.Set<TourItem>()
                .AnyAsync(i => i.TourId == tid &&
                               !db.Set<VisitItem>().Any(v => v.VisitId == visitId && v.ArtifactId == i.ArtifactId));

            if (hasUnvisitedStop)
            {
                throw new ExperienceException("Hãy quét đủ điểm dừng trước khi hoàn thành tour.", 409);
            }
        }

        visit.Status = VisitStatus.Completed;
        visit.CompletedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Map(visit, versionNo);
    }

    private async Task ValidateActiveArtifactsForVersionAsync(int museumId, int tourVersionId)
    {
        var ids = await db.Set<TourVersionItem>()
            .Where(i => i.TourVersionId == tourVersionId)
            .Select(i => i.ArtifactId)
            .ToListAsync();

        var activeCount = await db.Artifacts.CountAsync(
            a => ids.Contains(a.Id) && a.IsActive && a.MuseumId == museumId);

        if (ids.Count == 0 || activeCount != ids.Count)
        {
            throw new ExperienceException("Tour có hiện vật không còn khả dụng.", 409);
        }
    }

    private static VisitResponseDto Map(Visit v, int? versionNo = null) => new()
    {
        Id = v.Id,
        VisitorId = v.VisitorId,
        MuseumId = v.MuseumId,
        TourId = v.TourId,
        TourVersionId = v.TourVersionId,
        TourVersionNo = versionNo,
        Status = v.Status,
        StartedAt = v.StartedAt,
        CompletedAt = v.CompletedAt,
        HasFeedback = v.Feedback is not null
    };
}
