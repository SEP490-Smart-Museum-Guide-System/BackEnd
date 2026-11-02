using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Topic;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class TopicService(ApplicationDbContext db) : ITopicService
{
    private async Task<bool> CanManageMuseumAsync(Guid userId, bool isAdmin, int museumId)
    {
        if (isAdmin) return true;
        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == museumId &&
            a.GalleryId == null &&
            a.ArtifactId == null);
    }

    private async Task<bool> CanManageArtifactAsync(Guid userId, bool isAdmin, Artifact artifact)
    {
        if (isAdmin) return true;
        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == artifact.MuseumId &&
            (
                (a.GalleryId == null && a.ArtifactId == null) ||
                (artifact.GalleryId != null && a.GalleryId == artifact.GalleryId && a.ArtifactId == null) ||
                a.ArtifactId == artifact.Id
            ));
    }

    public async Task<ResponseDto<PaginatedResponseDto<TopicResponseDto>>> GetAllAsync(
        int? museumId, string? keyword, bool? isActive, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Topics.AsNoTracking().AsQueryable();

        if (museumId.HasValue)
            query = query.Where(t => t.MuseumId == museumId.Value);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(kw) ||
                (t.Description != null && t.Description.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TopicResponseDto
            {
                Id = t.Id,
                MuseumId = t.MuseumId,
                Name = t.Name,
                Description = t.Description,
                IsActive = t.IsActive,
                ArtifactCount = t.ArtifactTopics.Count(),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return ResponseDto<PaginatedResponseDto<TopicResponseDto>>.Success(new PaginatedResponseDto<TopicResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ResponseDto<TopicResponseDto>> GetByIdAsync(int id)
    {
        var topic = await db.Topics.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TopicResponseDto
            {
                Id = t.Id,
                MuseumId = t.MuseumId,
                Name = t.Name,
                Description = t.Description,
                IsActive = t.IsActive,
                ArtifactCount = t.ArtifactTopics.Count(),
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();

        return topic is null
            ? ResponseDto<TopicResponseDto>.Fail("Không tìm thấy chủ đề.", 404)
            : ResponseDto<TopicResponseDto>.Success(topic);
    }

    public async Task<ResponseDto<TopicResponseDto>> CreateAsync(CreateTopicRequestDto dto, Guid userId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ResponseDto<TopicResponseDto>.Fail("Tên chủ đề không được để trống.", 400);

        if (!await db.Museums.AnyAsync(m => m.Id == dto.MuseumId))
            return ResponseDto<TopicResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);

        if (!await CanManageMuseumAsync(userId, isAdmin, dto.MuseumId))
            return ResponseDto<TopicResponseDto>.Fail("Bạn không được phân công quản lý bảo tàng này.", 403);

        var name = dto.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.MuseumId == dto.MuseumId && t.Name.ToLower() == name.ToLower()))
            return ResponseDto<TopicResponseDto>.Fail("Chủ đề đã tồn tại trong bảo tàng này.", 409);

        var topic = new Topic
        {
            MuseumId = dto.MuseumId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        return ResponseDto<TopicResponseDto>.Success(new TopicResponseDto
        {
            Id = topic.Id,
            MuseumId = topic.MuseumId,
            Name = topic.Name,
            Description = topic.Description,
            IsActive = topic.IsActive,
            ArtifactCount = 0,
            CreatedAt = topic.CreatedAt
        }, "Tạo chủ đề thành công.", 201);
    }

    public async Task<ResponseDto<TopicResponseDto>> UpdateAsync(int id, UpdateTopicRequestDto dto, Guid userId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ResponseDto<TopicResponseDto>.Fail("Tên chủ đề không được để trống.", 400);

        var topic = await db.Topics.FirstOrDefaultAsync(t => t.Id == id);
        if (topic is null)
            return ResponseDto<TopicResponseDto>.Fail("Không tìm thấy chủ đề.", 404);

        if (!await CanManageMuseumAsync(userId, isAdmin, topic.MuseumId))
            return ResponseDto<TopicResponseDto>.Fail("Bạn không được phân công quản lý bảo tàng này.", 403);

        var name = dto.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.MuseumId == topic.MuseumId && t.Id != id && t.Name.ToLower() == name.ToLower()))
            return ResponseDto<TopicResponseDto>.Fail("Tên chủ đề đã tồn tại trong bảo tàng này.", 409);

        topic.Name = name;
        topic.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        topic.IsActive = dto.IsActive;
        await db.SaveChangesAsync();

        var count = await db.ArtifactTopics.CountAsync(at => at.TopicId == id);
        return ResponseDto<TopicResponseDto>.Success(new TopicResponseDto
        {
            Id = topic.Id,
            MuseumId = topic.MuseumId,
            Name = topic.Name,
            Description = topic.Description,
            IsActive = topic.IsActive,
            ArtifactCount = count,
            CreatedAt = topic.CreatedAt
        }, "Cập nhật chủ đề thành công.");
    }

    public async Task<ResponseDto<object>> DeleteAsync(int id, Guid userId, bool isAdmin)
    {
        var topic = await db.Topics.FirstOrDefaultAsync(t => t.Id == id);
        if (topic is null)
            return ResponseDto<object>.Fail("Không tìm thấy chủ đề.", 404);

        if (!await CanManageMuseumAsync(userId, isAdmin, topic.MuseumId))
            return ResponseDto<object>.Fail("Bạn không được phân công quản lý bảo tàng này.", 403);

        if (await db.ArtifactTopics.AnyAsync(at => at.TopicId == id))
            return ResponseDto<object>.Fail("Chủ đề đang được gán cho hiện vật, hãy gỡ chủ đề khỏi hiện vật trước khi xóa.", 409);

        db.Topics.Remove(topic);
        await db.SaveChangesAsync();
        return ResponseDto<object>.Success(null!, "Đã xóa chủ đề thành công.");
    }

    public async Task<ResponseDto<List<ArtifactTopicTagDto>>> GetArtifactTopicsAsync(int artifactId)
    {
        if (!await db.Artifacts.AnyAsync(a => a.Id == artifactId))
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Không tìm thấy hiện vật.", 404);

        var topics = await (from at in db.ArtifactTopics.AsNoTracking()
                            join t in db.Topics.AsNoTracking() on at.TopicId equals t.Id
                            where at.ArtifactId == artifactId
                            orderby t.Name
                            select new ArtifactTopicTagDto
                            {
                                Id = t.Id,
                                Name = t.Name,
                                Description = t.Description
                            }).ToListAsync();

        return ResponseDto<List<ArtifactTopicTagDto>>.Success(topics);
    }

    public async Task<ResponseDto<List<ArtifactTopicTagDto>>> AssignTopicsAsync(
        int artifactId, IReadOnlyCollection<int> topicIds, Guid userId, bool isAdmin)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Không tìm thấy hiện vật.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var distinctIds = topicIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Danh sách chủ đề không được để trống.", 400);

        var validTopics = await db.Topics.AsNoTracking()
            .Where(t => distinctIds.Contains(t.Id) && t.MuseumId == artifact.MuseumId && t.IsActive)
            .ToListAsync();

        if (validTopics.Count != distinctIds.Length)
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Một hoặc nhiều chủ đề không thuộc bảo tàng của hiện vật hoặc đã ngừng hoạt động.", 400);

        var existingIds = await db.ArtifactTopics
            .Where(at => at.ArtifactId == artifactId && distinctIds.Contains(at.TopicId))
            .Select(at => at.TopicId)
            .ToListAsync();

        var toAdd = distinctIds.Except(existingIds)
            .Select(tid => new ArtifactTopic { ArtifactId = artifactId, TopicId = tid, AssignedAt = DateTime.UtcNow })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.ArtifactTopics.AddRange(toAdd);
            await db.SaveChangesAsync();
        }

        return await GetArtifactTopicsAsync(artifactId);
    }

    public async Task<ResponseDto<List<ArtifactTopicTagDto>>> SetArtifactTopicsAsync(
        int artifactId, IReadOnlyCollection<int> topicIds, Guid userId, bool isAdmin)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Không tìm thấy hiện vật.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var distinctIds = topicIds.Distinct().ToArray();
        if (distinctIds.Length > 0)
        {
            var validCount = await db.Topics.CountAsync(t =>
                distinctIds.Contains(t.Id) && t.MuseumId == artifact.MuseumId && t.IsActive);
            if (validCount != distinctIds.Length)
                return ResponseDto<List<ArtifactTopicTagDto>>.Fail("Một hoặc nhiều chủ đề không thuộc bảo tàng của hiện vật hoặc đã ngừng hoạt động.", 400);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ArtifactTopics.Where(at => at.ArtifactId == artifactId).ExecuteDeleteAsync();

        if (distinctIds.Length > 0)
        {
            var newLinks = distinctIds.Select(tid => new ArtifactTopic
            {
                ArtifactId = artifactId,
                TopicId = tid,
                AssignedAt = DateTime.UtcNow
            });
            db.ArtifactTopics.AddRange(newLinks);
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return await GetArtifactTopicsAsync(artifactId);
    }

    public async Task<ResponseDto<object>> RemoveTopicFromArtifactAsync(
        int artifactId, int topicId, Guid userId, bool isAdmin)
    {
        var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artifactId);
        if (artifact is null)
            return ResponseDto<object>.Fail("Không tìm thấy hiện vật.", 404);

        if (!await CanManageArtifactAsync(userId, isAdmin, artifact))
            return ResponseDto<object>.Fail("Bạn không có quyền quản lý hiện vật này.", 403);

        var link = await db.ArtifactTopics.FirstOrDefaultAsync(at => at.ArtifactId == artifactId && at.TopicId == topicId);
        if (link is null)
            return ResponseDto<object>.Fail("Hiện vật chưa được gán chủ đề này.", 404);

        db.ArtifactTopics.Remove(link);
        await db.SaveChangesAsync();
        return ResponseDto<object>.Success(null!, "Đã gỡ chủ đề khỏi hiện vật.");
    }
}

