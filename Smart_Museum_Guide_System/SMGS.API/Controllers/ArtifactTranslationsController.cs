using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.API.Controllers;

[ApiController, Route("api/artifacts/{artifactId:int}/translations"), Authorize]
public sealed class ArtifactTranslationsController(ApplicationDbContext db, IAccountAdminService accounts) : ControllerBase
{
    public sealed class ArtifactTranslationRequest
    {
        [Required, StringLength(300)] public string Name { get; set; } = string.Empty;
    }

    public sealed record ArtifactTranslationResponse(int ArtifactId, string LanguageCode, bool IsTranslated, string Name);

    [HttpGet("{languageCode}")]
    public async Task<IActionResult> Get(int artifactId, string languageCode, CancellationToken cancellationToken)
    {
        if (!await CanManage(artifactId)) return Forbid();
        languageCode = languageCode.ToLowerInvariant();
        if (!ValidLanguage(languageCode)) return BadRequest(ResponseDto<object>.Fail("LanguageCode must be 'vi' or 'en'."));
        var artifact = await db.Artifacts.Include(a => a.Translations).SingleOrDefaultAsync(a => a.Id == artifactId, cancellationToken);
        if (artifact is null) return NotFound(ResponseDto<object>.Fail("Artifact was not found.", 404));
        var name = languageCode == "en" ? artifact.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name ?? artifact.Name : artifact.Name;
        var exists = languageCode == "vi" || artifact.Translations.Any(t => t.LanguageCode == "en");
        return Ok(ResponseDto<ArtifactTranslationResponse>.Success(new(artifactId, languageCode, exists, name)));
    }

    [HttpPut("{languageCode}")]
    public async Task<IActionResult> Put(int artifactId, string languageCode, [FromBody] ArtifactTranslationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManage(artifactId)) return Forbid();
        languageCode = languageCode.ToLowerInvariant();
        if (!ValidLanguage(languageCode)) return BadRequest(ResponseDto<object>.Fail("LanguageCode must be 'vi' or 'en'."));
        var artifact = await db.Artifacts.Include(a => a.Translations).SingleOrDefaultAsync(a => a.Id == artifactId, cancellationToken);
        if (artifact is null) return NotFound(ResponseDto<object>.Fail("Artifact was not found.", 404));
        if (languageCode == "vi") artifact.Name = request.Name.Trim();
        else
        {
            var translation = artifact.Translations.FirstOrDefault(t => t.LanguageCode == "en");
            if (translation is null)
            {
                translation = new ArtifactTranslation { ArtifactId = artifactId, LanguageCode = "en", Name = request.Name.Trim() };
                db.ArtifactTranslations.Add(translation);
            }
            translation.Name = request.Name.Trim();
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ResponseDto<ArtifactTranslationResponse>.Success(new(artifactId, languageCode, true, request.Name.Trim()), "Translation saved."));
    }

    private async Task<bool> CanManage(int artifactId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        && await accounts.CanManageArtifactAsync(userId, User.IsInRole(RoleNames.Admin), artifactId);

    private static bool ValidLanguage(string code) => code is "vi" or "en";
}
