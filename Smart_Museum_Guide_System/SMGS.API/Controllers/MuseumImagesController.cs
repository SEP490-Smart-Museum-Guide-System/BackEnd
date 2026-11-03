using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/museums/{museumId:int}/image")]
[TypeFilter(typeof(MediaErrorFilter))]
public class MuseumImagesController(ApplicationDbContext db, IServiceProvider services, IConfiguration configuration) : ControllerBase
{
    private IFileStorageService Storage => services.GetRequiredService<IFileStorageService>();
    private async Task<bool> CanManage(int id, CancellationToken token)
    {
        if (User.IsInRole(RoleNames.Admin)) return true;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var user)
            && await db.StaffAssignments.AnyAsync(a => a.UserId == user && a.MuseumId == id && a.GalleryId == null && a.ArtifactId == null, token);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int museumId, IFormFile? file, CancellationToken token)
    {
        if (!await CanManage(museumId, token)) return StatusCode(403);
        var museum = await db.Museums.SingleOrDefaultAsync(m => m.Id == museumId, token);
        if (museum is null) return NotFound();
        if (file is null || file.Length == 0 || file.Length > MediaValidation.Limit(configuration, "image", 15))
            return BadRequest(ResponseDto<object>.Fail("Chọn ảnh PNG/JPEG/WebP, tối đa 15 MB.", 400));
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, token);
        (string MediaType, string ContentType, string Extension) spec;
        try { spec = ArtifactAssetStore.ValidateMedia(buffer.ToArray(), file.FileName, "image"); }
        catch (ArgumentException e) { return BadRequest(ResponseDto<object>.Fail(e.Message, 400)); }
        var name = $"{Guid.NewGuid():N}{spec.Extension}";
        var key = $"museums/{museumId}/images/{name}";
        buffer.Position = 0;
        await Storage.UploadAsync(key, buffer, spec.ContentType, token);
        // Stable API URL fits the existing field and remains usable by mobile clients.
        var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/museums/{museumId}/image/{name}";
        try {
            museum.CoverImageUrl = url;
            await db.SaveChangesAsync(token);
        }
        catch {
            try { await Storage.DeleteAsync(key, CancellationToken.None); } catch (MediaStorageException) { }
            throw;
        }
        // Do not remove the old object: legacy URL fields can be shared across records.
        return Ok(ResponseDto<object>.Success(new { CoverImageUrl = url }));
    }

    [HttpGet("{name}")]
    [AllowAnonymous]
    public async Task<IActionResult> Image(int museumId, string name, CancellationToken token)
    {
        var key = $"museums/{museumId}/images/{name}";
        try { R2FileStorageService.ValidateKey(key); } catch (ArgumentException) { return NotFound(); }
        var suffix = $"/api/museums/{museumId}/image/{name}";
        var publicReference = await db.Museums.AnyAsync(m => m.IsActive && m.CoverImageUrl != null && m.CoverImageUrl.EndsWith(suffix), token);
        if (!publicReference) {
            if (!await CanManage(museumId, token) || !await db.Museums.AnyAsync(m => m.Id == museumId && m.CoverImageUrl != null && m.CoverImageUrl.EndsWith(suffix), token))
                return NotFound();
        }
        var mime = Path.GetExtension(name) switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => null };
        if (mime is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(await Storage.OpenReadAsync(key, token), mime);
    }

    [HttpDelete]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    public async Task<IActionResult> Remove(int museumId, CancellationToken token)
    {
        if (!await CanManage(museumId, token)) return StatusCode(403);
        var museum = await db.Museums.SingleOrDefaultAsync(m => m.Id == museumId, token);
        if (museum is null) return NotFound();
        museum.CoverImageUrl = null;
        await db.SaveChangesAsync(token);
        return Ok(ResponseDto<object>.Success(new { CoverImageUrl = (string?)null }));
    }
}
