using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Auth.Request;
using SMGS.Application.DTOs.Auth.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        var response = await authService.RegisterAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var response = await authService.LoginAsync(dto);
        return StatusCode(response.StatusCode, response);
    }
}