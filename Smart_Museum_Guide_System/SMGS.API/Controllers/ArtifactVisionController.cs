using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.API.Controllers;

[ApiController]
[Authorize]
public class ArtifactVisionController(IAccountAdminService scopes, ApplicationDbContext db,
    IHttpClientFactory clients, IConfiguration config) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool Admin => User.IsInRole(RoleNames.Admin);
    private sealed class VisionError(int status, string message) : Exception(message) { public int Status => status; }

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        try { return await action(); }
        catch (VisionError e) { return StatusCode(e.Status, ResponseDto<object>.Fail(e.Message, e.Status)); }
        catch (HttpRequestException) { return StatusCode(503, ResponseDto<object>.Fail("Chưa kết nối được dịch vụ nhận diện.", 503)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(504, ResponseDto<object>.Fail("Nhận diện phản hồi quá thời gian. Vui lòng thử lại.", 504)); }
        catch (JsonException) { return StatusCode(502, ResponseDto<object>.Fail("Dịch vụ nhận diện trả dữ liệu không hợp lệ.", 502)); }
    }

    private async Task<JsonObject> Send(HttpMethod method, string path, CancellationToken ct, HttpContent? body = null)
    {
        var key = config["AiVision:ServiceKey"];
        if (string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(config["AiVision:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            throw new VisionError(503, "Chưa cấu hình AiVision:BaseUrl / AiVision:ServiceKey tại Backend.");
        using var request = new HttpRequestMessage(method, uri.ToString().TrimEnd('/') + path) { Content = body };
        request.Headers.Add("X-Service-Key", key);
        using var response = await clients.CreateClient("artifact-vision").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var message = "Không thể xử lý yêu cầu nhận diện.";
            try
            {
                var error = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
                if (error?["detail"] is JsonValue value && value.TryGetValue<string>(out var detail)) message = detail;
            }
            catch (JsonException) { }
            if (status is 401 or 403) { status = 503; message = "Khóa kết nối Vision tại Backend chưa đúng."; }
            throw new VisionError(status, message);
        }
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct) ?? throw new JsonException();
    }

    private static MultipartFormDataContent ImageForm(IFormFile image)
    {
        if (image.Length <= 0) throw new VisionError(422, "Ảnh không được rỗng.");
        if (image.Length > 10L * 1024 * 1024) throw new VisionError(413, "Ảnh nhận diện tối đa 10 MiB.");
        var form = new MultipartFormDataContent();
        var part = new StreamContent(image.OpenReadStream());
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(part, "image", Path.GetFileName(image.FileName.Replace('\\', '/')));
        return form;
    }

    [HttpGet("api/vision/health")]
    public Task<IActionResult> Health(CancellationToken ct) => Run(async () =>
        Ok(ResponseDto<JsonObject>.Success(await Send(HttpMethod.Get, "/health", ct))), ct);

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("api/artifacts/{artifactId:int}/vision/references")]
    [RequestSizeLimit(12L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 12L * 1024 * 1024)]
    public Task<IActionResult> Index(int artifactId, IFormFile image, CancellationToken ct) => Run(async () =>
    {
        if (!await scopes.CanManageArtifactAsync(UserId, Admin, artifactId))
            throw new VisionError(403, "Bạn không được phân công quản lý hiện vật này.");
        using var form = ImageForm(image);
        return Ok(ResponseDto<JsonObject>.Success(await Send(HttpMethod.Post, $"/api/v1/references?artifact_id={artifactId}", ct, form)));
    }, ct);

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("api/artifacts/{artifactId:int}/vision/references")]
    public Task<IActionResult> Delete(int artifactId, CancellationToken ct) => Run(async () =>
    {
        if (!await scopes.CanManageArtifactAsync(UserId, Admin, artifactId))
            throw new VisionError(403, "Bạn không được phân công quản lý hiện vật này.");
        return Ok(ResponseDto<JsonObject>.Success(await Send(HttpMethod.Delete, $"/api/v1/references/{artifactId}", ct)));
    }, ct);

    [HttpPost("api/vision/recognitions")]
    [RequestSizeLimit(12L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 12L * 1024 * 1024)]
    public Task<IActionResult> Recognize(IFormFile image, [FromQuery(Name = "top_k")] int topK = 3,
        [FromQuery(Name = "museum_id")] int? museumId = null, CancellationToken ct = default) => Run(async () =>
    {
        if (topK is < 1 or > 10 || museumId is <= 0) throw new VisionError(422, "Tham số nhận diện không hợp lệ.");
        using var form = ImageForm(image);
        // Recognition exposes only active artifacts with current published content,
        // even when the derived index still contains draft/deactivated references.
        var ids = await db.Artifacts.AsNoTracking()
            .Where(a => a.IsActive && a.Museum!.IsActive && (!museumId.HasValue || a.MuseumId == museumId))
            .Where(a => db.Set<ArtifactContentVersion>().Any(v => v.Content.ArtifactId == a.Id && v.IsCurrent && v.Status == ContentStatuses.Published))
            .OrderBy(a => a.Id).Select(a => a.Id).Take(5001).ToListAsync(ct);
        if (ids.Count > 5000) throw new VisionError(422, "Vui lòng chọn bảo tàng trước khi nhận diện.");
        if (ids.Count == 0)
            return Ok(ResponseDto<object>.Success(new { matched = false, artifactId = (int?)null, similarity = 0,
                margin = 0, requiresConfirmation = true, candidates = Array.Empty<object>(), message = "Chưa có hiện vật đã xuất bản để nhận diện." }));
        form.Add(new StringContent(JsonSerializer.Serialize(ids)), "artifact_ids");
        var result = await Send(HttpMethod.Post, $"/api/v1/recognitions?top_k={topK}", ct, form);
        // Also defend against an older AI service that ignores artifact_ids.
        var candidates = result["candidates"] as JsonArray ?? throw new JsonException();
        var allowed = ids.ToHashSet();
        var filtered = new JsonArray();
        foreach (var candidate in candidates)
            if (candidate?["artifactId"]?.GetValue<int>() is int id && allowed.Contains(id)) filtered.Add(candidate.DeepClone());
        var winner = result["artifactId"]?.GetValue<int>();
        if (filtered.Count != candidates.Count || (winner.HasValue && !allowed.Contains(winner.Value)))
        {
            result["matched"] = false; result["artifactId"] = null; result["requiresConfirmation"] = true; result["margin"] = 0;
            result["similarity"] = filtered.FirstOrDefault()?["similarity"]?.DeepClone() ?? JsonValue.Create(0);
        }
        result["candidates"] = filtered;
        return Ok(ResponseDto<JsonObject>.Success(result));
    }, ct);
}
