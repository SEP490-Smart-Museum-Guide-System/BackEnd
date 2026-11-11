using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[Route("api/personalized-tours"), Authorize(Roles = RoleNames.Visitor)]
public class PersonalizedToursController(IPersonalizedTourService service) : ExperienceControllerBase
{
    [HttpGet("interests")]
    public async Task<IActionResult> Interests(int museumId, CancellationToken ct) => Result(await service.Interests(museumId, ct));
    [HttpPost("recommendations"), EnableRateLimiting("personal-tour")]
    public async Task<IActionResult> Recommend(PersonalizedTourRequest request, CancellationToken ct) => Result(await service.Recommend(request, UserId, ct));
    [HttpPost]
    public async Task<IActionResult> Save(SavePersonalizedTourRequest request, CancellationToken ct) => Result(await service.Save(request.RecommendationId, UserId, ct), 201);
    [HttpGet("me")]
    public async Task<IActionResult> Mine(int page = 1, int pageSize = 20, CancellationToken ct = default) => Result(await service.Mine(UserId, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Result(await service.Get(id, UserId, ct));
}
