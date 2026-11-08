using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/artifacts/{artifactId:int}/guide/messages")]
[Authorize(Roles = RoleNames.Visitor)]
[EnableRateLimiting("artifact-guide")]
public class ArtifactGuideController(IArtifactGuideService guide) : ControllerBase
{
    [HttpPost, RequestSizeLimit(144000)]
    public async Task<IActionResult> Ask(int artifactId, GuideMessageRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập để hỏi trợ lý.", 401));
        var result = await guide.AskAsync(artifactId, userId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
