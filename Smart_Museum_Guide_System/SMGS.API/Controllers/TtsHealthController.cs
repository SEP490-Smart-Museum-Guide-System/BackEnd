using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/tts/health")]
public sealed class TtsHealthController(
    IHttpClientFactory clients,
    IOptions<ArtifactSpeechOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https") || baseUri.AbsolutePath != "/")
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });

        try
        {
            using var response = await clients.CreateClient("tts-health").GetAsync(
                new Uri(baseUri, "ready"), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
            return Ok(new { status = "ok", service = "Azure TTS" });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
        }
    }
}
