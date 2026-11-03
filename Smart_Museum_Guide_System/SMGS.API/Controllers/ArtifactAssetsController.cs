using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[TypeFilter(typeof(MediaErrorFilter))]
[Route("api/artifacts/{artifactId:int}")]
[Produces("application/json")]
public class ArtifactAssetsController(IArtifactAssetService assetService) : ControllerBase
{
    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsAdmin => User.IsInRole(RoleNames.Admin);
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    private string ToAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        return $"{BaseUrl}{(url.StartsWith('/') ? "" : "/")}{url}";
    }

    private ArtifactMediaResponseDto WithAbsoluteUrl(ArtifactMediaResponseDto dto)
    {
        dto.FileUrl = ToAbsoluteUrl(dto.FileUrl);
        return dto;
    }

    private Artifact3DModelResponseDto WithAbsoluteUrl(Artifact3DModelResponseDto dto)
    {
        dto.ModelUrl = ToAbsoluteUrl(dto.ModelUrl);
        if (!string.IsNullOrWhiteSpace(dto.PosterUrl))
            dto.PosterUrl = ToAbsoluteUrl(dto.PosterUrl);
        return dto;
    }

    // ────────────────────────────────────────────────────────────
    //  MEDIA ENDPOINTS (CD-06B)
    // ────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet("media")]
    public async Task<IActionResult> ListMedia(
        int artifactId,
        [FromQuery] string? mediaType = null,
        [FromQuery] string? languageCode = null)
    {
        var response = await assetService.GetMediaListAsync(artifactId, mediaType, languageCode, CurrentUserId);
        if (response.Data is not null)
        {
            foreach (var item in response.Data) WithAbsoluteUrl(item);
        }
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("media")]
    [RequestSizeLimit(105L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]
    public async Task<IActionResult> UploadMedia(
        int artifactId,
        IFormFile? file,
        [FromForm] string? title = null,
        [FromForm] string? mediaType = null,
        [FromForm] string? languageCode = null,
        [FromForm] int sortOrder = 0)
    {
        if (CurrentUserId is null)
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        if (file is null || file.Length == 0)
            return BadRequest(ResponseDto<object>.Fail("Vui lòng chọn file media để tải lên.", 400));

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, HttpContext.RequestAborted);

        var response = await assetService.UploadMediaAsync(
            artifactId,
            stream.ToArray(),
            file.FileName,
            title,
            mediaType,
            languageCode,
            sortOrder,
            CurrentUserId.Value,
            IsAdmin, HttpContext.RequestAborted);

        if (response.Data is not null) WithAbsoluteUrl(response.Data);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("media/{mediaId:int}/file")]
    public async Task<IActionResult> GetMediaFile(int artifactId, int mediaId)
    {
        var response = await assetService.GetMediaFileAsync(artifactId, mediaId, CurrentUserId, HttpContext.RequestAborted);
        if (response.StatusCode != 200)
            return StatusCode(response.StatusCode, ResponseDto<object>.Fail(response.Message, response.StatusCode));

        var (content, contentType, _) = response.Data;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(content, contentType, enableRangeProcessing: true);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("media/{mediaId:int}")]
    public async Task<IActionResult> DeleteMedia(int artifactId, int mediaId)
    {
        if (CurrentUserId is null)
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));

        var response = await assetService.DeleteMediaAsync(artifactId, mediaId, CurrentUserId.Value, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    // ────────────────────────────────────────────────────────────
    //  3D MODEL & VIEWER ENDPOINTS (CD-07)
    // ────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet("model-3d")]
    public async Task<IActionResult> Get3DModel(int artifactId)
    {
        var response = await assetService.Get3DModelAsync(artifactId, CurrentUserId);
        if (response.Data is not null) WithAbsoluteUrl(response.Data);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("model-3d")]
    [HttpPut("model-3d")]
    [RequestSizeLimit(105L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]
    public async Task<IActionResult> UploadOrReplace3DModel(
        int artifactId,
        IFormFile? file,
        [FromForm] string? name = null,
        [FromForm] string? posterUrl = null,
        [FromForm] bool autoRotate = true,
        [FromForm] string? cameraOrbit = null,
        [FromForm] string? fieldOfView = null,
        [FromForm] decimal scale = 1.0m)
    {
        if (CurrentUserId is null)
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        if (file is null || file.Length == 0)
            return BadRequest(ResponseDto<object>.Fail("Vui lòng chọn file mô hình 3D (.glb).", 400));

        await using var stream = file.OpenReadStream();

        var settings = new Update3DModelSettingsRequestDto
        {
            Name = name,
            PosterUrl = posterUrl,
            AutoRotate = autoRotate,
            CameraOrbit = cameraOrbit,
            FieldOfView = fieldOfView,
            Scale = scale
        };

        var response = await assetService.UploadOrReplace3DModelAsync(
            artifactId,
            stream,
            file.Length,
            file.ContentType,
            file.FileName,
            settings,
            CurrentUserId.Value,
            IsAdmin, HttpContext.RequestAborted);

        if (response.Data is not null) WithAbsoluteUrl(response.Data);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPatch("model-3d/settings")]
    public async Task<IActionResult> Update3DModelSettings(
        int artifactId,
        [FromBody] Update3DModelSettingsRequestDto settings)
    {
        if (CurrentUserId is null)
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));

        var response = await assetService.Update3DModelSettingsAsync(artifactId, settings, CurrentUserId.Value, IsAdmin);
        if (response.Data is not null) WithAbsoluteUrl(response.Data);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("model-3d/file")]
    public async Task<IActionResult> Get3DModelFile(int artifactId)
    {
        var response = await assetService.Get3DModelFileAsync(artifactId, CurrentUserId, HttpContext.RequestAborted);
        if (response.StatusCode != 200)
            return StatusCode(response.StatusCode, ResponseDto<object>.Fail(response.Message, response.StatusCode));

        var (content, contentType, _) = response.Data;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(content, contentType, enableRangeProcessing: true);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("model-3d")]
    public async Task<IActionResult> Delete3DModel(int artifactId)
    {
        if (CurrentUserId is null)
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));

        var response = await assetService.Delete3DModelAsync(artifactId, CurrentUserId.Value, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("viewer")]
    public async Task<IActionResult> GetViewerData(int artifactId, [FromQuery] string? languageCode = null)
    {
        var response = await assetService.GetViewerDataAsync(artifactId, languageCode, CurrentUserId);
        if (response.Data is not null)
        {
            if (response.Data.Model3D is not null) WithAbsoluteUrl(response.Data.Model3D);
            foreach (var m in response.Data.Media) WithAbsoluteUrl(m);
            foreach (var c in response.Data.Contents)
            {
                if (!string.IsNullOrWhiteSpace(c.AudioUrl))
                    c.AudioUrl = ToAbsoluteUrl(c.AudioUrl);
            }
        }
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Mở trang giao diện 3D Viewer & Nghe thuyết minh trực tiếp trên trình duyệt:
    /// http://localhost:5169/api/artifacts/{artifactId}/viewer-ui
    /// </summary>
    [AllowAnonymous]
    [HttpGet("viewer-ui")]
    [Produces("text/html")]
    public IActionResult GetViewerPage(int artifactId)
    {
        var html = $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>3D & Narration Viewer - Artifact #{{artifactId}}</title>
            <script type="module" src="https://ajax.googleapis.com/ajax/libs/model-viewer/3.5.0/model-viewer.min.js"></script>
            <style>
                * { box-sizing: border-box; font-family: 'Segoe UI', system-ui, sans-serif; }
                body { margin: 0; background: #0f172a; color: #f8fafc; display: flex; flex-direction: column; min-height: 100vh; }
                header { padding: 16px 24px; background: #1e293b; border-bottom: 1px solid #334155; display: flex; justify-content: space-between; align-items: center; }
                .badge { background: #3b82f6; color: white; padding: 4px 10px; border-radius: 999px; font-size: 13px; margin-right: 6px; display: inline-block; }
                .layout { display: grid; grid-template-columns: 1.3fr 1fr; gap: 20px; padding: 20px; flex: 1; }
                @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
                .card { background: #1e293b; border: 1px solid #334155; border-radius: 12px; padding: 20px; }
                model-viewer { width: 100%; height: 480px; background: radial-gradient(#334155, #0f172a); border-radius: 10px; }
                select, button { background: #334155; color: white; border: 1px solid #475569; padding: 8px 12px; border-radius: 6px; font-size: 14px; cursor: pointer; }
                audio, video { width: 100%; margin-top: 10px; border-radius: 8px; }
                .narration-box { background: #0f172a; border-left: 4px solid #38bdf8; padding: 14px; border-radius: 6px; margin-top: 12px; line-height: 1.6; white-space: pre-wrap; }
                .media-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; margin-top: 12px; }
                .media-item { background: #0f172a; padding: 10px; border-radius: 8px; border: 1px solid #334155; }
                .media-item img { width: 100%; height: 140px; object-fit: cover; border-radius: 6px; }
                .empty { color: #94a3b8; font-style: italic; padding: 20px 0; }
            </style>
        </head>
        <body>
            <header>
                <div>
                    <h2 id="title" style="margin:0;">Đang tải hiện vật #{{artifactId}}...</h2>
                    <div id="subtitle" style="color:#94a3b8; font-size:14px; margin-top:4px;"></div>
                </div>
                <div id="topics"></div>
            </header>
            <div class="layout">
                <div class="card">
                    <h3 style="margin-top:0;">Mô hình 3D tương tác (Xoay / Phóng to bằng chuột)</h3>
                    <div id="model-container"></div>
                    <h3 style="margin-top:24px;">Bộ sưu tập Media (Ảnh / Audio / Video)</h3>
                    <div id="media-container" class="media-grid"></div>
                </div>
                <div class="card">
                    <div style="display:flex; justify-content:space-between; align-items:center;">
                        <h3 style="margin:0;">Nội dung & Thuyết minh (Narration)</h3>
                        <select id="lang-select"></select>
                    </div>
                    <div id="content-container" style="margin-top:16px;"></div>
                </div>
            </div>
            <script>
                const artifactId = {{artifactId}};
                let viewerData = null;

                async function load() {
                    const res = await fetch(`/api/artifacts/${artifactId}/viewer`);
                    const json = await res.json();
                    if (!res.ok || !json.data) {
                        document.getElementById('title').textContent = json.message || 'Không tải được dữ liệu hiện vật';
                        return;
                    }
                    viewerData = json.data;
                    document.getElementById('title').textContent = `${viewerData.name} (${viewerData.code || '#' + viewerData.id})`;
                    document.getElementById('subtitle').textContent = `${viewerData.museumName} ${viewerData.galleryName ? '• ' + viewerData.galleryName : ''}`;
                    document.getElementById('topics').innerHTML = (viewerData.topics || []).map(t => `<span class="badge">${t.name}</span>`).join('');

                    // Render 3D Model
                    const modelBox = document.getElementById('model-container');
                    if (viewerData.model3D) {
                        const m = viewerData.model3D;
                        modelBox.innerHTML = `
                            <model-viewer src="${m.modelUrl}"
                                ${m.posterUrl ? `poster="${m.posterUrl}"` : ''}
                                ${m.autoRotate ? 'auto-rotate' : ''}
                                ${m.cameraOrbit ? `camera-orbit="${m.cameraOrbit}"` : ''}
                                ${m.fieldOfView ? `field-of-view="${m.fieldOfView}"` : ''}
                                camera-controls shadow-intensity="1">
                            </model-viewer>
                            <div style="margin-top:8px; font-size:13px; color:#94a3b8;">
                                File: <b>${m.fileName}</b> (${m.fileFormat.toUpperCase()}, ${(m.fileSize / 1024).toFixed(1)} KB) • Scale: ${m.scale}x
                            </div>`;
                    } else {
                        modelBox.innerHTML = `<div class="empty">Hiện vật này chưa upload mô hình 3D (.glb / .gltf). Hãy upload tại POST /api/artifacts/${artifactId}/model-3d rồi tải lại trang.</div>`;
                    }

                    // Render Media
                    const mediaBox = document.getElementById('media-container');
                    if (viewerData.media && viewerData.media.length > 0) {
                        mediaBox.innerHTML = viewerData.media.map(item => {
                            let preview = '';
                            if (item.mediaType === 'image') preview = `<img src="${item.fileUrl}" alt="${item.title}" />`;
                            else if (item.mediaType === 'audio') preview = `<audio controls src="${item.fileUrl}"></audio>`;
                            else if (item.mediaType === 'video') preview = `<video controls src="${item.fileUrl}"></video>`;
                            return `<div class="media-item">
                                <div style="font-weight:600; font-size:14px; margin-bottom:6px;">${item.title} ${item.languageCode ? '(' + item.languageCode + ')' : ''}</div>
                                ${preview}
                            </div>`;
                        }).join('');
                    } else {
                        mediaBox.innerHTML = `<div class="empty">Chưa có media nào.</div>`;
                    }

                    // Render Contents & Narration
                    const langSelect = document.getElementById('lang-select');
                    const contents = viewerData.contents || [];
                    if (contents.length === 0) {
                        langSelect.style.display = 'none';
                        document.getElementById('content-container').innerHTML = `<div class="empty">Chưa có nội dung xuất bản (Published). Hãy duyệt nội dung bằng tài khoản Curator.</div>`;
                    } else {
                        langSelect.innerHTML = contents.map((c, idx) => `<option value="${idx}">Ngôn ngữ: ${c.languageCode.toUpperCase()}</option>`).join('');
                        langSelect.onchange = () => renderContent(contents[langSelect.value]);
                        renderContent(contents[0]);
                    }
                }

                function renderContent(c) {
                    const box = document.getElementById('content-container');
                    let audioHtml = '';
                    if (c.audioUrl) {
                        audioHtml = `<div style="margin-top:16px;">
                            <div style="font-size:13px; color:#38bdf8; margin-bottom:4px;">🔊 File Audio thuyết minh (${c.languageCode.toUpperCase()}):</div>
                            <audio controls src="${c.audioUrl}"></audio>
                        </div>`;
                    }
                    let ttsBtn = '';
                    if (c.narrationText) {
                        ttsBtn = `<button onclick="speakText()" style="margin-top:10px;">🗣️ Đọc kịch bản bằng giọng nói trình duyệt (TTS)</button>`;
                    }
                    box.innerHTML = `
                        <h4 style="margin:0 0 8px 0; font-size:18px; color:#f8fafc;">${c.title}</h4>
                        <p style="color:#cbd5e1; line-height:1.6;">${c.description}</p>
                        <div style="margin-top:16px; font-weight:600; color:#38bdf8;">📜 Kịch bản thuyết minh (Narration):</div>
                        <div class="narration-box" id="narration-text">${c.narrationText || '(Chưa nhập kịch bản thuyết minh)'}</div>
                        ${ttsBtn}
                        ${audioHtml}
                    `;
                    window.currentNarration = { text: c.narrationText, lang: c.languageCode };
                }

                window.speakText = function() {
                    if (!window.currentNarration || !window.currentNarration.text) return;
                    window.speechSynthesis.cancel();
                    const utter = new SpeechSynthesisUtterance(window.currentNarration.text);
                    utter.lang = window.currentNarration.lang === 'vi' ? 'vi-VN' : 'en-US';
                    window.speechSynthesis.speak(utter);
                };

                load();
            </script>
        </body>
        </html>
        """;

        return Content(html, "text/html", Encoding.UTF8);
    }
}
