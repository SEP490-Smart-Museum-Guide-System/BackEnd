using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Data;

namespace SMGS.API.Controllers;

[ApiController, Route("api/museums/{museumId:int}/translations"), Authorize]
public sealed class MuseumTranslationsController(ApplicationDbContext db, IAccountAdminService accounts) : ControllerBase
{
    public sealed class MuseumTranslationRequest
    {
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [StringLength(2000)] public string? Description { get; set; }
        [StringLength(500)] public string? Address { get; set; }
        [StringLength(500)] public string? OpeningHours { get; set; }
    }

    public sealed record MuseumTranslationResponse(int MuseumId, string LanguageCode, bool IsTranslated, string Name, string? Description, string? Address, string? OpeningHours);

    [HttpGet("{languageCode}")]
    public async Task<IActionResult> Get(int museumId, string languageCode, CancellationToken cancellationToken)
    {
        if (!await CanManage(museumId, cancellationToken)) return Forbid();
        languageCode = languageCode.ToLowerInvariant();
        if (!ValidLanguage(languageCode)) return BadRequest(ResponseDto<object>.Fail("LanguageCode debe ser 'vi' o 'en'."));
        var museum = await db.Museums.Include(m => m.Translations).SingleOrDefaultAsync(m => m.Id == museumId, cancellationToken);
        if (museum is null) return NotFound(ResponseDto<object>.Fail("No se encontró el museo.", 404));
        var t = languageCode == "en" ? museum.Translations.FirstOrDefault(x => x.LanguageCode == "en") : null;
        return Ok(ResponseDto<MuseumTranslationResponse>.Success(new(museumId, languageCode, languageCode == "vi" || t is not null,
            t?.Name ?? museum.Name, t?.Description ?? museum.Description, t?.Address ?? museum.Address, t?.OpeningHours ?? museum.OpeningHours)));
    }

    [HttpPut("{languageCode}")]
    public async Task<IActionResult> Put(int museumId, string languageCode, [FromBody] MuseumTranslationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManage(museumId, cancellationToken)) return Forbid();
        languageCode = languageCode.ToLowerInvariant();
        if (!ValidLanguage(languageCode)) return BadRequest(ResponseDto<object>.Fail("LanguageCode debe ser 'vi' o 'en'."));
        var museum = await db.Museums.Include(m => m.Translations).SingleOrDefaultAsync(m => m.Id == museumId, cancellationToken);
        if (museum is null) return NotFound(ResponseDto<object>.Fail("No se encontró el museo.", 404));
        if (languageCode == "vi")
        {
            museum.Name = request.Name.Trim(); museum.Description = Clean(request.Description);
            museum.Address = Clean(request.Address); museum.OpeningHours = Clean(request.OpeningHours);
        }
        else
        {
            var translation = museum.Translations.FirstOrDefault(x => x.LanguageCode == "en");
            if (translation is null)
            {
                translation = new MuseumTranslation { MuseumId = museumId, LanguageCode = "en", Name = request.Name.Trim() };
                db.MuseumTranslations.Add(translation);
            }
            translation.Name = request.Name.Trim(); translation.Description = Clean(request.Description);
            translation.Address = Clean(request.Address); translation.OpeningHours = Clean(request.OpeningHours);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ResponseDto<MuseumTranslationResponse>.Success(new(museumId, languageCode, true, request.Name.Trim(),
            Clean(request.Description), Clean(request.Address), Clean(request.OpeningHours)), "Translation saved."));
    }

    private async Task<bool> CanManage(int museumId, CancellationToken token) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        && await accounts.CanManageMuseumAsync(userId, User.IsInRole(RoleNames.Admin), museumId);

    private static bool ValidLanguage(string code) => code is "vi" or "en";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
