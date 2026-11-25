using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class GuideHistoryService(
    ApplicationDbContext db,
    IAccountAdminService? accountAdmin = null,
    TimeProvider? clock = null) : IGuideHistoryService
{
    private DateTime UtcNow => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task RecordExchangeAsync(
        Guid userId,
        int artifactId,
        int museumId,
        string question,
        string answer,
        bool supported,
        GuideSource? source,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || artifactId <= 0 || string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return;

        var entry = new GuideChatMessage
        {
            UserId = userId,
            ArtifactId = artifactId,
            MuseumId = museumId,
            Question = question.Length > 1000 ? question[..1000] : question,
            Answer = answer.Length > 4000 ? answer[..4000] : answer,
            Supported = supported,
            ContentVersionId = supported ? source?.ContentVersionId : null,
            ContentVersionNo = supported ? source?.VersionNo : null,
            ContentTitle = supported && source?.Title is { Length: > 0 } t
                ? (t.Length > 300 ? t[..300] : t)
                : null,
            LanguageCode = source?.LanguageCode is { Length: > 0 } lang
                ? (lang.Length > 10 ? lang[..10] : lang)
                : "vi",
            CreatedAt = UtcNow
        };

        db.GuideChatMessages.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GuideTurn>> GetRecentTurnsAsync(
        Guid userId,
        int artifactId,
        int maxExchanges = 4,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || artifactId <= 0) return [];
        var count = Math.Clamp(maxExchanges, 1, 4);

        var rows = await db.GuideChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.ArtifactId == artifactId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        rows.Reverse();
        var turns = new List<GuideTurn>(rows.Count * 2);
        foreach (var row in rows)
        {
            turns.Add(new GuideTurn("user", row.Question));
            turns.Add(new GuideTurn("assistant", row.Answer));
        }
        return turns;
    }

    public async Task<ResponseDto<IReadOnlyList<GuideChatMessageDto>>> GetArtifactHistoryAsync(
        int artifactId,
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ResponseDto<IReadOnlyList<GuideChatMessageDto>>.Fail("Vui lòng đăng nhập để xem lịch sử hỏi đáp.", 401);
        if (artifactId <= 0)
            return ResponseDto<IReadOnlyList<GuideChatMessageDto>>.Fail("Hiện vật không hợp lệ.", 400);

        var take = Math.Clamp(limit, 1, 200);
        var rows = await (from m in db.GuideChatMessages.AsNoTracking()
                          join a in db.Artifacts.AsNoTracking() on m.ArtifactId equals a.Id into aj
                          from a in aj.DefaultIfEmpty()
                          join mu in db.Museums.AsNoTracking() on m.MuseumId equals mu.Id into muj
                          from mu in muj.DefaultIfEmpty()
                          where m.UserId == userId && m.ArtifactId == artifactId
                          orderby m.CreatedAt descending, m.Id descending
                          select new
                          {
                              Message = m,
                              ArtifactName = a != null ? a.Name : "",
                              MuseumName = mu != null ? mu.Name : ""
                          })
                         .Take(take)
                         .ToListAsync(cancellationToken);

        rows.Reverse();
        var dtos = rows.Select(r => MapDto(r.Message, r.ArtifactName, r.MuseumName)).ToList();
        return ResponseDto<IReadOnlyList<GuideChatMessageDto>>.Success(dtos);
    }

    public async Task<ResponseDto<int>> ClearArtifactHistoryAsync(
        int artifactId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ResponseDto<int>.Fail("Vui lòng đăng nhập.", 401);
        if (artifactId <= 0)
            return ResponseDto<int>.Fail("Hiện vật không hợp lệ.", 400);

        var items = await db.GuideChatMessages
            .Where(m => m.UserId == userId && m.ArtifactId == artifactId)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            db.GuideChatMessages.RemoveRange(items);
            await db.SaveChangesAsync(cancellationToken);
        }

        return ResponseDto<int>.Success(items.Count, $"Đã xóa {items.Count} lượt hỏi đáp của hiện vật.");
    }

    public async Task<ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>> GetMyConversationsAsync(
        Guid userId,
        int? museumId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>.Fail("Vui lòng đăng nhập.", 401);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = db.GuideChatMessages.AsNoTracking().Where(m => m.UserId == userId);
        if (museumId.HasValue)
            baseQuery = baseQuery.Where(m => m.MuseumId == museumId.Value);

        var allMessages = await baseQuery
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        var grouped = allMessages
            .GroupBy(m => m.ArtifactId)
            .Select(g =>
            {
                var latest = g.First();
                return new
                {
                    ArtifactId = g.Key,
                    latest.MuseumId,
                    ExchangeCount = g.Count(),
                    LastQuestion = latest.Question,
                    LastAnswer = latest.Answer,
                    LastSupported = latest.Supported,
                    LastAskedAt = latest.CreatedAt
                };
            })
            .OrderByDescending(g => g.LastAskedAt)
            .ThenBy(g => g.ArtifactId)
            .ToList();

        var totalCount = grouped.Count;
        var pageGroups = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var artifactIds = pageGroups.Select(g => g.ArtifactId).Distinct().ToArray();
        var museumIds = pageGroups.Select(g => g.MuseumId).Distinct().ToArray();

        var artifactNames = artifactIds.Length == 0
            ? new Dictionary<int, string>()
            : await db.Artifacts.AsNoTracking()
                .Where(a => artifactIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var museumNames = museumIds.Length == 0
            ? new Dictionary<int, string>()
            : await db.Museums.AsNoTracking()
                .Where(m => museumIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var items = pageGroups.Select(g => new GuideConversationSummaryDto(
            g.ArtifactId,
            artifactNames.GetValueOrDefault(g.ArtifactId, ""),
            g.MuseumId,
            museumNames.GetValueOrDefault(g.MuseumId, ""),
            g.ExchangeCount,
            g.LastQuestion,
            g.LastAnswer,
            g.LastSupported,
            g.LastAskedAt)).ToList();

        return ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>.Success(new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>> GetMyMessagesFeedAsync(
        Guid userId,
        int? museumId = null,
        int? artifactId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>.Fail("Vui lòng đăng nhập.", 401);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.GuideChatMessages.AsNoTracking().Where(m => m.UserId == userId);
        if (museumId.HasValue)
            query = query.Where(m => m.MuseumId == museumId.Value);
        if (artifactId.HasValue)
            query = query.Where(m => m.ArtifactId == artifactId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await (from m in query
                          join a in db.Artifacts.AsNoTracking() on m.ArtifactId equals a.Id into aj
                          from a in aj.DefaultIfEmpty()
                          join mu in db.Museums.AsNoTracking() on m.MuseumId equals mu.Id into muj
                          from mu in muj.DefaultIfEmpty()
                          orderby m.CreatedAt descending, m.Id descending
                          select new
                          {
                              Message = m,
                              ArtifactName = a != null ? a.Name : "",
                              MuseumName = mu != null ? mu.Name : ""
                          })
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .ToListAsync(cancellationToken);

        var items = rows.Select(r => MapDto(r.Message, r.ArtifactName, r.MuseumName)).ToList();
        return ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>.Success(new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>> GetLogsAsync(
        int? museumId,
        int? artifactId,
        bool? supported,
        int page,
        int pageSize,
        Guid callerId,
        bool isAdminOrSystemStaff,
        CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty)
            return ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Fail("Vui lòng đăng nhập.", 401);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.GuideChatMessages.AsNoTracking().AsQueryable();

        if (!isAdminOrSystemStaff)
        {
            if (artifactId.HasValue && accountAdmin is not null)
            {
                if (!await accountAdmin.CanManageArtifactAsync(callerId, false, artifactId.Value))
                    return ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Fail("Bạn không có quyền xem nhật ký hỏi đáp của hiện vật này.", 403);
            }
            else if (museumId.HasValue && accountAdmin is not null)
            {
                if (!await accountAdmin.CanManageMuseumAsync(callerId, false, museumId.Value))
                    return ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Fail("Bạn không có quyền xem nhật ký hỏi đáp của bảo tàng này.", 403);
            }
            else
            {
                var managedMuseumIds = await db.StaffAssignments.AsNoTracking()
                    .Where(s => s.UserId == callerId && s.GalleryId == null && s.ArtifactId == null)
                    .Select(s => s.MuseumId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var managedArtifactIds = await db.StaffAssignments.AsNoTracking()
                    .Where(s => s.UserId == callerId && s.ArtifactId != null)
                    .Select(s => s.ArtifactId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (managedMuseumIds.Count == 0 && managedArtifactIds.Count == 0)
                    return ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Fail("Bạn chưa được phân công bảo tàng hoặc hiện vật.", 403);

                query = query.Where(m => managedMuseumIds.Contains(m.MuseumId) || managedArtifactIds.Contains(m.ArtifactId));
            }
        }

        if (museumId.HasValue)
            query = query.Where(m => m.MuseumId == museumId.Value);
        if (artifactId.HasValue)
            query = query.Where(m => m.ArtifactId == artifactId.Value);
        if (supported.HasValue)
            query = query.Where(m => m.Supported == supported.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await (from m in query
                          join a in db.Artifacts.AsNoTracking() on m.ArtifactId equals a.Id into aj
                          from a in aj.DefaultIfEmpty()
                          join mu in db.Museums.AsNoTracking() on m.MuseumId equals mu.Id into muj
                          from mu in muj.DefaultIfEmpty()
                          orderby m.CreatedAt descending, m.Id descending
                          select new GuideAdminLogDto(
                              m.Id,
                              m.UserId,
                              m.ArtifactId,
                              a != null ? a.Name : "",
                              m.MuseumId,
                              mu != null ? mu.Name : "",
                              m.Question,
                              m.Answer,
                              m.Supported,
                              m.LanguageCode,
                              m.ContentVersionNo,
                              m.CreatedAt))
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .ToListAsync(cancellationToken);

        return ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Success(new()
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    private static GuideChatMessageDto MapDto(GuideChatMessage m, string artifactName, string museumName)
    {
        IReadOnlyList<GuideSource> sources = m.Supported && m.ContentVersionId.HasValue && m.ContentVersionNo.HasValue
            ? [new GuideSource(m.ContentVersionId.Value, m.ContentVersionNo.Value, m.ContentTitle ?? "", m.LanguageCode)]
            : [];

        return new GuideChatMessageDto(
            m.Id,
            m.ArtifactId,
            artifactName,
            m.MuseumId,
            museumName,
            m.Question,
            m.Answer,
            m.Supported,
            sources,
            m.CreatedAt);
    }
}
