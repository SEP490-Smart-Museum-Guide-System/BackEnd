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
    /// <summary>Nhận kết quả AI và tạo phiên bản mới chờ Curator duyệt.</summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost]
    public async Task<IActionResult> Submit(int artifactId, [FromBody] SubmitArtifactContentRequestDto dto)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var response = await service.SubmitAsync(artifactId, dto, userId);
        return StatusCode(response.StatusCode, response);
    }
    /// <summary>Chỉ trả về phiên bản đang xuất bản của ngôn ngữ được chọn.</summary>
    [Authorize]
    [HttpGet("published")]
    public async Task<IActionResult> GetPublished(int artifactId, [FromQuery] string languageCode = "vi")
    {
        var response = await service.GetPublishedAsync(artifactId, languageCode);
        return StatusCode(response.StatusCode, response);
    }
}
