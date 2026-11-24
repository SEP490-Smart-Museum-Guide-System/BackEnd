using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Common;
using SMGS.Infrastructure.Identity;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/me/language")]
[Produces("application/json")]
[Authorize]
public sealed class LanguageController(UserManager<ApplicationUser> users) : ControllerBase
{
    public sealed record LanguageRequest(string LanguageCode);
    public sealed record LanguageResponse(string LanguageCode);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        return Ok(ResponseDto<LanguageResponse>.Success(new(user.PreferredLanguage)));
    }

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] LanguageRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));

        var language = request.LanguageCode?.Trim().ToLowerInvariant();
        if (language is not ("vi" or "en"))
            return BadRequest(ResponseDto<object>.Fail("LanguageCode chỉ nhận 'vi' hoặc 'en'.", 400));

        user.PreferredLanguage = language;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            return StatusCode(500, ResponseDto<object>.Fail("Không thể lưu ngôn ngữ ưu tiên.", 500));

        Response.Headers["Content-Language"] = language;
        return Ok(ResponseDto<LanguageResponse>.Success(new(language), "Đã cập nhật ngôn ngữ."));
    }

    private Task<ApplicationUser?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? users.FindByIdAsync(userId.ToString()) : Task.FromResult<ApplicationUser?>(null);
    }
}
