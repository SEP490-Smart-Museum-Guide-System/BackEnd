using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
namespace SMGS.API.Controllers;

[ApiController]
[Route("api/artifact-contents")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Curator)]
public class ArtifactContentsController(IArtifactContentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? museumId, [FromQuery] int? artifactId,
        [FromQuery] string? status = ContentStatuses.PendingReview, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var response = await service.GetAllAsync(museumId, artifactId, status, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await service.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }
    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, [FromBody] ReviewArtifactContentRequestDto dto) => Review(id, dto, true);
    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, [FromBody] ReviewArtifactContentRequestDto dto) => Review(id, dto, false);
    private async Task<IActionResult> Review(int id, ReviewArtifactContentRequestDto dto, bool approve)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var response = await service.ReviewAsync(id, dto, userId, approve);
        return StatusCode(response.StatusCode, response);
    }
}
