using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/places")]
public class PlacesController(PlaceSearchService search) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("place-search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var result = await search.SearchAsync(q, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
