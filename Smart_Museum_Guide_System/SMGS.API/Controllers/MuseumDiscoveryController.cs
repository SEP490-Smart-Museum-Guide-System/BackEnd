using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController, AllowAnonymous]
public class MuseumDiscoveryController(ApplicationDbContext db, ICommerceService commerce) : ControllerBase
{
    private Guid? VisitorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("api/museums/{museumId:int}/discovery")]
    public async Task<IActionResult> Get(int museumId, CancellationToken token)
    {
        if (!await db.Museums.AnyAsync(m => m.Id == museumId && m.IsActive, token))
            return NotFound(ResponseDto<object>.Fail("Không tìm thấy bảo tàng.", 404));

        var grants = await CommerceService.GrantedResourceIds(db, museumId, VisitorId);
        var eligible = from a in db.Artifacts.AsNoTracking()
                       join resource in db.CommerceResources on a.Id equals resource.ArtifactId into resources
                       from r in resources.DefaultIfEmpty()
                       where a.MuseumId == museumId && a.IsActive &&
                           (r == null ? a.AccessLevel == "free" : r.AccessLevel == "free" || grants.Contains(r.Id))
                       select a;
        var qrArtifactCount = await eligible.CountAsync(a => !string.IsNullOrWhiteSpace(a.QrCode), token);
        var ranked = await (from a in eligible
            let score = db.Set<RecognitionHistory>().LongCount(h => h.ArtifactId == a.Id && h.IsSuccess && h.ConfirmedQrScan)
                + db.Set<ArtifactDetailView>().LongCount(v => v.ArtifactId == a.Id)
            where score > 0
            orderby score descending, a.Id
            select new { a.Id, a.Name, ImageId = a.MediaItems.Where(m => m.MediaType == "image")
                .OrderBy(m => m.SortOrder).ThenBy(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault() }
        ).Take(3).ToListAsync(token);
        var topArtifacts = ranked.Select(a => new { a.Id, a.Name,
            ImageUrl = a.ImageId is int image ? $"/api/artifacts/{a.Id}/media/{image}/file" : null });
        return Ok(ResponseDto<object>.Success(new { qrArtifactCount, topArtifacts }));
    }

    public record DetailViewRequest(Guid RequestId);

    [HttpPost("api/artifacts/{artifactId:int}/detail-views")]
    public async Task<IActionResult> RecordView(int artifactId, DetailViewRequest request, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty)
            return BadRequest(ResponseDto<object>.Fail("Cần RequestId UUID.", 400));
        if (!await db.Artifacts.AnyAsync(a => a.Id == artifactId && a.IsActive && a.Museum!.IsActive, token))
            return NotFound(ResponseDto<object>.Fail("Không tìm thấy hiện vật.", 404));
        if (!await commerce.CanReadArtifact(artifactId, VisitorId))
            return StatusCode(403, ResponseDto<object>.Fail("Bạn chưa có quyền xem hiện vật.", 403));

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO artifact_detail_views ("RequestId", "ArtifactId", "ViewedAt")
            VALUES ({request.RequestId}, {artifactId}, CURRENT_TIMESTAMP)
            ON CONFLICT ("RequestId") DO NOTHING
            """, token);
        if (inserted == 0 && !await db.Set<ArtifactDetailView>().AnyAsync(
            v => v.RequestId == request.RequestId && v.ArtifactId == artifactId, token))
            return Conflict(ResponseDto<object>.Fail("RequestId đã được dùng cho hiện vật khác.", 409));
        return NoContent();
    }
}
