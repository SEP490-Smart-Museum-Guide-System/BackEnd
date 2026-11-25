using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;

namespace SMGS.API.Controllers;

public sealed class VisitorSpeechRequest
{
    [Required, StringLength(12000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;

    [Required, RegularExpression("^(vi|en|vi-VN|en-US)$")]
    public string Language { get; set; } = "vi-VN";
}

[ApiController]
[AllowAnonymous]
[Route("api/tts")]
[EnableRateLimiting("artifact-guide")]
public sealed class VisitorSpeechController(IArtifactSpeech speech) : ControllerBase
{
    [HttpPost("speech")]
    [RequestSizeLimit(144000)]
    public async Task<IActionResult> Synthesize(VisitorSpeechRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ResponseDto<object>.Fail("Nội dung thuyết minh không được để trống."));

        var language = request.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
        try
        {
            var audio = await speech.GenerateAsync(request.Text.Trim(), cancellationToken, language);
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.ContentLanguage = language;
            return File(audio.Bytes, "audio/wav");
        }
        catch (GuideException e)
        {
            return StatusCode(e.StatusCode, ResponseDto<object>.Fail(e.Message, e.StatusCode));
        }
    }
}
