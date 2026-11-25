using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMGS.Infrastructure.Data;
using SMGS.Application.DTOs.Navigation;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/places")]
public class PlacesController(PlaceSearchService search) : ControllerBase
{
    [HttpGet("museums")]
    [AllowAnonymous]
    [EnableRateLimiting("place-search")]
    public async Task<IActionResult> Museums([FromQuery] string cityCode, [FromServices] ApplicationDbContext db,
        [FromServices] IMemoryCache cache, CancellationToken cancellationToken)
    {
        var name = await db.Cities.Where(c => c.Code == cityCode).Select(c => c.Name).SingleOrDefaultAsync(cancellationToken);
        if (name is null) return BadRequest(new { message = "Tỉnh/thành không hợp lệ." });
        var key = "osm-museums:" + cityCode;
        if (cache.TryGetValue(key, out List<PlaceResponseDto>? saved)) return Ok(new { data = saved });
        var result = await search.MuseumsInProvinceAsync(name, cancellationToken);
        if (result.StatusCode == 200) cache.Set(key, result.Data, TimeSpan.FromHours(1));
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("place-search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var result = await search.SearchAsync(q, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
