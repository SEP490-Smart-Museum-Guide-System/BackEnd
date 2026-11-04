using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Museum.Request;
using SMGS.Application.DTOs.Museum.Response;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

// Metadata and cover upload in one request; existing image endpoints remain in MuseumImagesController.
[ApiController, Route("api/museums"), TypeFilter(typeof(MediaErrorFilter))]
public class MuseumFormsController(IMuseumService museums, ApplicationDbContext db, MuseumImageStore images) : ControllerBase
{
    [Authorize(Roles = RoleNames.Admin), HttpPost("with-image")]
    [RequestSizeLimit(16 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public Task<IActionResult> Create([FromForm] CreateMuseumRequestDto request, IFormFile? file, CancellationToken token) =>
        Save(file, () => museums.CreateAsync(request), true, token);

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff), HttpPut("{id:int}/with-image")]
    [RequestSizeLimit(16 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateMuseumRequestDto request, IFormFile? file, CancellationToken token)
    {
        if (!User.IsInRole(RoleNames.Admin) &&
            (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
             !await db.StaffAssignments.AnyAsync(a => a.UserId == userId && a.MuseumId == id && a.GalleryId == null && a.ArtifactId == null, token)))
            return Forbid();
        return await Save(file, () => museums.UpdateAsync(id, request), false, token);
    }

    private async Task<IActionResult> Save(IFormFile? file, Func<Task<ResponseDto<MuseumResponseDto>>> write, bool creating, CancellationToken token)
    {
        byte[] bytes;
        try
        {
            if (file is null)
            {
                var metadata = await write();
                return StatusCode(metadata.StatusCode, metadata);
            }
            if (file.Length == 0 || file.Length > MuseumImageStore.MaxBytes)
                return BadRequest(ResponseDto<object>.Fail("Ảnh phải có dung lượng từ 1 byte đến 15 MB."));
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, token);
            bytes = buffer.ToArray();
            images.ValidateUpload(bytes, file.FileName);
        }
        catch (ArgumentException error) { return BadRequest(ResponseDto<object>.Fail(error.Message)); }

        // Create obtains a real museum ID before generating its R2 key. Both DB writes share this transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        string? key = null;
        var committed = false;
        try
        {
            var metadata = await write();
            if (metadata.StatusCode is < 200 or >= 300 || metadata.Data is null)
                return StatusCode(metadata.StatusCode, metadata);
            key = await images.Save(bytes, file!.FileName, metadata.Data.Id, token);
            var imageUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/museums/{metadata.Data.Id}/image/{Path.GetFileName(key)}";
            var museum = metadata.Data;
            var updated = await museums.UpdateAsync(museum.Id, new UpdateMuseumRequestDto
            {
                Name = museum.Name, Description = museum.Description, Address = museum.Address,
                CityCode = museum.CityCode, Latitude = museum.Latitude, Longitude = museum.Longitude,
                OpeningHours = museum.OpeningHours, CoverImageUrl = imageUrl,
            });
            if (updated.StatusCode is < 200 or >= 300) return StatusCode(updated.StatusCode, updated);
            await transaction.CommitAsync(token);
            committed = true;
            // Old R2 objects may have shared references. Keep the remote branch's retention policy.
            var status = creating ? 201 : 200;
            return StatusCode(status, ResponseDto<MuseumResponseDto>.Success(updated.Data!, creating ? "Tạo bảo tàng thành công." : "Cập nhật bảo tàng thành công.", status));
        }
        finally { if (!committed) await images.Delete(key); }
    }
}
