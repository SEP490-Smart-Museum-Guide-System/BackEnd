using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/visitor-home")]
[TypeFilter(typeof(MediaErrorFilter))]
public class VisitorHomeController(ApplicationDbContext db, IServiceProvider services) : ControllerBase
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxRequestBytes = 51 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private IFileStorageService Storage => services.GetRequiredService<IFileStorageService>();

    public record Slide(string Name, string Caption, string? Title = null);
    public record DraftSlide(string? Name, string? Caption, int? FileIndex, string? Title);
    public record Draft(Guid Revision, string? Title, DraftSlide?[]? Slides);
    private record Upload(string Name, string Mime, byte[] Bytes);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken token) =>
        Ok(Describe(await db.Set<VisitorHome>().AsNoTracking().SingleAsync(x => x.Id == 1, token)));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Save([FromForm] string draft, [FromForm] List<IFormFile> files, CancellationToken token)
    {
        Draft? input;
        try { input = JsonSerializer.Deserialize<Draft>(draft, Json); }
        catch (JsonException) { return Invalid("Cấu hình ảnh không hợp lệ."); }

        if (input?.Slides is not { Length: <= 10 } rows || files.Count > 10 || input.Title?.Trim().Length > 120)
            return Invalid("Tối đa 10 ảnh và 120 ký tự cho tiêu đề.");

        var home = await db.Set<VisitorHome>().SingleAsync(x => x.Id == 1, token);
        if (home.Revision != input.Revision) return Changed();

        var existing = ReadSlides(home).Select(x => x.Name).ToHashSet();
        var usedFiles = new HashSet<int>();
        var usedNames = new HashSet<string>();
        var uploads = new List<Upload>();
        var slides = new List<Slide>();
        foreach (var row in rows)
        {
            if (row is null || row.Caption?.Trim().Length > 160)
                return Invalid("Chú thích ảnh tối đa 160 ký tự.");

            if (row.Title?.Trim().Length > 120)
                return Invalid("Tiêu đề mỗi ảnh tối đa 120 ký tự.");

            string name;
            if (row.FileIndex is int index)
            {
                if (row.Name is not null || index < 0 || index >= files.Count || !usedFiles.Add(index))
                    return Invalid("Tệp ảnh không hợp lệ.");
                var file = files[index];
                if (file.Length == 0 || file.Length > MaxImageBytes)
                    return Invalid("Mỗi ảnh tối đa 5 MB.");
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, token);
                var bytes = buffer.ToArray();
                try
                {
                    var spec = ArtifactAssetStore.ValidateMedia(bytes, file.FileName, "image");
                    name = $"{Guid.NewGuid():N}{spec.Extension}";
                    uploads.Add(new Upload(name, spec.ContentType, bytes));
                }
                catch (ArgumentException error) { return Invalid(error.Message); }
            }
            else
            {
                if (row.Name is null || !existing.Contains(row.Name) || !usedNames.Add(row.Name))
                    return Invalid("Ảnh đã lưu không hợp lệ.");
                name = row.Name;
            }

            slides.Add(new Slide(name, row.Caption?.Trim() ?? "", row.Title?.Trim()));
        }
        if (usedFiles.Count != files.Count) return Invalid("Có tệp ảnh chưa được sử dụng.");

        var uploaded = new List<string>();
        try
        {
            foreach (var file in uploads)
            {
                uploaded.Add(file.Name);
                using var stream = new MemoryStream(file.Bytes, writable: false);
                await Storage.UploadAsync(Key(file.Name), stream, file.Mime, token);
            }
        }
        catch
        {
            await RemoveFiles(uploaded);
            throw;
        }

        home.Title = input.Title?.Trim() ?? "";
        home.SlidesJson = JsonSerializer.Serialize(slides, Json);
        home.Revision = Guid.NewGuid();
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        {
            await RemoveFiles(uploaded);
            return Changed();
        }

        await RemoveFiles(existing.Except(slides.Select(slide => slide.Name)));
        return Ok(Describe(home));
    }

    [HttpGet("images/{name}")]
    [AllowAnonymous]
    public async Task<IActionResult> Image(string name, CancellationToken token)
    {
        var mime = Path.GetExtension(name) switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => null
        };
        if (mime is null) return NotFound();
        var home = await db.Set<VisitorHome>().AsNoTracking().SingleAsync(x => x.Id == 1, token);
        if (!ReadSlides(home).Any(slide => slide.Name == name)) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(await Storage.OpenReadAsync(Key(name), token), mime);
    }

    private async Task RemoveFiles(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            try { await Storage.DeleteAsync(Key(name), CancellationToken.None); }
            catch (Exception error) when (error is MediaStorageException or HttpRequestException or OperationCanceledException)
            {
                services.GetRequiredService<ILogger<VisitorHomeController>>()
                    .LogWarning(error, "Could not remove visitor home image {ImageName}", name);
            }
        }
    }

    private static string Key(string name) => $"visitor-home/images/{name}";
    private static Slide[] ReadSlides(VisitorHome home) => JsonSerializer.Deserialize<Slide[]>(home.SlidesJson, Json) ?? [];
    private static ResponseDto<object> Describe(VisitorHome home) =>
        ResponseDto<object>.Success(new { home.Revision, home.Title, Slides = ReadSlides(home) });
    private IActionResult Invalid(string message) => BadRequest(ResponseDto<object>.Fail(message, 400));
    private IActionResult Changed() =>
        Conflict(ResponseDto<object>.Fail("Trang chủ vừa được cập nhật. Hãy đóng và mở lại trình chỉnh sửa.", 409));
}
