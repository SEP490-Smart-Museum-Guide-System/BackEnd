using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

/// <summary>
/// Dịch vụ quản lý Tour, phiên bản TourVersion (F04) và gợi ý lộ trình theo sở thích + thời gian (F05).
/// Giữ tương thích ngược với các lời gọi cũ trong hệ thống đồng thời thực thi đầy đủ quy trình kiểm duyệt phiên bản.
/// </summary>
public class TourService(ApplicationDbContext db, TimeProvider? clock = null) : ITourService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    private async Task Validate(int museumId, TourRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) ||
            r.Title.Length > 200 ||
            r.Description is null ||
            r.Description.Length > 4000 ||
            r.EstimatedMinutes is < 1 or > 1440 ||
            r.ArtifactIds is null ||
            r.ArtifactIds.Length is < 1 or > 100 ||
            r.ArtifactIds.Distinct().Count() != r.ArtifactIds.Length)
        {
            throw new ExperienceException("Tên, thời lượng hoặc danh sách điểm dừng không hợp lệ.");
        }

        var validCount = await db.Artifacts.CountAsync(
            a => r.ArtifactIds.Contains(a.Id) && a.MuseumId == museumId && a.IsActive);

        if (validCount != r.ArtifactIds.Length)
        {
            throw new ExperienceException("Các hiện vật phải đang hoạt động và thuộc cùng bảo tàng.");
        }
    }

    public async Task<Tour> CreateAsync(int museumId, TourRequest request, Guid user, bool admin)
    {
        await ExperienceAccess.Museum(db, museumId, user, admin);
        await Validate(museumId, request);

        var now = UtcNow;
        var title = request.Title.Trim();
        var description = request.Description.Trim();

        var tour = new Tour
        {
            MuseumId = museumId,
            CreatedBy = user,
            Title = title,
            Description = description,
            EstimatedMinutes = request.EstimatedMinutes,
            Status = TourVersionStatuses.Draft,
            CreatedAt = now,
            Items = request.ArtifactIds
                .Select((id, i) => new TourItem { ArtifactId = id, OrderNo = i + 1 })
                .ToList(),
            Versions =
            [
                new TourVersion
                {
                    VersionNo = 1,
                    Title = title,
                    Description = description,
                    EstimatedMinutes = request.EstimatedMinutes,
                    Status = TourVersionStatuses.Draft,
                    CreatedBy = user,
                    CreatedAt = now,
                    Items = request.ArtifactIds
                        .Select((id, i) => new TourVersionItem { ArtifactId = id, OrderNo = i + 1 })
                        .ToList()
                }
            ]
        };

        db.Add(tour);
        await db.SaveChangesAsync();
        return tour;
    }

    public async Task<Tour> UpdateStopsAsync(int id, UpdateTourRequest request, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var tour = await db.Set<Tour>()
            .FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);

        if (tour.Revision != request.Revision)
        {
            throw new ExperienceException("Tour đã thay đổi, hãy tải lại.", 409);
        }

        if (await db.Visits.AnyAsync(v => v.TourId == id))
        {
            throw new ExperienceException("Tour đã có lượt tham quan; tạo phiên bản mới để giữ nguyên lịch sử.", 409);
        }

        await Validate(tour.MuseumId, request.Tour);
        await db.Entry(tour).Collection(x => x.Items).LoadAsync();
        db.RemoveRange(tour.Items);
        await db.SaveChangesAsync();

        var title = request.Tour.Title.Trim();
        var description = request.Tour.Description.Trim();

        tour.Items = request.Tour.ArtifactIds
            .Select((a, i) => new TourItem { ArtifactId = a, OrderNo = i + 1 })
            .ToList();
        tour.Title = title;
        tour.Description = description;
        tour.EstimatedMinutes = request.Tour.EstimatedMinutes;
        tour.Status = TourVersionStatuses.Draft;
        tour.Revision++;

        var latestDraft = await db.Set<TourVersion>()
            .Include(v => v.Items)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(v => v.TourId == id && v.Status == TourVersionStatuses.Draft);

        if (latestDraft is not null)
        {
            db.RemoveRange(latestDraft.Items);
            await db.SaveChangesAsync();
            latestDraft.Title = title;
            latestDraft.Description = description;
            latestDraft.EstimatedMinutes = request.Tour.EstimatedMinutes;
            latestDraft.Items = request.Tour.ArtifactIds
                .Select((a, i) => new TourVersionItem { ArtifactId = a, OrderNo = i + 1 })
                .ToList();
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return tour;
    }

    public async Task<Tour> PublishAsync(int id, int revision, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var tour = await db.Set<Tour>()
            .FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);

        if (tour.Revision != revision)
        {
            throw new ExperienceException("Tour đã thay đổi, hãy tải lại.", 409);
        }

        await db.Entry(tour).Collection(x => x.Items).LoadAsync();
        var artifactIds = tour.Items.OrderBy(x => x.OrderNo).Select(x => x.ArtifactId).ToArray();
        await Validate(tour.MuseumId, new(tour.Title, tour.Description, tour.EstimatedMinutes, artifactIds));

        var now = UtcNow;
        var version = await db.Set<TourVersion>()
            .Include(v => v.Items)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(v => v.TourId == id);

        if (version is null)
        {
            version = new TourVersion
            {
                TourId = id,
                VersionNo = 1,
                Title = tour.Title,
                Description = tour.Description,
                EstimatedMinutes = tour.EstimatedMinutes,
                Status = TourVersionStatuses.Approved,
                PublicAt = now,
                CreatedBy = tour.CreatedBy,
                CreatedAt = now,
                SubmittedBy = user,
                SubmittedAt = now,
                ReviewedBy = user,
                ReviewedAt = now,
                Items = artifactIds
                    .Select((aid, idx) => new TourVersionItem { ArtifactId = aid, OrderNo = idx + 1 })
                    .ToList()
            };
            db.Add(version);
        }
        else if (version.Status != TourVersionStatuses.Approved || version.PublicAt > now)
        {
            version.Status = TourVersionStatuses.Approved;
            version.PublicAt = now;
            version.SubmittedBy ??= user;
            version.SubmittedAt ??= now;
            version.ReviewedBy = user;
            version.ReviewedAt = now;
        }

        tour.Status = "published";
        tour.Revision++;

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return tour;
    }

    public async Task<Tour> GetAsync(int id, Guid? user, bool admin)
    {
        var tour = await db.Set<Tour>()
            .AsNoTracking()
            .Include(x => x.Items.OrderBy(i => i.OrderNo))
            .Include(x => x.Versions.OrderByDescending(v => v.VersionNo))
                .ThenInclude(v => v.Items.OrderBy(i => i.OrderNo))
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        if (!await db.Museums.AnyAsync(x => x.Id == tour.MuseumId && x.IsActive))
        {
            throw new ExperienceException("Không tìm thấy tour.", 404);
        }

        var now = UtcNow;
        var publicVersion = tour.Versions
            .Where(v => v.IsPublicAt(now))
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefault();

        if (publicVersion is null && tour.Status != "published")
        {
            if (user is null)
            {
                throw new ExperienceException("Không tìm thấy tour.", 404);
            }

            await ExperienceAccess.Museum(db, tour.MuseumId, user.Value, admin);
            return tour;
        }

        if (publicVersion is not null && user is null)
        {
            ProjectPublicVersionOntoTour(tour, publicVersion);
        }

        return tour;
    }

    public async Task<IReadOnlyList<Tour>> ListAsync(int museumId, bool drafts, Guid? user, bool admin, int page, int pageSize)
    {
        if (drafts)
        {
            await ExperienceAccess.Museum(
                db,
                museumId,
                user ?? throw new ExperienceException("Vui lòng đăng nhập.", 401),
                admin);
        }

        if (!await db.Museums.AnyAsync(x => x.Id == museumId && x.IsActive))
        {
            throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        }

        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        var now = UtcNow;

        if (drafts)
        {
            return await db.Set<Tour>()
                .AsNoTracking()
                .Where(x => x.MuseumId == museumId)
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(take)
                .Include(x => x.Items.OrderBy(i => i.OrderNo))
                .Include(x => x.Versions.OrderByDescending(v => v.VersionNo))
                    .ThenInclude(v => v.Items.OrderBy(i => i.OrderNo))
                .ToListAsync();
        }

        var tours = await db.Set<Tour>()
            .AsNoTracking()
            .Where(x => x.MuseumId == museumId &&
                        (x.Status == "published" ||
                         x.Versions.Any(v => v.Status == TourVersionStatuses.Approved && v.PublicAt <= now)))
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Include(x => x.Items.OrderBy(i => i.OrderNo))
            .Include(x => x.Versions.OrderByDescending(v => v.VersionNo))
                .ThenInclude(v => v.Items.OrderBy(i => i.OrderNo))
            .ToListAsync();

        foreach (var tour in tours)
        {
            var pub = tour.Versions
                .Where(v => v.IsPublicAt(now))
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefault();

            if (pub is not null)
            {
                ProjectPublicVersionOntoTour(tour, pub);
            }
        }

        return tours;
    }

    // =========================================================================
    // WF-02 (F04): Quản lý vòng đời TourVersion (Draft -> Submitted -> Approved/Rejected)
    // =========================================================================

    public async Task<TourVersion> CreateVersionDraftAsync(
        int tourId,
        CreateTourVersionRequest request,
        Guid user,
        bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var tour = await db.Set<Tour>()
            .FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {tourId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);

        TourRequest content;
        if (request.Content is not null)
        {
            content = request.Content;
        }
        else
        {
            var sourceQuery = db.Set<TourVersion>()
                .AsNoTracking()
                .Include(v => v.Items.OrderBy(i => i.OrderNo))
                .Where(v => v.TourId == tourId);

            var source = request.CopyFromVersionId.HasValue
                ? await sourceQuery.SingleOrDefaultAsync(v => v.Id == request.CopyFromVersionId.Value)
                : await sourceQuery.OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync();

            if (source is null)
            {
                throw new ExperienceException("Không tìm thấy phiên bản nguồn để sao chép.", 404);
            }

            content = new TourRequest(
                source.Title,
                source.Description,
                source.EstimatedMinutes,
                source.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray());
        }

        await Validate(tour.MuseumId, content);

        var maxVersionNo = await db.Set<TourVersion>()
            .Where(v => v.TourId == tourId)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync() ?? 0;

        var version = new TourVersion
        {
            TourId = tourId,
            VersionNo = maxVersionNo + 1,
            Title = content.Title.Trim(),
            Description = content.Description.Trim(),
            EstimatedMinutes = content.EstimatedMinutes,
            Status = TourVersionStatuses.Draft,
            CreatedBy = user,
            CreatedAt = UtcNow,
            Items = content.ArtifactIds
                .Select((aid, idx) => new TourVersionItem { ArtifactId = aid, OrderNo = idx + 1 })
                .ToList()
        };

        db.Add(version);
        tour.Revision++;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return version;
    }

    public async Task<TourVersion> UpdateVersionDraftAsync(
        int versionId,
        TourRequest request,
        Guid user,
        bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var version = await db.Set<TourVersion>()
            .FromSqlInterpolated($"SELECT * FROM tour_versions WHERE \"Id\" = {versionId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour.", 404);

        var tour = await db.Set<Tour>().SingleAsync(t => t.Id == version.TourId);
        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);

        if (version.Status != TourVersionStatuses.Draft)
        {
            throw new ExperienceException("Phiên bản đã gửi duyệt hoặc đã quyết định là bất biến, không thể chỉnh sửa.", 409);
        }

        await Validate(tour.MuseumId, request);
        await db.Entry(version).Collection(v => v.Items).LoadAsync();
        db.RemoveRange(version.Items);
        await db.SaveChangesAsync();

        version.Title = request.Title.Trim();
        version.Description = request.Description.Trim();
        version.EstimatedMinutes = request.EstimatedMinutes;
        version.Items = request.ArtifactIds
            .Select((aid, idx) => new TourVersionItem { ArtifactId = aid, OrderNo = idx + 1 })
            .ToList();

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return version;
    }

    public async Task<TourVersion> SubmitVersionAsync(int versionId, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var version = await db.Set<TourVersion>()
            .FromSqlInterpolated($"SELECT * FROM tour_versions WHERE \"Id\" = {versionId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour.", 404);

        var tour = await db.Set<Tour>().SingleAsync(t => t.Id == version.TourId);
        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);
        await db.Entry(version).Collection(v => v.Items).LoadAsync();

        var artifactIds = version.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();
        await Validate(tour.MuseumId, new(version.Title, version.Description, version.EstimatedMinutes, artifactIds));

        try
        {
            version.Submit(user, UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new ExperienceException(ex.Message, 409);
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return version;
    }

    public async Task<TourVersion> ApproveVersionAsync(
        int versionId,
        ApproveTourVersionRequest request,
        Guid curator,
        bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var version = await db.Set<TourVersion>()
            .FromSqlInterpolated($"SELECT * FROM tour_versions WHERE \"Id\" = {versionId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour.", 404);

        var tour = await db.Set<Tour>()
            .FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {version.TourId} FOR UPDATE")
            .SingleAsync();

        await ExperienceAccess.Museum(db, tour.MuseumId, curator, admin);
        await db.Entry(version).Collection(v => v.Items).LoadAsync();

        var artifactIds = version.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();
        await Validate(tour.MuseumId, new(version.Title, version.Description, version.EstimatedMinutes, artifactIds));

        var now = UtcNow;
        var publicAt = NormalizeUtc(request.PublicAt ?? now);

        try
        {
            version.Approve(curator, publicAt, request.Note, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ExperienceException(ex.Message, 409);
        }

        // Nếu thời điểm công bố đã đến, đồng bộ thông tin tóm tắt lên bảng Tour để tương thích ngược.
        if (publicAt <= now)
        {
            await SyncTourSummaryFromVersionAsync(tour, version);
        }

        tour.Revision++;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return version;
    }

    public async Task<TourVersion> RejectVersionAsync(
        int versionId,
        RejectTourVersionRequest request,
        Guid curator,
        bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var version = await db.Set<TourVersion>()
            .FromSqlInterpolated($"SELECT * FROM tour_versions WHERE \"Id\" = {versionId} FOR UPDATE")
            .SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour.", 404);

        var tour = await db.Set<Tour>().SingleAsync(t => t.Id == version.TourId);
        await ExperienceAccess.Museum(db, tour.MuseumId, curator, admin);
        await db.Entry(version).Collection(v => v.Items).LoadAsync();

        try
        {
            version.Reject(curator, request.Note, UtcNow);
        }
        catch (ArgumentException ex)
        {
            throw new ExperienceException(ex.Message, 400);
        }
        catch (InvalidOperationException ex)
        {
            throw new ExperienceException(ex.Message, 409);
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return version;
    }

    public async Task<TourVersion> GetPublicTourVersionAsync(int tourId)
    {
        var tour = await db.Set<Tour>()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tourId)
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        if (!await db.Museums.AnyAsync(m => m.Id == tour.MuseumId && m.IsActive))
        {
            throw new ExperienceException("Không tìm thấy tour.", 404);
        }

        var now = UtcNow;
        var version = await db.Set<TourVersion>()
            .AsNoTracking()
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .Where(v => v.TourId == tourId && v.Status == TourVersionStatuses.Approved && v.PublicAt <= now)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy phiên bản công khai cho tour này.", 404);

        return version;
    }

    public async Task<IReadOnlyList<TourVersion>> ListVersionsAsync(int tourId, Guid user, bool admin)
    {
        var tour = await db.Set<Tour>()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tourId)
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);

        return await db.Set<TourVersion>()
            .AsNoTracking()
            .Where(v => v.TourId == tourId)
            .OrderByDescending(v => v.VersionNo)
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .ToListAsync();
    }

    // =========================================================================
    // WF-02 (F05): Gợi ý lộ trình theo sở thích + thời gian & lưu lộ trình (SavedRoute)
    // =========================================================================

    public async Task<TourRecommendationResultDto> RecommendAsync(
        int museumId,
        TourRecommendRequest request,
        Guid? visitor)
    {
        if (!await db.Museums.AnyAsync(m => m.Id == museumId && m.IsActive))
        {
            throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        }

        var now = UtcNow;

        // Lấy tất cả phiên bản đã duyệt & tới giờ công bố của bảo tàng, sau đó chọn bản VersionNo lớn nhất cho mỗi TourId.
        var publicVersions = await db.Set<TourVersion>()
            .AsNoTracking()
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .Where(v => v.Status == TourVersionStatuses.Approved &&
                        v.PublicAt <= now &&
                        db.Set<Tour>().Any(t => t.Id == v.TourId && t.MuseumId == museumId))
            .ToListAsync();

        var latestByTour = publicVersions
            .GroupBy(v => v.TourId)
            .Select(g => g.OrderByDescending(v => v.VersionNo).First())
            .OrderBy(v => v.TourId)
            .ToList();

        if (latestByTour.Count == 0)
        {
            return TourRecommender.Recommend([], request.TopicIds ?? [], request.AvailableMinutes);
        }

        var allArtifactIds = latestByTour
            .SelectMany(v => v.Items.Select(i => i.ArtifactId))
            .Distinct()
            .ToArray();

        // Chỉ giữ các phiên bản mà toàn bộ hiện vật vẫn đang hoạt động.
        var activeArtifactIds = (await db.Artifacts
            .AsNoTracking()
            .Where(a => allArtifactIds.Contains(a.Id) && a.MuseumId == museumId && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync())
            .ToHashSet();

        var validVersions = latestByTour
            .Where(v => v.Items.Count > 0 && v.Items.All(i => activeArtifactIds.Contains(i.ArtifactId)))
            .ToList();

        if (validVersions.Count == 0)
        {
            return TourRecommender.Recommend([], request.TopicIds ?? [], request.AvailableMinutes);
        }

        var artifactTopics = await db.ArtifactTopics
            .AsNoTracking()
            .Where(at => allArtifactIds.Contains(at.ArtifactId) && at.Topic.IsActive && at.Topic.MuseumId == museumId)
            .Select(at => new { at.ArtifactId, at.TopicId, TopicName = at.Topic.Name })
            .ToListAsync();

        var topicsByArtifact = artifactTopics
            .GroupBy(x => x.ArtifactId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TopicId).Distinct().ToArray());

        var topicNames = artifactTopics
            .GroupBy(x => x.TopicId)
            .ToDictionary(g => g.Key, g => g.First().TopicName);

        var tourIds = validVersions.Select(v => v.TourId).ToArray();

        var completedCounts = await db.Visits
            .AsNoTracking()
            .Where(v => v.TourId != null && tourIds.Contains(v.TourId.Value) && v.Status == VisitStatus.Completed)
            .GroupBy(v => v.TourId!.Value)
            .Select(g => new { TourId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TourId, x => x.Count);

        var visitorCompletedTours = visitor.HasValue
            ? (await db.Visits
                .AsNoTracking()
                .Where(v => v.VisitorId == visitor.Value &&
                            v.TourId != null &&
                            tourIds.Contains(v.TourId.Value) &&
                            v.Status == VisitStatus.Completed)
                .Select(v => v.TourId!.Value)
                .Distinct()
                .ToListAsync())
                .ToHashSet()
            : [];

        var candidates = validVersions
            .Select(v =>
            {
                var orderedArtifacts = v.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();
                var tourTopicIds = orderedArtifacts
                    .SelectMany(aid => topicsByArtifact.TryGetValue(aid, out var tids) ? tids : [])
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();

                return new TourRecommender.CandidateTour(
                    TourId: v.TourId,
                    TourVersionId: v.Id,
                    VersionNo: v.VersionNo,
                    Title: v.Title,
                    Description: v.Description,
                    EstimatedMinutes: v.EstimatedMinutes,
                    ArtifactIds: orderedArtifacts,
                    TopicIds: tourTopicIds,
                    TopicNames: topicNames,
                    AlreadyCompletedByVisitor: visitorCompletedTours.Contains(v.TourId),
                    CompletedVisitCount: completedCounts.GetValueOrDefault(v.TourId, 0));
            })
            .ToList();

        return TourRecommender.Recommend(candidates, request.TopicIds ?? [], request.AvailableMinutes);
    }

    public async Task<SavedRouteDto> SaveRouteAsync(Guid visitor, SaveRouteRequest request)
    {
        if (request.AvailableMinutes is < 1 or > 1440)
        {
            throw new ExperienceException("Thời lượng có sẵn không hợp lệ.");
        }

        var now = UtcNow;
        var version = await db.Set<TourVersion>()
            .AsNoTracking()
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .SingleOrDefaultAsync(v => v.Id == request.TourVersionId)
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour.", 404);

        if (!version.IsPublicAt(now))
        {
            throw new ExperienceException("Chỉ có thể lưu phiên bản tour đang công khai.", 400);
        }

        var tour = await db.Set<Tour>()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == version.TourId)
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);

        if (!await db.Museums.AnyAsync(m => m.Id == tour.MuseumId && m.IsActive))
        {
            throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        }

        var distinctTopics = (request.TopicIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        var orderedArtifacts = version.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();

        var artifactTopics = await db.ArtifactTopics
            .AsNoTracking()
            .Where(at => orderedArtifacts.Contains(at.ArtifactId) && at.Topic.IsActive)
            .Select(at => new { at.TopicId, TopicName = at.Topic.Name })
            .ToListAsync();

        var topicNames = artifactTopics
            .GroupBy(x => x.TopicId)
            .ToDictionary(g => g.Key, g => g.First().TopicName);

        var candidate = new TourRecommender.CandidateTour(
            TourId: tour.Id,
            TourVersionId: version.Id,
            VersionNo: version.VersionNo,
            Title: version.Title,
            Description: version.Description,
            EstimatedMinutes: version.EstimatedMinutes,
            ArtifactIds: orderedArtifacts,
            TopicIds: topicNames.Keys.OrderBy(id => id).ToArray(),
            TopicNames: topicNames,
            AlreadyCompletedByVisitor: false,
            CompletedVisitCount: 0);

        var scored = TourRecommender.ScoreCandidate(candidate, distinctTopics, request.AvailableMinutes);

        var saved = new SavedRoute
        {
            VisitorId = visitor,
            MuseumId = tour.MuseumId,
            TourId = tour.Id,
            TourVersionId = version.Id,
            TopicIdsJson = JsonSerializer.Serialize(distinctTopics),
            AvailableMinutes = request.AvailableMinutes,
            Score = scored.TotalScore,
            Explanation = scored.Explanation,
            SavedAt = now
        };

        db.Add(saved);
        await db.SaveChangesAsync();

        return MapSavedRoute(saved, version, orderedArtifacts, distinctTopics);
    }

    public async Task<IReadOnlyList<SavedRouteDto>> ListSavedRoutesAsync(Guid visitor, int? museumId)
    {
        var query = db.Set<SavedRoute>()
            .AsNoTracking()
            .Where(r => r.VisitorId == visitor);

        if (museumId.HasValue)
        {
            query = query.Where(r => r.MuseumId == museumId.Value);
        }

        var routes = await query
            .OrderByDescending(r => r.SavedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        if (routes.Count == 0)
        {
            return [];
        }

        var versionIds = routes.Select(r => r.TourVersionId).Distinct().ToArray();
        var versions = await db.Set<TourVersion>()
            .AsNoTracking()
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .Where(v => versionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        return routes
            .Where(r => versions.ContainsKey(r.TourVersionId))
            .Select(r =>
            {
                var v = versions[r.TourVersionId];
                var artifacts = v.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();
                var topics = DeserializeTopicIds(r.TopicIdsJson);
                return MapSavedRoute(r, v, artifacts, topics);
            })
            .ToList();
    }

    public async Task<SavedRouteDto> GetSavedRouteAsync(Guid visitor, int savedRouteId)
    {
        var route = await db.Set<SavedRoute>()
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == savedRouteId && r.VisitorId == visitor)
            ?? throw new ExperienceException("Không tìm thấy lộ trình đã lưu.", 404);

        var version = await db.Set<TourVersion>()
            .AsNoTracking()
            .Include(v => v.Items.OrderBy(i => i.OrderNo))
            .SingleOrDefaultAsync(v => v.Id == route.TourVersionId)
            ?? throw new ExperienceException("Không tìm thấy phiên bản tour đã lưu.", 404);

        var artifacts = version.Items.OrderBy(i => i.OrderNo).Select(i => i.ArtifactId).ToArray();
        var topics = DeserializeTopicIds(route.TopicIdsJson);
        return MapSavedRoute(route, version, artifacts, topics);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static void ProjectPublicVersionOntoTour(Tour tour, TourVersion publicVersion)
    {
        tour.Title = publicVersion.Title;
        tour.Description = publicVersion.Description;
        tour.EstimatedMinutes = publicVersion.EstimatedMinutes;
        tour.Status = "published";
        tour.Items = publicVersion.Items
            .OrderBy(i => i.OrderNo)
            .Select(i => new TourItem
            {
                Id = i.Id,
                TourId = tour.Id,
                ArtifactId = i.ArtifactId,
                OrderNo = i.OrderNo
            })
            .ToList();
    }

    private async Task SyncTourSummaryFromVersionAsync(Tour tour, TourVersion version)
    {
        tour.Title = version.Title;
        tour.Description = version.Description;
        tour.EstimatedMinutes = version.EstimatedMinutes;
        tour.Status = "published";

        // Nếu đã có lượt tham quan cũ gắn vào tour_items thì không xóa tour_items để bảo toàn khóa ngoại lịch sử.
        if (await db.Visits.AnyAsync(v => v.TourId == tour.Id))
        {
            return;
        }

        await db.Entry(tour).Collection(x => x.Items).LoadAsync();
        db.RemoveRange(tour.Items);
        await db.SaveChangesAsync();

        tour.Items = version.Items
            .OrderBy(i => i.OrderNo)
            .Select(i => new TourItem
            {
                TourId = tour.Id,
                ArtifactId = i.ArtifactId,
                OrderNo = i.OrderNo
            })
            .ToList();
    }

    private static int[] DeserializeTopicIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static SavedRouteDto MapSavedRoute(
        SavedRoute route,
        TourVersion version,
        IReadOnlyList<int> artifactIds,
        IReadOnlyList<int> topicIds) =>
        new(
            Id: route.Id,
            MuseumId: route.MuseumId,
            TourId: route.TourId,
            TourVersionId: route.TourVersionId,
            VersionNo: version.VersionNo,
            Title: version.Title,
            Description: version.Description,
            EstimatedMinutes: version.EstimatedMinutes,
            ArtifactIds: artifactIds,
            TopicIds: topicIds,
            AvailableMinutes: route.AvailableMinutes,
            Score: route.Score,
            Explanation: route.Explanation,
            SavedAt: route.SavedAt);
}
