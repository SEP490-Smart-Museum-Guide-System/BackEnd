using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

public class SpeechPreviewRequest
{
    [Required, StringLength(12000, MinimumLength = 1)] public string Text { get; set; } = "";
}

[Route("api/artifacts/{artifactId:int}/ai/speech-preview")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
[EnableRateLimiting("artifact-guide")]
public class ArtifactSpeechController(IStaffAiSource source, IArtifactSpeech speech) : ExperienceControllerBase
{
    [HttpPost, RequestSizeLimit(144000)]
    public async Task<IActionResult> Preview(int artifactId, SpeechPreviewRequest request, CancellationToken cancellationToken)
    {
        if (artifactId <= 0 || string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ResponseDto<object>.Fail("Cần chọn hiện vật và nhập bản thuyết minh để nghe."));
        // Draft listening is staff-only; the same assignment check as the AI editor applies.
        await source.GetAsync(artifactId, UserId, Admin, cancellationToken);
        try
        {
            var audio = await speech.GenerateAsync(request.Text.Trim(), cancellationToken);
            Response.Headers.CacheControl = "private, no-store";
            return File(audio.Bytes, "audio/wav");
        }
        catch (GuideException e) { return StatusCode(e.StatusCode, ResponseDto<object>.Fail(e.Message, e.StatusCode)); }
    }
}
