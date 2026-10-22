using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;
using SMGS.Application.Services;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/navigation/outdoor/routes")]
public class NavigationController(OutdoorNavigationService navigation) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("outdoor-routing")]
    [ProducesResponseType(typeof(ResponseDto<OutdoorRouteResponseDto>), 200)]
    [ProducesResponseType(typeof(ResponseDto<object>), 400)]
    [ProducesResponseType(typeof(ResponseDto<object>), 404)]
    [ProducesResponseType(typeof(ResponseDto<object>), 422)]
    [ProducesResponseType(typeof(ResponseDto<object>), 429)]
    [ProducesResponseType(typeof(ResponseDto<object>), 502)]
    [ProducesResponseType(typeof(ResponseDto<object>), 504)]
    public async Task<IActionResult> Route([FromBody] OutdoorRouteRequestDto request, CancellationToken cancellationToken)
    {
        var response = await navigation.GetRouteAsync(request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
