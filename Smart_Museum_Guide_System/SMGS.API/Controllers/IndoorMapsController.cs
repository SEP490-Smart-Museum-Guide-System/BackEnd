using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = RoleNames.MuseumStaff + "," + RoleNames.Admin)]
[TypeFilter(typeof(IndoorMapErrorFilter))]
public class IndoorMapsController(IIndoorMapService service, IndoorImageStore images, IArtifactService artifacts) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new IndoorMapException("Phiên đăng nhập không hợp lệ.", 401);
    private bool Admin => User.IsInRole(RoleNames.Admin);
    private static object OkData<T>(T data) => ResponseDto<T>.Success(data);

    [HttpGet("me/museums")]
    public async Task<object> Museums() => OkData(await service.Museums(UserId, Admin));

    [HttpGet("museums/{museumId:int}/indoor-structure")]
    public async Task<object> Structure(int museumId) => OkData(await service.Structure(museumId, UserId, Admin));

    [HttpPut("museums/{museumId:int}/indoor-structure")]
    public async Task<object> Layout(int museumId, LayoutRequest request)
    {
        await service.SetLayout(museumId, request.Layout, UserId, Admin); return OkData(true);
    }

    [HttpPost("museums/{museumId:int}/buildings")]
    public async Task<object> Building(int museumId, BuildingRequest request) => OkData(await service.AddBuilding(museumId, request, UserId, Admin));

    [HttpPost("museums/{museumId:int}/floors")]
    public async Task<object> Floor(int museumId, FloorRequest request) => OkData(await service.AddFloor(museumId, request, UserId, Admin));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("museums/{museumId:int}/indoor-staff")]
    public async Task<object> Assign(int museumId, AssignmentRequest request)
    {
        await service.Assign(museumId, request.UserId); return OkData(true);
    }

    [HttpGet("museums/{museumId:int}/indoor-maps")]
    public async Task<object> Maps(int museumId) => OkData(await service.Maps(museumId, UserId, Admin));

    [HttpPost("museums/{museumId:int}/indoor-maps")]
    public async Task<object> Create(int museumId, CreateMapRequest request) => OkData(await service.Create(museumId, request, UserId, Admin));

    [HttpGet("indoor-maps/{id:int}/draft")]
    public async Task<object> Draft(int id) => OkData(await service.Get(id, UserId, Admin));

    [HttpGet("indoor-maps/{id:int}/links")]
    public async Task<object> Links(int id) => OkData(await service.Links(id, UserId, Admin));

    [HttpPut("indoor-maps/{id:int}/draft")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<object> Save(int id, MapDraft draft) => OkData(await service.Save(id, draft, UserId, Admin));

    [HttpPost("indoor-maps/{id:int}/image")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<object> Upload(int id, IFormFile file, [FromForm] int revision, [FromForm] bool confirm = false)
    {
        await service.Get(id, UserId, Admin);
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) throw new IndoorMapException("Ảnh phải nhỏ hơn hoặc bằng 10 MB.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream);
        var saved = await images.Save(stream.ToArray(), Path.GetExtension(file.FileName));
        try { return OkData(await service.SetImage(id, revision, saved.Key, saved.Width, saved.Height, confirm, UserId, Admin)); }
        catch { images.Delete(saved.Key); throw; }
    }

    [HttpGet("indoor-maps/{id:int}/image")]
    public async Task<IActionResult> Image(int id) => await ImageResult(id, false);

    [HttpPost("indoor-maps/{id:int}/publish")]
    public async Task<object> Publish(int id, RevisionRequest request) => OkData(await service.Publish(id, request.Revision, true, UserId, Admin));

    [HttpPost("indoor-maps/{id:int}/unpublish")]
    public async Task<object> Unpublish(int id, RevisionRequest request) => OkData(await service.Publish(id, request.Revision, false, UserId, Admin));

    [HttpPost("indoor-maps/{id:int}/preview-route")]
    public async Task<object> Route(int id, RouteRequest request) => OkData(await service.Route(id, request, UserId, Admin));

    [HttpGet("indoor-maps/{id:int}/qr/{markerId:guid}")]
    public async Task<IActionResult> Qr(int id, Guid markerId)
    {
        var artifactId = await service.QrArtifactId(id, markerId, UserId, Admin);
        var result = await artifacts.GetQrCodeImageAsync(artifactId, $"{Request.Scheme}://{Request.Host}");
        if (result.StatusCode != 200) return StatusCode(result.StatusCode, ResponseDto<object>.Fail(result.Message, result.StatusCode));
        return File(result.Data!, "image/png", $"artifact-{artifactId}-qr.png");
    }

    // Published reads still require login, but do not require a staff role.
    [AllowAnonymous]
    [HttpGet("indoor-maps/{id:int}/published")]
    public async Task<IActionResult> Published(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        return Ok(OkData(await service.Published(id)));
    }

    [AllowAnonymous]
    [HttpGet("indoor-maps/{id:int}/published/image")]
    public async Task<IActionResult> PublishedImage(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        return await ImageResult(id, true);
    }

    private async Task<IActionResult> ImageResult(int id, bool published)
    {
        var key = await service.ImageKey(id, UserId, Admin, published);
        if (Request.Query.TryGetValue("key", out var expected) && expected != key)
            throw new IndoorMapException("Ảnh sơ đồ đã thay đổi. Hãy tải lại bản đồ.", 409);
        var path = images.PathFor(key);
        if (!System.IO.File.Exists(path)) throw new IndoorMapException("Không tìm thấy file ảnh sơ đồ.", 404);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(path, "image/png");
    }
}

public class IndoorMapErrorFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (code, message) = context.Exception switch
        {
            IndoorMapException e => (e.StatusCode, e.Message),
            DbUpdateConcurrencyException => (409, "Sơ đồ đã thay đổi. Hãy tải lại trước khi lưu."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" } } => (409, "Dữ liệu trùng hoặc liên kết đã thay đổi. Hãy tải lại."),
            PostgresException { SqlState: "40001" } => (409, "Dữ liệu đang được cập nhật. Hãy tải lại."),
            DbUpdateException { InnerException: PostgresException { SqlState: "40001" } } => (409, "Dữ liệu đang được cập nhật. Hãy tải lại."),
            _ => (0, "")
        };
        if (code == 0) return;
        context.Result = new ObjectResult(ResponseDto<object>.Fail(message, code)) { StatusCode = code };
        context.ExceptionHandled = true;
    }
}
