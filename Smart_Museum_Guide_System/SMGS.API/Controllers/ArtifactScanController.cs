using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/artifacts/{artifactId:int}/scans")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
public class ArtifactScanController(IAccountAdminService scopes, IArtifactAssetService assets,
    IHttpClientFactory clients, IConfiguration config, ILogger<ArtifactScanController> logger) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool Admin => User.IsInRole(RoleNames.Admin);
    private sealed class ScanError(int status, string message) : Exception(message) { public int Status => status; }

    private async Task<IActionResult> Authorized(int artifactId, Func<Task<IActionResult>> action, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        if (!await scopes.CanManageArtifactAsync(UserId, Admin, artifactId))
            return StatusCode(403, ResponseDto<object>.Fail("Bạn không có quyền quản lý hiện vật này.", 403));
        try { return await action(); }
        catch (ScanError e) { return StatusCode(e.Status, ResponseDto<object>.Fail(e.Message, e.Status)); }
        catch (HttpRequestException) { return StatusCode(503, ResponseDto<object>.Fail("AI Scan chưa kết nối được. Kiểm tra cấu hình server.", 503)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(504, ResponseDto<object>.Fail("AI Scan phản hồi quá thời gian. Kiểm tra danh sách job trước khi gửi lại.", 504)); }
        catch (JsonException) { return StatusCode(502, ResponseDto<object>.Fail("AI Scan trả dữ liệu không hợp lệ.", 502)); }
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, CancellationToken ct, HttpContent? body = null)
    {
        var key = config["AiScan:ServiceKey"];
        if (string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(config["AiScan:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            throw new ScanError(503, "Chưa cấu hình AiScan:BaseUrl / AiScan:ServiceKey tại Backend.");
        using var request = new HttpRequestMessage(method, uri.ToString().TrimEnd('/') + path) { Content = body };
        request.Headers.Add("X-Service-Key", key);
        var response = await clients.CreateClient("artifact-scan").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            string message = "Không thể xử lý yêu cầu AI Scan.";
            try {
                var json = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
                if (json?["detail"] is JsonValue value && value.TryGetValue<string>(out var detail)) message = detail;
            } catch (JsonException) { }
            response.Dispose();
            if (status is 401 or 403) { status = 503; message = "Khóa kết nối AI Scan tại Backend chưa đúng."; }
            throw new ScanError(status, message);
        }
        return response;
    }

    private async Task<JsonObject> Job(int artifactId, Guid jobId, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"/api/v1/scans/{jobId}", ct);
        var job = await response.Content.ReadFromJsonAsync<JsonObject>(ct) ?? throw new JsonException();
        if (job["artifactId"]?.GetValue<int>() != artifactId)
            throw new ScanError(403, "Job không thuộc hiện vật này.");
        return job;
    }

    [HttpGet("health")]
    public Task<IActionResult> Health(int artifactId, CancellationToken ct) => Authorized(artifactId, async () => {
        using var response = await Send(HttpMethod.Get, "/ready", ct);
        return Ok(ResponseDto<JsonNode>.Success((await response.Content.ReadFromJsonAsync<JsonNode>(ct))!));
    }, ct);

    [HttpGet]
    public Task<IActionResult> List(int artifactId, CancellationToken ct) => Authorized(artifactId, async () => {
        using var response = await Send(HttpMethod.Get, $"/api/v1/artifacts/{artifactId}/scans", ct);
        return Ok(ResponseDto<JsonNode>.Success((await response.Content.ReadFromJsonAsync<JsonNode>(ct))!));
    }, ct);

    [HttpPost]
    [RequestSizeLimit(550L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 550L * 1024 * 1024)]
    public Task<IActionResult> Create(int artifactId, [FromForm] List<IFormFile> images,
        [FromForm] IFormFile? metadata, [FromForm] IFormFile? referenceObj,
        [FromForm] string mode = "auto", CancellationToken ct = default) => Authorized(artifactId, async () => {
        if (images.Count is < 5 or > 200) throw new ScanError(422, "Chọn 5–200 ảnh của cùng một hiện vật.");
        if (images.Sum(f => f.Length) + (metadata?.Length ?? 0) + (referenceObj?.Length ?? 0) > 500L * 1024 * 1024)
            throw new ScanError(413, "Bộ dữ liệu tối đa 500 MiB.");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(artifactId.ToString()), "artifact_id");
        form.Add(new StringContent(mode), "mode");
        void Add(IFormFile file, string field) {
            var part = new StreamContent(file.OpenReadStream());
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, field, Path.GetFileName(file.FileName.Replace('\\', '/')));
        }
        foreach (var file in images) Add(file, "images");
        if (metadata is not null) Add(metadata, "metadata");
        if (referenceObj is not null) Add(referenceObj, "reference_obj");
        using var response = await Send(HttpMethod.Post, "/api/v1/scans/dataset", ct, form);
        var job = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        return StatusCode(202, ResponseDto<JsonNode>.Success(job!, "Đã nhận bộ ảnh, đang xếp hàng dựng 3D.", 202));
    }, ct);

    [HttpGet("{jobId:guid}")]
    public Task<IActionResult> Status(int artifactId, Guid jobId, CancellationToken ct) => Authorized(artifactId,
        async () => Ok(ResponseDto<JsonObject>.Success(await Job(artifactId, jobId, ct))), ct);

    [HttpGet("{jobId:guid}/model")]
    public Task<IActionResult> Model(int artifactId, Guid jobId, CancellationToken ct) => Authorized(artifactId, async () => {
        var job = await Job(artifactId, jobId, ct);
        if (job["status"]?.GetValue<string>() is not ("REVIEW_REQUIRED" or "COMPLETED"))
            throw new ScanError(409, "Model chưa sẵn sàng.");
        var response = await Send(HttpMethod.Get, $"/api/v1/scans/{jobId}/model", ct);
        Response.RegisterForDispose(response);
        Response.Headers.CacheControl = "private, no-store";
        return File(await response.Content.ReadAsStreamAsync(ct), "model/gltf-binary");
    }, ct);

    [HttpPost("{jobId:guid}/save")]
    public Task<IActionResult> Save(int artifactId, Guid jobId, CancellationToken ct) => Authorized(artifactId, async () => {
        var job = await Job(artifactId, jobId, ct);
        if (job["status"]?.GetValue<string>() != "REVIEW_REQUIRED") throw new ScanError(409, "Job phải ở trạng thái chờ xem xét.");
        using var response = await Send(HttpMethod.Get, $"/api/v1/scans/{jobId}/model", ct);
        var length = response.Content.Headers.ContentLength;
        if (length is null or <= 0 or > 104857600) throw new ScanError(413, "GLB phải có dung lượng hợp lệ, tối đa 100 MiB.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        // Existing media validation rewinds the GLB. HTTP response streams cannot seek.
        // Spool to disk with a strict bound instead of holding a full model in RAM.
        await using var buffered = new FileStream(Path.Combine(Path.GetTempPath(), $"smgs-scan-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var chunk = new byte[65536]; long received = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) != 0)
        {
            received += read;
            if (received > length.Value) throw new ScanError(502, "Dung lượng GLB không khớp phản hồi AI.");
            await buffered.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (received != length.Value) throw new ScanError(502, "Tải GLB chưa đầy đủ. Vui lòng thử lại.");
        buffered.Position = 0;
        var result = await assets.UploadOrReplace3DModelAsync(artifactId, buffered, length.Value,
            "model/gltf-binary", $"scan-{jobId}.glb", new Update3DModelSettingsRequestDto(), UserId, Admin, ct);
        if (result.StatusCode is >= 200 and < 300)
        {
            try { using var approved = await Send(HttpMethod.Post, $"/api/v1/scans/{jobId}/approve", ct); }
            catch (Exception e) when (e is ScanError or HttpRequestException or OperationCanceledException)
            { logger.LogWarning("Model saved for artifact {ArtifactId}; could not mark scan {JobId} completed", artifactId, jobId); }
        }
        return StatusCode(result.StatusCode, result);
    }, ct);

    [HttpPost("{jobId:guid}/discard")]
    public Task<IActionResult> Discard(int artifactId, Guid jobId, CancellationToken ct) => Authorized(artifactId, async () => {
        await Job(artifactId, jobId, ct);
        using var response = await Send(HttpMethod.Post, $"/api/v1/scans/{jobId}/discard", ct);
        return Ok(ResponseDto<object>.Success(new { status = "DISCARDED" }, "Đã bỏ kết quả scan; model đang lưu không thay đổi."));
    }, ct);
}
