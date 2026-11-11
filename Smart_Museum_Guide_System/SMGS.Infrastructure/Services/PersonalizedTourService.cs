using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class PersonalizedTourService(ApplicationDbContext db, IPersonalizedTourModel model, ICommerceService commerce,
    IMemoryCache cache, TimeProvider clock) : IPersonalizedTourService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private record Preview(Guid Owner, PersonalTourResult Result);
    private static string CacheKey(Guid user) => "personal-tour:" + user;

    public async Task<IReadOnlyList<TourInterest>> Interests(int museumId, CancellationToken ct) =>
        await db.Topics.AsNoTracking().Where(t => t.MuseumId == museumId && t.IsActive && t.Museum!.IsActive)
            .OrderBy(t => t.Name).Select(t => new TourInterest(t.Id, t.Name)).ToListAsync(ct);

    private async Task<List<PersonalTourCandidate>> Candidates(int museumId, Guid user, CancellationToken ct)
    {
        var versions = await db.ArtifactContentVersions.AsNoTracking().Include(v => v.Content).ThenInclude(c => c.Artifact)
            .Where(v => v.IsCurrent && v.Status == ContentStatuses.Published && v.Content.LanguageCode == "vi" &&
                v.Content.Artifact.MuseumId == museumId && v.Content.Artifact.IsActive)
            .OrderBy(v => v.Content.ArtifactId).Take(501).ToListAsync(ct);
        if (versions.Count > 500) throw new ExperienceException("Bộ sưu tập quá lớn cho lần gợi ý này. Vui lòng liên hệ bảo tàng để dùng tour có sẵn.", 422);
        var ids = versions.Select(v => v.Content.ArtifactId).ToArray();
        var topics = await db.ArtifactTopics.AsNoTracking().Where(t => ids.Contains(t.ArtifactId) && t.Topic.IsActive)
            .Select(t => new { t.ArtifactId, t.Topic.Name }).ToListAsync(ct);
        var galleryIds = versions.Select(v => v.Content.Artifact.GalleryId).OfType<int>().Distinct().ToArray();
        var galleries = await db.Galleries.AsNoTracking().Where(g => galleryIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var result = new List<PersonalTourCandidate>();
        foreach (var v in versions) {
            ct.ThrowIfCancellationRequested();
            var a = v.Content.Artifact;
            if (!await commerce.CanReadArtifact(a.Id, user)) continue;
            var gallery = a.GalleryId is int gid ? galleries.GetValueOrDefault(gid) : null;
            var words = Regex.Matches((v.NarrationText ?? v.Description), @"\S+").Count;
            // Estimate only. No pixel-to-meter conversion or fabricated location.
            var minutes = Math.Clamp(Math.Ceiling(words / 110.0) + 3, 4, 12);
            result.Add(new(a.Id, v.Id, museumId, a.Name, v.Title, v.Description[..Math.Min(v.Description.Length, 2000)],
                topics.Where(t => t.ArtifactId == a.Id).Select(t => t.Name).ToArray(), a.GalleryId,
                gallery?.Name ?? "Chưa có thông tin phòng", gallery?.FloorId, minutes));
        }
        return result;
    }

    public async Task<PersonalTourResult> Recommend(PersonalizedTourRequest request, Guid user, CancellationToken ct)
    {
        if (request.AvailableMinutes is < 5 or > 240 || request.MaxStops is < 1 or > 30 || request.Interests is null ||
            request.Interests.Length is < 1 or > 8 || request.Interests.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length is < 2 or > 80))
            throw new ExperienceException("Chọn thời gian từ 5 đến 240 phút và 1–8 sở thích hợp lệ.");
        var museum = await db.Museums.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.MuseumId && m.IsActive, ct)
            ?? throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        var interests = request.Interests.Select(i => i.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var candidates = await Candidates(museum.Id, user, ct);
        var result = candidates.Count == 0 ? new TourModelResult("content-tfidf-v1", 0, false,
            "Chưa có hiện vật có nội dung tiếng Việt đã xuất bản phù hợp quyền truy cập của bạn.", []) :
            await model.Recommend(new(museum.Id, request.AvailableMinutes, interests, request.MaxStops, candidates), ct);
        ValidateModel(result, candidates, request);
        var stops = result.Stops.Select((s, i) => {
            var c = candidates.Single(c => c.ArtifactId == s.ArtifactId);
            return new PersonalTourStop(c.ArtifactId, c.ContentVersionId, c.Name, i + 1, c.GalleryId, c.GalleryName,
                s.VisitMinutes, s.WalkMinutes, s.Reason, s.MatchedInterests);
        }).ToList();
        var preview = new PersonalTourResult(Guid.NewGuid(), museum.Id, museum.Name, interests, request.AvailableMinutes,
            result.EstimatedMinutes, result.EngineVersion, result.UsedFallback, result.Notice, stops, clock.GetUtcNow().UtcDateTime, false);
        // One pending preview per visitor, bounded rather than one entry per request.
        cache.Set(CacheKey(user), new Preview(user, preview), TimeSpan.FromMinutes(15));
        return preview;
    }

    private static void ValidateModel(TourModelResult result, List<PersonalTourCandidate> candidates, PersonalizedTourRequest request)
    {
        static void Invalid() => throw new ExperienceException("Dịch vụ trả về hành trình không hợp lệ.", 502);
        if (result.Stops is null || result.Stops.Any(s => s is null) || result.Stops.Count > request.MaxStops || result.Stops.Select(s => s.ArtifactId).Distinct().Count() != result.Stops.Count ||
            !double.IsFinite(result.EstimatedMinutes) || result.EstimatedMinutes < 0 || result.EstimatedMinutes > request.AvailableMinutes ||
            result.EngineVersion != "content-tfidf-v1" || result.Notice is null || result.Notice.Length > 1000) Invalid();
        PersonalTourCandidate? previous = null;
        double total = 0;
        foreach (var s in result.Stops!) {
            var c = candidates.SingleOrDefault(c => c.ArtifactId == s.ArtifactId);
            if (c is null || c.ContentVersionId != s.ContentVersionId || s.VisitMinutes != c.VisitMinutes ||
                !double.IsFinite(s.WalkMinutes) || !double.IsFinite(s.Score) || s.Score < 0 || s.Reason is null || s.Reason.Length > 500 ||
                s.MatchedInterests is null || s.MatchedInterests.Any(i => !request.Interests.Any(x => x.Trim().Equals(i, StringComparison.OrdinalIgnoreCase)))) Invalid();
            var walk = previous is null ? 2 : previous.GalleryId is not null && previous.GalleryId == c!.GalleryId ? 0.5 :
                previous.FloorId is not null && c!.FloorId is not null && previous.FloorId != c.FloorId ? 4 : 2;
            if (s.WalkMinutes != walk) Invalid();
            total += s.VisitMinutes + s.WalkMinutes;
            previous = c;
        }
        if (Math.Abs(total - result.EstimatedMinutes) > 0.01) Invalid();
    }

    public async Task<PersonalTourResult> Save(Guid recommendationId, Guid user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ExperienceAccess.LockUser(db, user);
        var existing = await db.Set<PersonalizedTour>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == recommendationId && p.OwnerId == user, ct);
        if (existing is not null) return Deserialize(existing);
        if (!cache.TryGetValue<Preview>(CacheKey(user), out var pending) || pending is null || pending.Owner != user || pending.Result.Id != recommendationId)
            throw new ExperienceException("Gợi ý đã hết hạn hoặc đã có gợi ý mới. Hãy tạo lại hành trình.", 409);
        if (pending.Result.Stops.Count == 0) throw new ExperienceException("Chưa có điểm dừng để lưu.", 409);
        await CheckAvailable(pending.Result, user, true, ct);
        var saved = pending.Result with { IsSaved = true };
        db.Add(new PersonalizedTour { Id = saved.Id, OwnerId = user, MuseumId = saved.MuseumId,
            SnapshotJson = JsonSerializer.Serialize(saved, Json), CreatedAt = saved.CreatedAt });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return saved;
    }

    private static PersonalTourResult Deserialize(PersonalizedTour row) =>
        JsonSerializer.Deserialize<PersonalTourResult>(row.SnapshotJson, Json)
            ?? throw new ExperienceException("Không đọc được hành trình đã lưu.", 409);
    public async Task<PersonalTourResult> Get(Guid id, Guid user, CancellationToken ct) => Deserialize(
        await db.Set<PersonalizedTour>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.OwnerId == user, ct)
            ?? throw new ExperienceException("Không tìm thấy hành trình cá nhân.", 404));
    public async Task<IReadOnlyList<PersonalTourResult>> Mine(Guid user, int page, int pageSize, CancellationToken ct)
    {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return (await db.Set<PersonalizedTour>().AsNoTracking().Where(p => p.OwnerId == user).OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(take).ToListAsync(ct)).Select(Deserialize).ToList();
    }
    public async Task<PersonalTourResult> ValidateForStart(Guid id, int museumId, Guid user, CancellationToken ct)
    {
        var saved = await Get(id, user, ct);
        if (saved.MuseumId != museumId) throw new ExperienceException("Hành trình không thuộc bảo tàng này.", 400);
        await CheckAvailable(saved, user, false, ct);
        return saved;
    }
    private async Task CheckAvailable(PersonalTourResult plan, Guid user, bool sameVersion, CancellationToken ct)
    {
        if (!await db.Museums.AnyAsync(m => m.Id == plan.MuseumId && m.IsActive, ct))
            throw new ExperienceException("Bảo tàng không còn khả dụng.", 409);
        foreach (var stop in plan.Stops) {
            var available = await db.ArtifactContentVersions.AnyAsync(v => v.Content.ArtifactId == stop.ArtifactId &&
                v.Content.Artifact.MuseumId == plan.MuseumId && v.Content.Artifact.IsActive && v.Content.LanguageCode == "vi" &&
                v.IsCurrent && v.Status == ContentStatuses.Published && (!sameVersion || v.Id == stop.ContentVersionId), ct);
            if (!available || !await commerce.CanReadArtifact(stop.ArtifactId, user))
                throw new ExperienceException("Một điểm dừng đã thay đổi nội dung hoặc quyền truy cập. Hãy tạo hành trình mới.", 409);
        }
    }
}
