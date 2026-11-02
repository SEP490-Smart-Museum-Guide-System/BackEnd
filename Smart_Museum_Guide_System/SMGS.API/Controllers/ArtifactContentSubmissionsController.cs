using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
namespace SMGS.API.Controllers;

[ApiController]
[Route("api/artifacts/{artifactId:int}/contents")]
[Produces("application/json")]
public class ArtifactContentSubmissionsController(IArtifactContentService service) : ControllerBase
{
    /// <summary>Nhận kết quả AI hoặc nội dung nhập và tạo phiên bản mới chờ Curator duyệt.</summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost]
    public async Task<IActionResult> Submit(int artifactId, [FromBody] SubmitArtifactContentRequestDto dto)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var response = await service.SubmitAsync(artifactId, dto, userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Nhập thủ công narration & nội dung đa ngôn ngữ, tạo phiên bản mới chờ Curator duyệt.</summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("manual")]
    public async Task<IActionResult> SubmitManual(int artifactId, [FromBody] SubmitArtifactContentRequestDto dto)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        dto.IsAiGenerated = false;
        var response = await service.SubmitAsync(artifactId, dto, userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Chỉ trả về phiên bản đang xuất bản của ngôn ngữ được chọn.</summary>
    [AllowAnonymous]
    [HttpGet("published")]
    public async Task<IActionResult> GetPublished(int artifactId, [FromQuery] string languageCode = "vi")
    {
        var response = await service.GetPublishedAsync(artifactId, languageCode,
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Trả về tất cả các ngôn ngữ đang xuất bản của hiện vật (kèm narration).</summary>
    [AllowAnonymous]
    [HttpGet("published/all")]
    public async Task<IActionResult> GetAllPublished(int artifactId)
    {
        var response = await service.GetAllPublishedByArtifactAsync(artifactId,
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Lịch sử phiên bản nội dung & narration của hiện vật (dành cho Admin, Museum Staff, Curator).</summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff + "," + RoleNames.Curator)]
    [HttpGet("versions")]
    public async Task<IActionResult> GetVersionHistory(int artifactId, [FromQuery] string? languageCode = null)
    {
        var response = await service.GetVersionHistoryAsync(artifactId, languageCode);
        return StatusCode(response.StatusCode, response);
    }
}