using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[Route("api/artifacts/{artifactId:int}/ai/drafts")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
[EnableRateLimiting("artifact-guide")]
public class StaffAiController(IStaffAiService service) : ExperienceControllerBase
{
    [HttpPost, RequestSizeLimit(64000)]
    public async Task<IActionResult> Generate(int artifactId, StaffAiRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GenerateAsync(artifactId, UserId, Admin, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
